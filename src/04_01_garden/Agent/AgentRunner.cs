using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Garden.Core;
using FourthDevs.Garden.Models;
using FourthDevs.Garden.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Garden.Agent
{
    /// <summary>
    /// Main agent loop: load template, resolve skills, call API, execute tools.
    /// Port of 04_01_garden/src/agent/loop.ts.
    /// </summary>
    internal static class AgentRunner
    {
        private const int MaxTurns = 20;

        public static async Task<AgentResult> RunAsync(string userMessage, string agent = "main")
        {
            AgentTemplate template = TemplateLoader.LoadTemplate(agent);
            SkillResolver.ResolvedSkillContext skillCtx = SkillResolver.Resolve(
                userMessage, template.Skills, template.Tools);

            JArray tools = ToolRegistry.Definitions(skillCtx.ToolNames);

            int totalTokens = 0;
            var input = new JArray
            {
                new JObject { ["role"] = "user", ["content"] = skillCtx.UserMessage }
            };
            string previousResponseId = null;

            for (int turn = 0; turn < MaxTurns; turn++)
            {
                ToolLog.LogTurn(turn + 1);

                JObject response = await ApiClient.CompletionAsync(
                    template.Model,
                    template.Instructions,
                    input,
                    tools,
                    previousResponseId);

                // Check for API error
                JToken errorToken = response["error"];
                if (errorToken != null && errorToken.Type != JTokenType.Null)
                {
                    string errMsg = (string)errorToken["message"] ?? "Unknown API error";
                    return new AgentResult
                    {
                        Text = "API error: " + errMsg,
                        Turns = turn + 1,
                        TotalTokens = totalTokens
                    };
                }

                // Accumulate token usage
                JToken usage = response["usage"];
                if (usage != null)
                {
                    int inputTokens = (int)(usage["input_tokens"] ?? 0);
                    int outputTokens = (int)(usage["output_tokens"] ?? 0);
                    totalTokens += inputTokens + outputTokens;
                }

                previousResponseId = (string)response["id"];

                // Log any built-in tool calls (web_search_call)
                JArray output = response["output"] as JArray;
                if (output != null)
                    LogBuiltinTools(output);

                // Collect function_call items
                var toolCalls = new List<JToken>();
                if (output != null)
                {
                    foreach (JToken item in output)
                    {
                        if ((string)item["type"] == "function_call")
                            toolCalls.Add(item);
                    }
                }

                // If no tool calls, return text response
                if (toolCalls.Count == 0)
                {
                    string text = ExtractOutputText(response);
                    return new AgentResult
                    {
                        Text = text,
                        Turns = turn + 1,
                        TotalTokens = totalTokens
                    };
                }

                // Execute tool calls and build next input
                input = new JArray();
                foreach (JToken call in toolCalls)
                {
                    string callId = (string)call["call_id"];
                    string name = (string)call["name"];
                    string arguments = (string)call["arguments"];

                    JObject args;
                    try { args = JObject.Parse(arguments ?? "{}"); }
                    catch { args = new JObject(); }

                    string argsPreview = args.ToString(Formatting.None);
                    ToolLog.LogToolCall(name, argsPreview);

                    ToolExecutionResult result;
                    try
                    {
                        result = await ToolRegistry.ExecuteAsync(name, args);
                    }
                    catch (Exception ex)
                    {
                        result = new ToolExecutionResult(false, "Error: " + ex.Message);
                    }

                    ToolLog.LogToolResult(name, result.Output, result.Ok);

                    input.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = callId,
                        ["output"] = result.Output
                    });
                }
            }

            return new AgentResult
            {
                Text = "Max turns reached",
                Turns = MaxTurns,
                TotalTokens = totalTokens
            };
        }

        private static string ExtractOutputText(JObject response)
        {
            // Try output_text shorthand first
            string outputText = (string)response["output_text"];
            if (!string.IsNullOrEmpty(outputText))
                return outputText;

            // Search output items for message with output_text content
            JArray output = response["output"] as JArray;
            if (output != null)
            {
                foreach (JToken item in output)
                {
                    if ((string)item["type"] == "message")
                    {
                        JArray content = item["content"] as JArray;
                        if (content != null)
                        {
                            foreach (JToken part in content)
                            {
                                if ((string)part["type"] == "output_text")
                                {
                                    string text = (string)part["text"];
                                    if (!string.IsNullOrEmpty(text))
                                        return text;
                                }
                            }
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static void LogBuiltinTools(JArray output)
        {
            foreach (JToken item in output)
            {
                string type = (string)item["type"];
                if (type == "web_search_call")
                {
                    JToken action = item["action"];
                    if (action != null)
                    {
                        string actionType = (string)action["type"];
                        if (actionType == "search")
                        {
                            JArray queries = action["queries"] as JArray;
                            if (queries != null)
                            {
                                var qList = new List<string>();
                                foreach (JToken q in queries)
                                    qList.Add("\"" + (string)q + "\"");
                                ToolLog.LogBuiltinTool("web_search", string.Join(", ", qList));
                            }
                        }
                        else if (actionType == "open_page")
                        {
                            ToolLog.LogBuiltinTool("web_search", (string)action["url"]);
                        }
                    }
                }
            }
        }
    }
}
