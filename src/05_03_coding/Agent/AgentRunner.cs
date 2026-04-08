using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.CodingAgent.Config;
using FourthDevs.CodingAgent.Logging;
using FourthDevs.CodingAgent.Memory;
using FourthDevs.CodingAgent.Models;
using FourthDevs.CodingAgent.Tools;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.CodingAgent.Agent
{
    /// <summary>
    /// Main agent loop. Mirrors agent.ts from the TypeScript original.
    /// Calls the Responses API, processes output items, executes tool calls,
    /// and loops until no more tool calls or max turns reached.
    /// </summary>
    internal sealed class AgentRunner
    {
        private readonly ToolRegistry _tools;
        private readonly AgentLogger _logger;

        public AgentRunner(ToolRegistry tools, AgentLogger logger)
        {
            _tools = tools;
            _logger = logger;
        }

        public async Task<string> RunAsync(Session session, string userMessage)
        {
            session.AddUserMessage(userMessage);
            _logger.Event("user.message", new JObject { ["chars"] = userMessage.Length });

            for (int turn = 1; turn <= AgentConfig.MaxTurns; turn++)
            {
                await MemoryManager.MaybeCompactMemoryAsync(session, _logger);

                _logger.Info("agent", string.Format("Turn {0}", turn));
                _logger.Event("turn.start", new JObject
                {
                    ["turn"] = turn,
                    ["messageCount"] = session.Messages.Count,
                    ["summaryChars"] = session.Summary.Length
                });

                // Build the input array
                var inputArray = BuildInputArray(session.Messages);

                string instructions = MemoryManager.BuildInstructions(
                    AgentConfig.SystemPrompt, session.Summary);

                string model = AiConfig.ResolveModel(AgentConfig.Model);

                var body = new JObject
                {
                    ["model"] = model,
                    ["instructions"] = instructions,
                    ["input"] = inputArray,
                    ["parallel_tool_calls"] = false,
                    ["reasoning"] = new JObject { ["effort"] = AgentConfig.ReasoningEffort },
                    ["store"] = false
                };

                var toolDefs = _tools.GetToolDefinitions();
                if (toolDefs.Count > 0)
                    body["tools"] = toolDefs;

                string responseJson = await PostAsync(body.ToString(Formatting.None));
                var parsed = JObject.Parse(responseJson);

                // Check for API error
                var errorToken = parsed["error"];
                if (errorToken != null && errorToken.Type != JTokenType.Null)
                {
                    string errorMsg = (string)errorToken["message"] ?? errorToken.ToString();
                    throw new InvalidOperationException("API error: " + errorMsg);
                }

                // Read usage
                LogUsage(parsed, turn);

                // Process output items
                var assistantTexts = new List<string>();
                var pendingToolCalls = new List<JObject>();

                var output = parsed["output"] as JArray;
                if (output != null)
                {
                    foreach (var item in output)
                    {
                        string itemType = (string)item["type"];

                        if (itemType == "message")
                        {
                            string text = ExtractMessageText(item);
                            if (string.IsNullOrEmpty(text))
                                continue;

                            session.AddAssistantMessage(text);
                            assistantTexts.Add(text);
                            _logger.Info("assistant", Truncate(text, 140));
                        }
                        else if (itemType == "function_call")
                        {
                            string callId = (string)item["call_id"];
                            string name = (string)item["name"];
                            string arguments = (string)item["arguments"];

                            session.AddToolCall(callId, name, arguments);
                            pendingToolCalls.Add(new JObject
                            {
                                ["call_id"] = callId,
                                ["name"] = name,
                                ["arguments"] = arguments
                            });
                        }
                    }
                }

                // No tool calls → agent is done
                if (pendingToolCalls.Count == 0)
                {
                    string finalText;
                    if (assistantTexts.Count > 0)
                    {
                        finalText = string.Join("\n\n", assistantTexts.ToArray());
                    }
                    else
                    {
                        finalText = (string)parsed["output_text"] ?? "Done.";
                    }
                    _logger.Event("turn.done", new JObject { ["turn"] = turn, ["completed"] = true });
                    return finalText;
                }

                // Execute tool calls
                foreach (var call in pendingToolCalls)
                {
                    string result = RunToolCall(
                        (string)call["call_id"],
                        (string)call["name"],
                        (string)call["arguments"]);

                    session.AddToolResult((string)call["call_id"], result);
                }
            }

            _logger.Event("turn.done", new JObject { ["completed"] = false, ["reason"] = "max_turns" });
            return "Stopped after reaching the maximum number of turns.";
        }

        private string RunToolCall(string callId, string name, string arguments)
        {
            JObject args;
            try
            {
                args = JObject.Parse(arguments ?? "{}");
            }
            catch
            {
                return "Error: invalid JSON arguments";
            }

            _logger.Info("tool", string.Format("{0}({1})", name, Truncate(args.ToString(Formatting.None), 140)));
            _logger.Event("tool.call", new JObject { ["name"] = name, ["args"] = args });

            try
            {
                string output = _tools.Execute(name, args);
                _logger.Event("tool.result", new JObject
                {
                    ["name"] = name,
                    ["outputPreview"] = Truncate(output, 300)
                });
                return output;
            }
            catch (Exception ex)
            {
                _logger.Error("tool", ex, name + " failed");
                return "Error: " + ex.Message;
            }
        }

        private static JArray BuildInputArray(List<ConversationItem> messages)
        {
            var arr = new JArray();
            foreach (var msg in messages)
            {
                var textMsg = msg as TextMessage;
                if (textMsg != null)
                {
                    arr.Add(new JObject
                    {
                        ["role"] = textMsg.Role,
                        ["content"] = textMsg.Content
                    });
                    continue;
                }

                var funcCall = msg as FunctionCallItem;
                if (funcCall != null)
                {
                    arr.Add(new JObject
                    {
                        ["type"] = "function_call",
                        ["call_id"] = funcCall.CallId,
                        ["name"] = funcCall.Name,
                        ["arguments"] = funcCall.Arguments
                    });
                    continue;
                }

                var funcOutput = msg as FunctionCallOutputItem;
                if (funcOutput != null)
                {
                    arr.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = funcOutput.CallId,
                        ["output"] = funcOutput.Output
                    });
                }
            }
            return arr;
        }

        private static string ExtractMessageText(JToken item)
        {
            var content = item["content"] as JArray;
            if (content == null) return null;

            var parts = new List<string>();
            foreach (var part in content)
            {
                if ((string)part["type"] == "output_text" && part["text"] != null)
                    parts.Add((string)part["text"]);
            }
            return parts.Count > 0 ? string.Join("", parts.ToArray()) : null;
        }

        private void LogUsage(JObject response, int turn)
        {
            var usage = response["usage"];
            string inputTokens = usage != null ? ((string)usage["input_tokens"] ?? "?") : "?";
            string outputTokens = usage != null ? ((string)usage["output_tokens"] ?? "?") : "?";
            string totalTokens = usage != null ? ((string)usage["total_tokens"] ?? "?") : "?";

            _logger.Info("agent",
                string.Format("Tokens in={0} out={1} total={2}", inputTokens, outputTokens, totalTokens));
            _logger.Event("model.response", new JObject
            {
                ["turn"] = turn,
                ["inputTokens"] = usage != null ? usage["input_tokens"] : null,
                ["outputTokens"] = usage != null ? usage["output_tokens"] : null,
                ["totalTokens"] = usage != null ? usage["total_tokens"] : null
            });
        }

        private static string Truncate(string value, int max)
        {
            if (value == null) return string.Empty;
            return value.Length > max ? value.Substring(0, max - 1) + "..." : value;
        }

        private static async Task<string> PostAsync(string jsonBody)
        {
            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromMinutes(5);
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
                        // Try to extract error message from body
                        try
                        {
                            var errObj = JObject.Parse(body);
                            var errMsg = errObj["error"]?["message"];
                            if (errMsg != null)
                                throw new InvalidOperationException("API error: " + (string)errMsg);
                        }
                        catch (JsonReaderException) { }

                        throw new HttpRequestException(
                            string.Format("Request failed with status {0}: {1}",
                                (int)response.StatusCode, body));
                    }
                    return body;
                }
            }
        }
    }
}
