using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using FourthDevs.System.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.System.Agent
{
    /// <summary>
    /// Runs an agent loop for a given agent template.
    /// Supports recursive delegation via the <c>delegate</c> tool.
    /// Turn-based loop with configurable max steps and delegation depth.
    ///
    /// Mirrors 04_04_system/src/agent.js (i-am-alice/4th-devs).
    /// </summary>
    internal static class AgentRunner
    {
        private const int MaxSteps = 10;
        private const int MaxDepth = 2;

        // ----------------------------------------------------------------
        // Public entry point
        // ----------------------------------------------------------------

        public static Task<string> RunAgentAsync(string agentName, string task)
            => RunAsync(agentName, task, 0);

        // ----------------------------------------------------------------
        // Recursive implementation
        // ----------------------------------------------------------------

        private static async Task<string> RunAsync(string agentName, string task, int depth)
        {
            try
            {
                if (depth > MaxDepth)
                    return "Max agent depth exceeded";

                ColorLine($"[{agentName}] Starting (depth: {depth})", ConsoleColor.Cyan);

                AgentTemplate template = AgentLoader.Load(agentName);

                // Strip optional provider prefix from model name
                string rawModel = template.Model;
                if (rawModel.Contains(":"))
                {
                    int idx = rawModel.IndexOf(':');
                    rawModel = rawModel.Substring(idx + 1);
                }
                string model = AiConfig.ResolveModel(rawModel);

                // Build tool definitions for this agent
                // When at max depth, exclude delegate to prevent further recursion
                bool canDelegate = depth < MaxDepth;
                JArray toolsArray = ToolDefinitions.BuildFor(template.Tools, canDelegate);

                // Build conversation with system prompt via instructions field
                var conversation = new JArray
                {
                    new JObject { ["role"] = "user", ["content"] = task }
                };

                // Agent loop
                for (int step = 0; step < MaxSteps; step++)
                {
                    var body = new JObject
                    {
                        ["model"] = model,
                        ["input"] = conversation,
                        ["instructions"] = template.SystemPrompt,
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
                        return $"Agent error: failed to parse API response – {ex.Message}";
                    }

                    if (parsed?.Error != null)
                        return $"Agent error: {parsed.Error.Message}";

                    List<OutputItem> toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                    // No tool calls → agent is done
                    if (toolCalls.Count == 0)
                    {
                        string text = ResponsesApiClient.ExtractText(parsed);
                        ColorLine($"[{agentName}] Completed", ConsoleColor.Green);
                        return text;
                    }

                    // Append function_call items to conversation
                    foreach (OutputItem item in parsed.Output)
                    {
                        if (item.Type == "function_call")
                        {
                            conversation.Add(new JObject
                            {
                                ["type"]      = "function_call",
                                ["call_id"]   = item.CallId,
                                ["name"]      = item.Name,
                                ["arguments"] = item.Arguments
                            });
                        }
                    }

                    // Execute tools
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
                        ColorLine($"[{agentName}] Tool: {call.Name}({argsPreview})", ConsoleColor.DarkYellow);

                        string result;
                        if (call.Name == "delegate")
                        {
                            string subAgent       = (string)args["agent"] ?? string.Empty;
                            string delegatedTask  = (string)args["task"]  ?? string.Empty;
                            ColorLine($"[{agentName}] Delegating to [{subAgent}]: {Truncate(delegatedTask, 80)}", ConsoleColor.Magenta);
                            result = await RunAsync(subAgent, delegatedTask, depth + 1);
                        }
                        else
                        {
                            result = await ToolExecutors.ExecuteAsync(call.Name, args);
                        }

                        string resultPreview = Truncate(result, 200);
                        ColorLine($"[{agentName}]   → {resultPreview}", ConsoleColor.DarkGray);

                        conversation.Add(new JObject
                        {
                            ["type"]    = "function_call_output",
                            ["call_id"] = call.CallId,
                            ["output"]  = result
                        });
                    }
                }

                return "Agent exceeded maximum steps";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{agentName}] Error: {ex.Message}");
                return $"Agent error: {ex.Message}";
            }
        }

        // ----------------------------------------------------------------
        // HTTP helper
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

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static string Truncate(string s, int max)
            => s != null && s.Length > max ? s.Substring(0, max) + "…" : s ?? string.Empty;

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
