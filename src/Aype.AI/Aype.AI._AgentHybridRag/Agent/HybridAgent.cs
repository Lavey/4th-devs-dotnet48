using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using Aype.AI.AgentHybridRag.Db;
using Aype.AI.ApiClient;
using Aype.AI.Common;
using Aype.AI.Common.Models;
using Aype.AI.Common.Models.Enums;
using Aype.AI.Tools.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aype.AI.AgentHybridRag.Agent
{
    /// <summary>
    /// Agent loop result.
    /// </summary>
    internal sealed class AgentResult
    {
        public string            Response            { get; set; }
        public List<InputMessage> ConversationHistory { get; set; }
    }

    /// <summary>
    /// Runs the Hybrid RAG agent: sends messages to the model, executes the
    /// hybrid-search tool, and iterates until the model produces a final text response.
    /// </summary>
    internal static class HybridAgent
    {
        internal static async Task<AgentResult> RunAsync(
            string query, List<InputMessage> conversationHistory, SQLiteConnection db)
        {
            var tools    = new List<ToolDefinition> { SearchTool.Build() };
            var messages = new List<InputMessage>(conversationHistory);
            messages.Add(new UserMessage(query));

            ColorLine("  [query] " + query, ConsoleColor.Cyan);

            using (var client = new ResponsesApiClient())
            {
                for (int step = 1; step <= AgentConfig.MaxSteps; step++)
                {
                    ColorLine(
                        string.Format("  [api] step {0}, messages: {1}", step, messages.Count),
                        ConsoleColor.DarkGray);

                    var request = new ResponsesRequest
                    {
                        Model        = AiConfig.ResolveModel(AgentConfig.Model),
                        Input        = messages,
                        Tools        = tools,
                        Instructions = AgentConfig.Instructions
                    };

                    ResponsesResponse parsed = await client.SendAsync(request);

                    if (parsed.Usage != null)
                        ColorLine(
                            string.Format("  [usage] in:{0} out:{1}",
                                parsed.Usage.InputTokens, parsed.Usage.OutputTokens),
                            ConsoleColor.DarkGray);

                    var toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                    if (toolCalls.Count == 0)
                    {
                        string text = ResponsesApiClient.ExtractText(parsed);

                        foreach (var item in parsed.Output)
                        {
                            if (item.Type == OutputItemType.Message)
                                messages.Add(new AssistantMessage(text));
                        }

                        return new AgentResult
                        {
                            Response            = text ?? string.Empty,
                            ConversationHistory = messages
                        };
                    }

                    // Append function_call items to conversation
                    foreach (var item in parsed.Output)
                    {
                        if (item.Type == OutputItemType.FunctionCall)
                            messages.Add(new FunctionCallMessage(
                                item.CallId, item.Name, item.Arguments));
                    }

                    // Execute tool calls and append results
                    foreach (var call in toolCalls)
                    {
                        var    callArgs   = JObject.Parse(call.Arguments ?? "{}");
                        string resultJson = await ExecuteToolAsync(call.Name, callArgs, db);

                        string preview = resultJson.Length > 120
                            ? resultJson.Substring(0, 120) + "..."
                            : resultJson;
                        ColorLine(
                            string.Format("  [tool] {0} → {1}", call.Name, preview),
                            ConsoleColor.DarkCyan);

                        messages.Add(new ToolMessage(call.CallId, resultJson));
                    }
                }
            }

            throw new InvalidOperationException(
                string.Format("Agent loop did not finish within {0} steps.",
                    AgentConfig.MaxSteps));
        }

        // ----------------------------------------------------------------
        // Tool execution
        // ----------------------------------------------------------------

        private static async Task<string> ExecuteToolAsync(
            string name, JObject args, SQLiteConnection db)
        {
            if (name != "search")
                return JsonConvert.SerializeObject(new { error = "Unknown tool: " + name });

            string keywords = args["keywords"]?.Value<string>() ?? string.Empty;
            string semantic = args["semantic"]?.Value<string>() ?? string.Empty;
            int    limit    = args["limit"]?.Value<int>() ?? 5;
            limit = Math.Min(limit, 20);

            var results = await Search.HybridSearchAsync(db, keywords, semantic, limit);

            var mapped = new List<object>();
            foreach (var r in results)
                mapped.Add(new
                {
                    source  = r.Source,
                    section = r.Section,
                    content = r.Content
                });

            return JsonConvert.SerializeObject(mapped);
        }

        // ----------------------------------------------------------------
        // Console helper
        // ----------------------------------------------------------------

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
