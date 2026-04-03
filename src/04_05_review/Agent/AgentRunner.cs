using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Review.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Review.Agent
{
    internal static class AgentRunner
    {
        private const int MaxSteps = 12;

        /// <summary>
        /// Run a tool-calling agent loop.
        /// Returns the final text response from the model.
        /// </summary>
        public static async Task<string> RunAsync(
            string input,
            string instructions,
            string model,
            List<ToolSpec> tools)
        {
            string resolvedModel = AiConfig.ResolveModel(model);

            // Build tool definitions array
            var toolDefs = new JArray();
            var handlerMap = new Dictionary<string, Func<string, string>>();
            foreach (var tool in tools)
            {
                toolDefs.Add(tool.Definition);
                string name = tool.Definition["name"]?.ToString();
                if (name != null)
                    handlerMap[name] = tool.Handler;
            }

            // Initial input
            var inputMessages = new JArray
            {
                new JObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = input
                }
            };

            for (int step = 0; step < MaxSteps; step++)
            {
                var body = new JObject
                {
                    ["model"] = resolvedModel,
                    ["instructions"] = instructions,
                    ["input"] = inputMessages,
                    ["tools"] = toolDefs,
                    ["parallel_tool_calls"] = false
                };

                string responseJson = await PostAsync(body.ToString(Formatting.None));
                JObject parsed;
                try { parsed = JObject.Parse(responseJson); }
                catch (Exception ex)
                {
                    return "Error parsing API response: " + ex.Message;
                }

                if (parsed["error"] != null)
                {
                    string errMsg = parsed["error"]["message"]?.ToString() ?? "Unknown API error";
                    return "API error: " + errMsg;
                }

                // Check for tool calls
                var outputArray = parsed["output"] as JArray;
                if (outputArray == null)
                    return ExtractText(parsed);

                var toolCalls = new List<JToken>();
                foreach (JToken item in outputArray)
                {
                    if (item["type"]?.ToString() == "function_call")
                        toolCalls.Add(item);
                }

                if (toolCalls.Count == 0)
                    return ExtractText(parsed);

                // Process tool calls and feed results back
                // Add all output items to the next input
                foreach (JToken item in outputArray)
                    inputMessages.Add(item);

                foreach (var call in toolCalls)
                {
                    string fnName = call["name"]?.ToString();
                    string callId = call["call_id"]?.ToString();
                    string argsStr = call["arguments"]?.ToString() ?? "{}";

                    string result;
                    if (fnName != null && handlerMap.ContainsKey(fnName))
                    {
                        try { result = handlerMap[fnName](argsStr); }
                        catch (Exception ex)
                        {
                            result = JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }
                    else
                    {
                        result = JsonConvert.SerializeObject(new { error = "Unknown tool: " + fnName });
                    }

                    inputMessages.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = callId,
                        ["output"] = result
                    });
                }
            }

            return "(max steps reached)";
        }

        private static string ExtractText(JObject parsed)
        {
            string outputText = parsed["output_text"]?.ToString();
            if (!string.IsNullOrWhiteSpace(outputText)) return outputText;

            var outputArray = parsed["output"] as JArray;
            if (outputArray != null)
            {
                foreach (JToken item in outputArray)
                {
                    if (item["type"]?.ToString() == "message")
                    {
                        var content = item["content"] as JArray;
                        if (content != null)
                        {
                            foreach (JToken part in content)
                            {
                                if (part["type"]?.ToString() == "output_text")
                                {
                                    string t = part["text"]?.ToString();
                                    if (!string.IsNullOrEmpty(t)) return t;
                                }
                            }
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static async Task<string> PostAsync(string jsonBody)
        {
            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromMinutes(3);
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
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }
    }
}
