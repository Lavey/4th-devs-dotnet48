using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Browser.Models;
using FourthDevs.Browser.Prompts;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Browser.Agent
{
    internal static class AgentRunner
    {
        private const int MaxTurns = 20;

        public static async Task<AgentRunResult> RunAsync(
            string model,
            string task,
            List<LocalToolDefinition> tools,
            string previousResponseId = null)
        {
            string resolvedModel = AiConfig.ResolveModel(model);
            JArray toolsArray = BuildToolsArray(tools);

            var handlers = new Dictionary<string, Func<JObject, Task<object>>>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var tool in tools)
                handlers[tool.Name] = tool.Handler;

            JArray input = new JArray
            {
                new JObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = task
                }
            };

            string currentResponseId = previousResponseId;

            for (int turn = 0; turn < MaxTurns; turn++)
            {
                ColorLine($"\n[agent] Turn {turn + 1}/{MaxTurns}", ConsoleColor.Cyan);

                var body = new JObject
                {
                    ["model"] = resolvedModel,
                    ["store"] = true,
                    ["input"] = input
                };

                if (toolsArray.Count > 0)
                    body["tools"] = toolsArray;

                // Send instructions only for the first request (no prior context)
                if (currentResponseId == null)
                    body["instructions"] = AgentPrompts.SystemPrompt;
                else
                    body["previous_response_id"] = currentResponseId;

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));

                JObject parsed;
                try
                {
                    parsed = JObject.Parse(responseJson);
                }
                catch (Exception ex)
                {
                    return new AgentRunResult
                    {
                        Text = "Agent error: failed to parse API response – " + ex.Message,
                        Turns = turn + 1
                    };
                }

                if (parsed["error"] != null)
                {
                    string errMsg = parsed["error"]["message"]?.ToString() ?? "Unknown error";
                    return new AgentRunResult
                    {
                        Text = "Agent error: " + errMsg,
                        Turns = turn + 1
                    };
                }

                currentResponseId = parsed["id"]?.ToString();

                // Log token usage
                var usage = parsed["usage"];
                if (usage != null)
                {
                    ColorLine(
                        $"[agent] Tokens: in={usage["input_tokens"]} out={usage["output_tokens"]}",
                        ConsoleColor.DarkGray);
                }

                // Collect tool calls from output
                var toolCalls = new List<JObject>();
                var outputArray = parsed["output"] as JArray;
                if (outputArray != null)
                {
                    foreach (JToken item in outputArray)
                    {
                        if (item["type"]?.ToString() == "function_call")
                            toolCalls.Add((JObject)item);
                    }
                }

                // No tool calls → final answer
                if (toolCalls.Count == 0)
                {
                    string text = ExtractText(parsed);
                    ColorLine("[agent] Completed", ConsoleColor.Green);
                    return new AgentRunResult
                    {
                        Text = text,
                        ResponseId = currentResponseId,
                        Turns = turn + 1
                    };
                }

                // Execute tool calls; build next input from results
                input = new JArray();
                foreach (JObject call in toolCalls)
                {
                    string toolName = call["name"]?.ToString();
                    string callId = call["call_id"]?.ToString();

                    JObject args;
                    try { args = JObject.Parse(call["arguments"]?.ToString() ?? "{}"); }
                    catch { args = new JObject(); }

                    ColorLine($"[agent] Tool: {toolName}({Truncate(args.ToString(Formatting.None), 120)})",
                        ConsoleColor.DarkYellow);

                    string result;
                    try
                    {
                        Func<JObject, Task<object>> handler;
                        if (!handlers.TryGetValue(toolName, out handler))
                        {
                            result = JsonConvert.SerializeObject(new { error = "Unknown tool: " + toolName });
                        }
                        else
                        {
                            object resultObj = await handler(args);
                            result = resultObj is string s
                                ? s
                                : JsonConvert.SerializeObject(resultObj, Formatting.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        result = JsonConvert.SerializeObject(new { error = ex.Message });
                    }

                    ColorLine($"[agent]   -> {Truncate(result, 200)}", ConsoleColor.DarkGray);

                    input.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = callId,
                        ["output"] = result
                    });
                }
            }

            return new AgentRunResult
            {
                Text = "Agent exceeded maximum turns (" + MaxTurns + ")",
                ResponseId = currentResponseId,
                Turns = MaxTurns
            };
        }

        private static string ExtractText(JObject parsed)
        {
            // Try output_text shorthand
            string outputText = parsed["output_text"]?.ToString();
            if (!string.IsNullOrWhiteSpace(outputText)) return outputText;

            // Walk output array for message content
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
                                    string text = part["text"]?.ToString();
                                    if (!string.IsNullOrEmpty(text)) return text;
                                }
                            }
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static async Task<string> PostRawAsync(string jsonBody)
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

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        private static JArray BuildToolsArray(List<LocalToolDefinition> tools)
        {
            var arr = new JArray();
            foreach (var tool in tools)
            {
                arr.Add(new JObject
                {
                    ["type"] = "function",
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = tool.Parameters ?? new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject(),
                        ["additionalProperties"] = false
                    }
                });
            }
            return arr;
        }

        private static string Truncate(string s, int max)
            => s != null && s.Length > max ? s.Substring(0, max) + "..." : s ?? string.Empty;

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
