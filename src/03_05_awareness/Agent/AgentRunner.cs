using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Awareness.Core;
using FourthDevs.Awareness.Models;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Awareness.Agent
{
    internal static class AgentRunner
    {
        private const int MaxTurns = 20;

        public static Session CreateSession(string id, List<Message> injectedMessages)
        {
            return new Session
            {
                Id = id,
                Messages = injectedMessages ?? new List<Message>(),
                Turns = 0
            };
        }

        public static async Task<AgentResponse> RunTurnAsync(Session session, string userMessage)
        {
            string templatePath = Path.Combine(WorkspaceInit.BaseDir, "templates", "awareness.agent.md");
            AgentTemplate template = TemplateLoader.Load(templatePath);
            string resolvedModel = AiConfig.ResolveModel(template.Model ?? "gpt-4.1");

            string wrappedMessage = WrapUserMessageWithMetadata(userMessage);
            bool usedTool = false;

            JArray input = BuildInput(session.Messages, wrappedMessage);

            var tools = BuildTools(session, userMessage);
            JArray toolsArray = BuildToolsArray(tools);
            var handlers = new Dictionary<string, Func<JObject, Task<object>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var tool in tools)
                handlers[tool.Name] = tool.Handler;

            string currentResponseId = session.LastResponseId;
            string finalText = string.Empty;

            for (int turn = 0; turn < MaxTurns; turn++)
            {
                var body = new JObject
                {
                    ["model"] = resolvedModel,
                    ["store"] = true,
                    ["input"] = input
                };

                if (toolsArray.Count > 0)
                    body["tools"] = toolsArray;

                if (currentResponseId == null)
                    body["instructions"] = template.SystemPrompt;
                else
                    body["previous_response_id"] = currentResponseId;

                JObject parsed = await ApiClient.PostAsync(body);

                if (parsed["error"] != null)
                {
                    finalText = "Error: " + (parsed["error"]["message"]?.ToString() ?? "unknown");
                    break;
                }

                currentResponseId = parsed["id"]?.ToString();

                var toolCalls = new List<JObject>();
                JArray outputArray = parsed["output"] as JArray;
                if (outputArray != null)
                {
                    foreach (JToken item in outputArray)
                    {
                        if (item["type"]?.ToString() == "function_call")
                            toolCalls.Add((JObject)item);
                    }
                }

                if (toolCalls.Count == 0)
                {
                    finalText = ExtractText(parsed);
                    break;
                }

                usedTool = true;
                input = new JArray();
                foreach (JObject call in toolCalls)
                {
                    string toolName = call["name"]?.ToString();
                    string callId = call["call_id"]?.ToString();

                    JObject args;
                    try { args = JObject.Parse(call["arguments"]?.ToString() ?? "{}"); }
                    catch { args = new JObject(); }

                    ColorLine($"[awareness] Tool: {toolName}", ConsoleColor.DarkYellow);

                    string result;
                    try
                    {
                        Func<JObject, Task<object>> handler;
                        if (!handlers.TryGetValue(toolName, out handler))
                            result = JsonConvert.SerializeObject(new { error = "Unknown tool: " + toolName });
                        else
                        {
                            object resultObj = await handler(args);
                            result = resultObj is string s ? s : JsonConvert.SerializeObject(resultObj, Formatting.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        result = JsonConvert.SerializeObject(new { error = ex.Message });
                    }

                    input.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = callId,
                        ["output"] = result
                    });
                }
            }

            session.LastResponseId = currentResponseId;
            session.Turns++;

            return new AgentResponse { Text = finalText, UsedTool = usedTool };
        }

        private static JArray BuildInput(List<Message> history, string newUserMessage)
        {
            var input = new JArray();
            foreach (var msg in history)
            {
                input.Add(new JObject
                {
                    ["type"] = "message",
                    ["role"] = msg.Role,
                    ["content"] = msg.Content
                });
            }
            input.Add(new JObject
            {
                ["type"] = "message",
                ["role"] = "user",
                ["content"] = newUserMessage
            });
            return input;
        }

        private static string WrapUserMessageWithMetadata(string userMessage)
        {
            string metadata = BuildTemporalMetadata();
            return metadata + "\n\n" + userMessage;
        }

        private static string BuildTemporalMetadata()
        {
            DateTime now = DateTime.UtcNow;
            string isoNow = now.ToString("o");
            string weekday = now.DayOfWeek.ToString();
            string localTime = now.ToString("HH:mm:ss");
            return $@"<metadata>
now_iso: {isoNow}
weekday: {weekday}
local_time: {localTime}
timezone: UTC
recallable: persona, user_identity, user_preferences, important_dates, episodic_memory, factual_memory, procedural_memory
nudge: think before you respond; recall when the topic shifts; connect what you know; speak as yourself
</metadata>";
        }

        private static List<LocalToolDefinition> BuildTools(Session session, string userMessage)
        {
            return new List<LocalToolDefinition>
            {
                new LocalToolDefinition
                {
                    Name = "think",
                    Description = "Use this tool to reason through something before responding. Pass an array of questions you want to think about.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""questions"": {
                                ""type"": ""array"",
                                ""items"": { ""type"": ""string"" },
                                ""description"": ""Questions to reflect on""
                            }
                        },
                        ""required"": [""questions""]
                    }"),
                    Handler = async (args) =>
                    {
                        JArray questions = args["questions"] as JArray;
                        var questionList = new List<string>();
                        if (questions != null)
                            foreach (JToken q in questions)
                                questionList.Add(q.ToString());

                        await Task.FromResult(0);
                        return JsonConvert.SerializeObject(new
                        {
                            acknowledged = questionList,
                            next_step = "Review the questions above, then decide whether to recall context or respond directly."
                        }, Formatting.None);
                    }
                },
                new LocalToolDefinition
                {
                    Name = "recall",
                    Description = "Delegate to the scout sub-agent to retrieve information from workspace memory files.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""goal"": {
                                ""type"": ""string"",
                                ""description"": ""What specific information you need the scout to find""
                            }
                        },
                        ""required"": [""goal""]
                    }"),
                    Handler = async (args) =>
                    {
                        string goal = args["goal"]?.ToString() ?? string.Empty;
                        ColorLine($"[scout] Recalling: {goal}", ConsoleColor.Cyan);
                        string findings = await ScoutRunner.RunAsync(goal, userMessage, session.ScoutLastResponseId);
                        return findings;
                    }
                }
            };
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

        private static string ExtractText(JObject parsed)
        {
            string outputText = parsed["output_text"]?.ToString();
            if (!string.IsNullOrWhiteSpace(outputText)) return outputText;

            JArray outputArray = parsed["output"] as JArray;
            if (outputArray != null)
            {
                foreach (JToken item in outputArray)
                {
                    if (item["type"]?.ToString() == "message")
                    {
                        JArray content = item["content"] as JArray;
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

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
