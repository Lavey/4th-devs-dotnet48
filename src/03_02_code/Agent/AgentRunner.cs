using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Code.Models;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Code.Agent
{
    /// <summary>
    /// Runs the code execution agent loop using the Responses API.
    ///
    /// Mirrors agent.ts from 03_02_code (i-am-alice/4th-devs).
    /// </summary>
    internal static class AgentRunner
    {
        private const int MaxTurns = 25;

        /// <summary>
        /// Runs the agent loop with the given tools and task.
        /// Returns the final text result and the number of turns taken.
        /// </summary>
        public static async Task<AgentResult> RunAsync(
            string model,
            string systemPrompt,
            string task,
            List<LocalToolDefinition> tools)
        {
            string resolvedModel = AiConfig.ResolveModel(model);

            // Build the tools array for the Responses API
            JArray toolsArray = BuildToolsArray(tools);

            // Build the conversation (Responses API input format)
            var conversation = new JArray
            {
                new JObject
                {
                    ["type"] = "message",
                    ["role"] = "system",
                    ["content"] = systemPrompt
                },
                new JObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = task
                }
            };

            // Build a handler map for quick tool lookup
            var handlers = new Dictionary<string, Func<JObject, Task<object>>>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var tool in tools)
                handlers[tool.Name] = tool.Handler;

            for (int turn = 0; turn < MaxTurns; turn++)
            {
                ColorLine($"\n[agent] Turn {turn + 1}/{MaxTurns}", ConsoleColor.Cyan);

                var body = new JObject
                {
                    ["model"] = resolvedModel,
                    ["input"] = conversation,
                };
                if (toolsArray.Count > 0)
                    body["tools"] = toolsArray;

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                ResponsesResponse parsed;
                try
                {
                    parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);
                }
                catch (Exception ex)
                {
                    return new AgentResult
                    {
                        Text = "Agent error: failed to parse API response – " + ex.Message,
                        Turns = turn + 1
                    };
                }

                if (parsed == null || parsed.Error != null)
                {
                    return new AgentResult
                    {
                        Text = "Agent error: " + (parsed?.Error?.Message ?? "null response"),
                        Turns = turn + 1
                    };
                }

                // Log token usage
                if (parsed.Usage != null)
                {
                    ColorLine($"[agent] Tokens: in={parsed.Usage.InputTokens} out={parsed.Usage.OutputTokens}",
                        ConsoleColor.DarkGray);
                }

                List<OutputItem> toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                // No tool calls → model returned final answer
                if (toolCalls.Count == 0)
                {
                    string text = ResponsesApiClient.ExtractText(parsed);
                    ColorLine("[agent] Completed", ConsoleColor.Green);
                    return new AgentResult { Text = text, Turns = turn + 1 };
                }

                // Record function_call items in conversation
                foreach (OutputItem item in parsed.Output)
                {
                    if (item.Type == "function_call")
                    {
                        conversation.Add(new JObject
                        {
                            ["type"] = "function_call",
                            ["call_id"] = item.CallId,
                            ["name"] = item.Name,
                            ["arguments"] = item.Arguments
                        });
                    }
                }

                // Execute each tool call and record results
                foreach (OutputItem call in toolCalls)
                {
                    JObject args;
                    try
                    {
                        args = JObject.Parse(call.Arguments ?? "{}");
                    }
                    catch
                    {
                        args = new JObject();
                    }

                    string argsPreview = Truncate(args.ToString(Formatting.None), 120);
                    ColorLine($"[agent] Tool: {call.Name}({argsPreview})", ConsoleColor.DarkYellow);

                    string result;
                    try
                    {
                        Func<JObject, Task<object>> handler;
                        if (!handlers.TryGetValue(call.Name, out handler))
                        {
                            result = JsonConvert.SerializeObject(new { error = "Unknown tool: " + call.Name });
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

                    string resultPreview = Truncate(result, 200);
                    ColorLine($"[agent]   -> {resultPreview}", ConsoleColor.DarkGray);

                    conversation.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = call.CallId,
                        ["output"] = result
                    });
                }
            }

            return new AgentResult
            {
                Text = "Agent exceeded maximum turns (" + MaxTurns + ")",
                Turns = MaxTurns
            };
        }

        // ----------------------------------------------------------------
        // HTTP helper – posts raw JSON to the Responses API
        // ----------------------------------------------------------------

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

        // ----------------------------------------------------------------
        // Tool array builder
        // ----------------------------------------------------------------

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

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static string Truncate(string s, int max)
            => s != null && s.Length > max ? s.Substring(0, max) + "..." : s ?? string.Empty;

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Result of an agent run.
    /// </summary>
    internal class AgentResult
    {
        public string Text { get; set; } = string.Empty;
        public int Turns { get; set; }
    }
}
