using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Language.Core;
using FourthDevs.Language.Hooks;
using FourthDevs.Language.Prompts;
using FourthDevs.Language.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Language.Agent
{
    public static class AgentRunner
    {
        private const int MaxTurns = 15;

        public static async Task<AgentRunResult> RunAsync(
            string userMessage,
            string workspaceDir,
            string previousResponseId = null)
        {
            string currentDate = DateTime.Now.ToString("yyyy-MM-dd");
            string sessionId = $"{currentDate}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            List<string> recentSessions = AgentPrompts.ListRecentSessions(workspaceDir, 3);
            string systemPrompt = AgentPrompts.BuildSystemPrompt(currentDate, sessionId, recentSessions);

            List<LocalToolDef> toolsList = AgentTools.CreateTools(workspaceDir);
            var hooks = new AgentHooksManager(currentDate, sessionId);

            List<GeminiFunctionToolDef> toolDefs = toolsList.Select(t => new GeminiFunctionToolDef
            {
                Type = "function",
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters
            }).ToList();

            string responseId = previousResponseId;
            object input = new object[] { new GeminiTextInput { Text = userMessage } };
            string finalText = string.Empty;

            string model = ConfigurationManager.AppSettings["GEMINI_MODEL"]?.Trim();
            if (string.IsNullOrEmpty(model)) model = "gemini-3-flash-preview";

            for (int turn = 1; turn <= MaxTurns; turn++)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[turn {turn}/{MaxTurns}]");
                Console.ResetColor();

                var request = new GeminiInteractionRequest
                {
                    Model = model,
                    Input = input,
                    SystemInstruction = systemPrompt,
                    Tools = toolDefs,
                    GenerationConfig = new Dictionary<string, object>
                    {
                        ["temperature"] = 0.3,
                        ["thinking_level"] = "low"
                    },
                    PreviousInteractionId = responseId
                };

                GeminiInteraction interaction;
                try
                {
                    interaction = await GeminiClient.CallInteractionAsync(request);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[gemini] {ex.Message}");
                    break;
                }

                responseId = interaction.Id;
                List<JObject> calls = GeminiClient.ExtractFunctionCalls(interaction.Outputs);
                string text = GeminiClient.ExtractText(interaction.Outputs);

                if (calls.Count == 0)
                {
                    var (allow, injectMessage) = hooks.BeforeFinish(text);
                    if (!allow && turn < MaxTurns)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("[hook:beforeFinish] missing steps, injecting message");
                        Console.ResetColor();
                        input = new object[] { new GeminiTextInput { Text = injectMessage ?? "Complete remaining actions." } };
                        continue;
                    }
                    finalText = text.Length > 0 ? text : hooks.BuildFallbackTextFeedback();
                    break;
                }

                var results = new List<GeminiFunctionResult>();
                foreach (JObject call in calls)
                {
                    string callId = call["id"]?.Value<string>();
                    string toolName = call["name"]?.Value<string>();
                    JObject toolArgs = call["arguments"] as JObject ?? new JObject();

                    hooks.BeforeToolCall(toolName, toolArgs);

                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write($"  [{toolName}]");
                    Console.ResetColor();

                    LocalToolDef tool = toolsList.FirstOrDefault(t => t.Name == toolName);
                    string output;
                    if (tool == null)
                    {
                        output = $"Unknown tool: {toolName}";
                    }
                    else
                    {
                        try
                        {
                            output = await tool.Handler(toolArgs);
                        }
                        catch (Exception ex)
                        {
                            output = JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }

                    string processedOutput = hooks.AfterToolResult(toolName, toolArgs, output) ?? output;

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    string preview = processedOutput.Length > 80
                        ? processedOutput.Substring(0, 80) + "..."
                        : processedOutput;
                    Console.WriteLine($" -> {preview}");
                    Console.ResetColor();

                    results.Add(new GeminiFunctionResult
                    {
                        CallId = callId,
                        Name = toolName,
                        Result = processedOutput
                    });
                }

                input = results.ToArray();
            }

            if (string.IsNullOrEmpty(finalText))
                finalText = hooks.BuildFallbackTextFeedback();

            return new AgentRunResult { Text = finalText, ResponseId = responseId };
        }
    }

    public class AgentRunResult
    {
        public string Text { get; set; }
        public string ResponseId { get; set; }
    }
}
