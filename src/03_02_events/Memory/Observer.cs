using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FourthDevs.Common;
using FourthDevs.Events.Helpers;
using FourthDevs.Events.Models;

namespace FourthDevs.Events.Memory
{
    /// <summary>
    /// Extracts observations from conversation history using an LLM.
    /// </summary>
    internal static class Observer
    {
        private const string SystemPrompt =
@"You are an observation extractor. Given a conversation history, extract concise factual observations.
Return ONLY a JSON array of strings, each being one observation. Example:
[""The user requested a research report on AI trends"", ""Agent found 3 relevant sources""]
If there are no new observations, return an empty array: []";

        public static async Task<List<string>> ExtractObservations(
            List<JObject> messages, int fromIndex, string model)
        {
            if (messages == null || fromIndex >= messages.Count)
                return new List<string>();

            var relevantMessages = new List<string>();
            for (int i = fromIndex; i < messages.Count; i++)
            {
                string role = messages[i]["role"]?.ToString() ?? "unknown";
                string content = messages[i]["content"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(content))
                    relevantMessages.Add(role + ": " + content);
            }

            if (relevantMessages.Count == 0)
                return new List<string>();

            string conversationText = string.Join("\n", relevantMessages);

            try
            {
                string responseText = await CallChatCompletions(
                    model, SystemPrompt, conversationText, 0.3);

                // Parse JSON array
                responseText = responseText.Trim();
                if (responseText.StartsWith("["))
                {
                    var arr = JArray.Parse(responseText);
                    var result = new List<string>();
                    foreach (var item in arr)
                    {
                        string s = item.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                            result.Add(s);
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Core.Logger.Warn("observer", "Failed to extract observations: " + ex.Message);
            }

            return new List<string>();
        }

        internal static async Task<string> CallChatCompletions(
            string model, string systemPrompt, string userMessage, double temperature)
        {
            // Use Chat Completions API endpoint for temperature control
            string resolvedModel = AiConfig.ResolveModel(model ?? "gpt-4.1");
            string endpoint = AiConfig.Provider == "openai"
                ? "https://api.openai.com/v1/chat/completions"
                : "https://openrouter.ai/api/v1/chat/completions";

            var payload = new JObject
            {
                ["model"] = resolvedModel,
                ["temperature"] = temperature,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = systemPrompt },
                    new JObject { ["role"] = "user", ["content"] = userMessage }
                }
            };

            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", AiConfig.AppName);
                }

                string json = payload.ToString(Formatting.None);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(endpoint, content))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    var parsed = JObject.Parse(body);

                    var choices = parsed["choices"] as JArray;
                    if (choices != null && choices.Count > 0)
                    {
                        return choices[0]["message"]?["content"]?.ToString() ?? "";
                    }

                    return "";
                }
            }
        }
    }
}
