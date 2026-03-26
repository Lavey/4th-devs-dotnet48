using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.ContextAgent.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MemSession = FourthDevs.ContextAgent.Memory.Session;

namespace FourthDevs.ContextAgent.Agent
{
    internal class AgentRunResult
    {
        public string Response { get; set; }
        public int Turns { get; set; }
        public int TotalEstimatedTokens { get; set; }
        public int TotalActualTokens { get; set; }
    }

    internal static class AgentRunner
    {
        private const int MaxTurns = 10;

        public static async Task<AgentRunResult> RunAsync(
            MemSession session,
            string userMessage,
            AgentTemplate template)
        {
            // Reset observer flag for this request
            session.Memory.ObserverRanThisRequest = false;

            // Add user message
            var userMsg = new JObject
            {
                ["type"] = "message",
                ["role"] = "user",
                ["content"] = userMessage
            };
            session.Messages.Add(userMsg);

            int totalActualTokens = 0;
            int turns = 0;
            string model = AiConfig.ResolveModel(template.Model);

            for (int turn = 0; turn < MaxTurns; turn++)
            {
                turns = turn + 1;

                // Process memory and build context
                var ctx = await MemoryProcessor.ProcessAsync(template.SystemPrompt, session)
                    .ConfigureAwait(false);

                // Build request
                var inputArray = new JArray();
                foreach (var m in ctx.Messages)
                    inputArray.Add(m);

                var body = new JObject
                {
                    ["model"] = model,
                    ["instructions"] = ctx.SystemPrompt,
                    ["input"] = inputArray,
                    ["tools"] = AgentTools.GetToolDefinitions(),
                    ["store"] = false
                };

                var response = await PostAsync(body).ConfigureAwait(false);

                if (response["usage"] != null)
                {
                    totalActualTokens += ((int?)response["usage"]["input_tokens"] ?? 0)
                        + ((int?)response["usage"]["output_tokens"] ?? 0);
                }

                // Process output items
                var output = response["output"] as JArray ?? new JArray();
                var toolCalls = new List<JObject>();
                string finalText = null;

                foreach (JObject item in output)
                {
                    string itemType = (string)item["type"];

                    if (itemType == "function_call")
                    {
                        // Add to session messages
                        var fcMsg = new JObject
                        {
                            ["type"] = "function_call",
                            ["call_id"] = item["call_id"],
                            ["name"] = item["name"],
                            ["arguments"] = item["arguments"]
                        };
                        session.Messages.Add(fcMsg);
                        toolCalls.Add(item);
                    }
                    else if (itemType == "message")
                    {
                        // Extract text from message content
                        var contentToken = item["content"];
                        string text = null;
                        if (contentToken != null)
                        {
                            if (contentToken.Type == JTokenType.String)
                                text = (string)contentToken;
                            else if (contentToken.Type == JTokenType.Array)
                            {
                                foreach (JObject c in (JArray)contentToken)
                                {
                                    if ((string)c["type"] == "output_text")
                                    { text = (string)c["text"]; break; }
                                    if (c["text"] != null)
                                    { text = (string)c["text"]; break; }
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(text))
                        {
                            finalText = text;
                            // Add assistant message to session
                            var assistantMsg = new JObject
                            {
                                ["type"] = "message",
                                ["role"] = "assistant",
                                ["content"] = text
                            };
                            session.Messages.Add(assistantMsg);
                        }
                    }
                }

                if (toolCalls.Count == 0)
                {
                    return new AgentRunResult
                    {
                        Response = finalText ?? "No response",
                        Turns = turns,
                        TotalActualTokens = totalActualTokens,
                        TotalEstimatedTokens = EstimateTokens(userMessage) + EstimateTokens(finalText ?? "")
                    };
                }

                // Execute tool calls
                foreach (JObject tc in toolCalls)
                {
                    string callId = (string)tc["call_id"];
                    string name = (string)tc["name"];
                    string arguments = (string)tc["arguments"] ?? "{}";

                    string toolOutput = await AgentTools.ExecuteAsync(name, arguments)
                        .ConfigureAwait(false);

                    var outputMsg = new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = callId,
                        ["output"] = toolOutput
                    };
                    session.Messages.Add(outputMsg);
                }
            }

            throw new InvalidOperationException("Exceeded maximum turns");
        }

        private static async Task<JObject> PostAsync(JObject body)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "X-Title", AiConfig.AppName);
                }

                string json = body.ToString(Formatting.None);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var resp = await http.PostAsync(AiConfig.ApiEndpoint, content)
                    .ConfigureAwait(false))
                {
                    string responseBody = await resp.Content.ReadAsStringAsync()
                        .ConfigureAwait(false);
                    var parsed = JObject.Parse(responseBody);

                    if (!resp.IsSuccessStatusCode)
                    {
                        string errMsg = (string)parsed["error"]?["message"]
                            ?? string.Format("Request failed with status {0}", (int)resp.StatusCode);
                        throw new InvalidOperationException(errMsg);
                    }

                    return parsed;
                }
            }
        }

        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Length / 4;
        }
    }
}
