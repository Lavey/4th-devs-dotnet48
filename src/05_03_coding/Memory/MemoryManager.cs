using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.CodingAgent.Config;
using FourthDevs.CodingAgent.Logging;
using FourthDevs.CodingAgent.Models;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.CodingAgent.Memory
{
    /// <summary>
    /// Manages conversation memory compaction and summary persistence.
    /// Mirrors memory.ts (maybeCompactMemory, persistSummary, buildInstructions).
    /// </summary>
    internal static class MemoryManager
    {
        /// <summary>
        /// Builds instructions by combining the system prompt with an optional session summary.
        /// </summary>
        public static string BuildInstructions(string basePrompt, string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
                return basePrompt;

            return basePrompt + "\n\nSession summary:\n" + summary;
        }

        /// <summary>
        /// Checks whether memory should be compacted and, if so, summarizes
        /// older messages and trims the session.
        /// </summary>
        public static async Task MaybeCompactMemoryAsync(Session session, AgentLogger logger)
        {
            string serialized = Session.SerializeMessages(session.Messages);
            bool needsCompaction = session.Messages.Count > AgentConfig.CompactAfterMessages
                || serialized.Length > AgentConfig.CompactAfterChars;

            if (!needsCompaction)
                return;

            int splitIndex = session.Messages.Count - AgentConfig.KeepRecentMessages;
            if (splitIndex < 0) splitIndex = 0;

            var olderMessages = session.Messages.Take(splitIndex).ToList();
            if (olderMessages.Count == 0)
                return;

            logger.Info("memory", string.Format("Compacting {0} older message(s)", olderMessages.Count));

            try
            {
                string currentSummary = !string.IsNullOrWhiteSpace(session.Summary)
                    ? "Current summary:\n" + session.Summary
                    : "Current summary:\n[none]";

                string olderSerialized = Session.SerializeMessages(olderMessages);
                string inputText = currentSummary + "\n\nConversation to fold into the summary:\n" + olderSerialized;

                string model = AiConfig.ResolveModel(AgentConfig.MemoryModel);

                var body = new JObject
                {
                    ["model"] = model,
                    ["instructions"] = AgentConfig.MemoryPrompt,
                    ["input"] = inputText,
                    ["store"] = false
                };

                string responseJson = await PostAsync(body.ToString(Formatting.None));
                var parsed = JObject.Parse(responseJson);

                string outputText = (string)parsed["output_text"];
                if (string.IsNullOrWhiteSpace(outputText))
                {
                    // Try to extract from output array
                    var output = parsed["output"] as JArray;
                    if (output != null)
                    {
                        foreach (var item in output)
                        {
                            if ((string)item["type"] == "message")
                            {
                                var content = item["content"] as JArray;
                                if (content != null)
                                {
                                    foreach (var part in content)
                                    {
                                        if ((string)part["type"] == "output_text" && part["text"] != null)
                                        {
                                            outputText = (string)part["text"];
                                            break;
                                        }
                                    }
                                }
                            }
                            if (!string.IsNullOrWhiteSpace(outputText)) break;
                        }
                    }
                }

                string nextSummary = outputText != null ? outputText.Trim() : null;
                if (string.IsNullOrEmpty(nextSummary))
                    return;

                session.Summary = nextSummary;
                session.Messages = session.Messages.Skip(splitIndex).ToList();

                PersistSummary(session);
                logger.Event("memory.compacted", new JObject
                {
                    ["summarizedMessages"] = olderMessages.Count,
                    ["keptMessages"] = session.Messages.Count,
                    ["summaryChars"] = session.Summary.Length
                });
            }
            catch (System.Exception ex)
            {
                logger.Error("memory", ex, "Compaction failed");
            }
        }

        private static void PersistSummary(Session session)
        {
            string memoryDir = AgentConfig.GetMemoryDir();
            Directory.CreateDirectory(memoryDir);

            string path = Path.Combine(memoryDir, session.Id + ".md");
            var content = new StringBuilder();
            content.AppendLine("# Session " + session.Id);
            content.AppendLine();
            content.AppendLine("Updated: " + System.DateTime.UtcNow.ToString("o"));
            content.AppendLine();
            content.AppendLine("## Summary");
            content.AppendLine();
            content.AppendLine(string.IsNullOrEmpty(session.Summary) ? "[empty]" : session.Summary);
            content.AppendLine();

            File.WriteAllText(path, content.ToString());
        }

        private static async Task<string> PostAsync(string jsonBody)
        {
            using (var http = new HttpClient())
            {
                http.Timeout = System.TimeSpan.FromMinutes(5);
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", AiConfig.AppName);
                }

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new System.InvalidOperationException(
                            string.Format("Memory API call failed ({0}): {1}",
                                (int)response.StatusCode, body));
                    }
                    return body;
                }
            }
        }
    }
}
