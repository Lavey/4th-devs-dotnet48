using System.Collections.Generic;
using FourthDevs.ChatUi.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Agent
{
    /// <summary>
    /// Converts conversation history into the OpenAI Responses API input format.
    /// </summary>
    internal static class InputBuilder
    {
        public static JArray Build(List<ConversationMessage> messages)
        {
            var input = new JArray();

            foreach (var msg in messages)
            {
                if (msg.Role == MessageRole.user)
                {
                    input.Add(new JObject
                    {
                        ["role"] = "user",
                        ["content"] = msg.Text ?? string.Empty
                    });
                }
                else if (msg.Role == MessageRole.assistant && msg.Events != null)
                {
                    // Collect text from text_delta events
                    var sb = new System.Text.StringBuilder();
                    var toolCalls = new List<JObject>();
                    var toolResults = new List<JObject>();

                    foreach (var ev in msg.Events)
                    {
                        if (ev is TextDeltaEvent td)
                        {
                            sb.Append(td.TextDelta);
                        }
                        else if (ev is ToolCallEvent tc)
                        {
                            toolCalls.Add(new JObject
                            {
                                ["type"] = "function_call",
                                ["call_id"] = tc.ToolCallId,
                                ["name"] = tc.Name,
                                ["arguments"] = tc.Args != null ? tc.Args.ToString(Newtonsoft.Json.Formatting.None) : "{}"
                            });
                        }
                        else if (ev is ToolResultEvent tr)
                        {
                            toolResults.Add(new JObject
                            {
                                ["type"] = "function_call_output",
                                ["call_id"] = tr.ToolCallId,
                                ["output"] = tr.Output != null ? tr.Output.ToString() : ""
                            });
                        }
                    }

                    string text = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        input.Add(new JObject
                        {
                            ["role"] = "assistant",
                            ["content"] = text
                        });
                    }

                    foreach (var tc in toolCalls)
                    {
                        input.Add(tc);
                    }
                    foreach (var tr in toolResults)
                    {
                        input.Add(tr);
                    }
                }
            }

            return input;
        }
    }
}
