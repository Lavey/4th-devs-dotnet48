using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using FourthDevs.Lesson06_AgenticRag.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson06_AgenticRag
{
    /// <summary>
    /// Result returned by the agent after processing a query.
    /// </summary>
    internal class AgentResult
    {
        public string       Response            { get; set; }
        public List<object> ConversationHistory { get; set; }
    }

    /// <summary>
    /// Runs the Agentic RAG loop: sends messages to the model, executes file-tool
    /// calls, and iterates until the model produces a final text response.
    /// Maintains conversation history for multi-turn follow-up questions.
    ///
    /// Mirrors 02_01_agentic_rag/src/agent.js in the source repo.
    /// </summary>
    internal static class Agent
    {
        // ----------------------------------------------------------------
        // Main agent loop
        // ----------------------------------------------------------------

        /// <summary>
        /// Runs the agent with a user query, optionally continuing an existing
        /// conversation.
        /// </summary>
        /// <param name="query">The user's question.</param>
        /// <param name="conversationHistory">
        /// Previous messages for follow-up questions. Pass an empty list for a
        /// fresh conversation.
        /// </param>
        internal static async Task<AgentResult> RunAsync(
            string query, List<object> conversationHistory)
        {
            var tools    = ToolDefinitions.Build();
            var messages = new List<object>(conversationHistory);
            messages.Add(new { type = "message", role = "user", content = query });

            ColorLine(string.Format("  [query] {0}", query), ConsoleColor.Cyan);

            for (int step = 1; step <= AgentConfig.MaxSteps; step++)
            {
                ColorLine(string.Format("  [api] step {0}, messages: {1}",
                    step, messages.Count), ConsoleColor.DarkGray);

                var body = new JObject
                {
                    ["model"]        = AiConfig.ResolveModel(AgentConfig.Model),
                    ["input"]        = JArray.FromObject(messages),
                    ["tools"]        = JArray.FromObject(tools),
                    ["instructions"] = AgentConfig.Instructions
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                var    parsed       = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);

                if (parsed?.Error != null)
                    throw new InvalidOperationException(parsed.Error.Message);

                if (parsed.Usage != null)
                {
                    ColorLine(string.Format(
                        "  [usage] in:{0} out:{1}",
                        parsed.Usage.InputTokens,
                        parsed.Usage.OutputTokens),
                        ConsoleColor.DarkGray);
                }

                var toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                if (toolCalls.Count == 0)
                {
                    string text = ResponsesApiClient.ExtractText(parsed);

                    // Append assistant reply to history
                    foreach (var item in parsed.Output)
                    {
                        if (item.Type == "message")
                            messages.Add(new
                            {
                                type    = "message",
                                role    = "assistant",
                                content = text
                            });
                    }

                    return new AgentResult
                    {
                        Response            = text ?? string.Empty,
                        ConversationHistory = messages
                    };
                }

                // Append function_call items to the conversation
                foreach (var item in parsed.Output)
                {
                    if (item.Type == "function_call")
                        messages.Add(new
                        {
                            type      = "function_call",
                            call_id   = item.CallId,
                            name      = item.Name,
                            arguments = item.Arguments
                        });
                }

                // Execute each tool call and append results
                foreach (var call in toolCalls)
                {
                    var    callArgs   = JObject.Parse(call.Arguments ?? "{}");
                    object result     = ExecuteTool(call.Name, callArgs);
                    string resultJson = JsonConvert.SerializeObject(result);

                    string preview = resultJson.Length > 120
                        ? resultJson.Substring(0, 120) + "..."
                        : resultJson;

                    ColorLine(string.Format("  [tool] {0} → {1}", call.Name, preview),
                        ConsoleColor.DarkCyan);

                    messages.Add(new
                    {
                        type    = "function_call_output",
                        call_id = call.CallId,
                        output  = resultJson
                    });
                }
            }

            throw new InvalidOperationException(
                string.Format("Agent loop did not finish within {0} steps.",
                    AgentConfig.MaxSteps));
        }

        // ----------------------------------------------------------------
        // Tool dispatcher
        // ----------------------------------------------------------------

        private static object ExecuteTool(string name, JObject args)
        {
            switch (name)
            {
                case "list_files":   return ToolExecutors.ExecuteListFiles(args);
                case "search_files": return ToolExecutors.ExecuteSearchFiles(args);
                case "read_file":    return ToolExecutors.ExecuteReadFile(args);
                default:
                    return new { error = "Unknown tool: " + name };
            }
        }

        // ----------------------------------------------------------------
        // HTTP helper — calls the OpenAI / OpenRouter Responses API
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

                using (var content = new StringContent(
                    jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
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
