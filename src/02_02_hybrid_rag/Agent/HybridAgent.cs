using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using FourthDevs.Lesson07_HybridRag.Db;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson07_HybridRag.Agent
{
    /// <summary>
    /// Agent loop result.
    /// </summary>
    internal sealed class AgentResult
    {
        public string       Response            { get; set; }
        public List<object> ConversationHistory { get; set; }
    }

    /// <summary>
    /// Runs the Hybrid RAG agent: sends messages to the model, executes the
    /// hybrid-search tool, and iterates until the model produces a final text response.
    ///
    /// Mirrors 02_02_hybrid_rag/src/agent/index.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class HybridAgent
    {
        // ----------------------------------------------------------------
        // Tool definition
        // ----------------------------------------------------------------

        private static readonly object SearchToolDefinition = new
        {
            type        = "function",
            name        = "search",
            description =
                "Search the indexed knowledge base using hybrid search " +
                "(full-text BM25 + semantic vector similarity). " +
                "Returns the most relevant document chunks with content, source file, " +
                "and section heading. " +
                "Provide BOTH a keyword query for full-text search AND a natural language " +
                "query for semantic search.",
            parameters  = new
            {
                type       = "object",
                properties = new
                {
                    keywords = new
                    {
                        type        = "string",
                        description = "Keywords for full-text search (BM25) — " +
                                      "specific terms, names, and phrases"
                    },
                    semantic = new
                    {
                        type        = "string",
                        description = "Natural language query for semantic/vector search"
                    },
                    limit = new
                    {
                        type        = "number",
                        description = "Maximum number of results to return (default 5, max 20)"
                    }
                },
                required = new[] { "keywords", "semantic" }
            },
            strict = false
        };

        // ----------------------------------------------------------------
        // Agent loop
        // ----------------------------------------------------------------

        internal static async Task<AgentResult> RunAsync(
            string query, List<object> conversationHistory, SQLiteConnection db)
        {
            var tools    = new[] { SearchToolDefinition };
            var messages = new List<object>(conversationHistory);
            messages.Add(new { type = "message", role = "user", content = query });

            ColorLine("  [query] " + query, ConsoleColor.Cyan);

            for (int step = 1; step <= AgentConfig.MaxSteps; step++)
            {
                ColorLine(
                    string.Format("  [api] step {0}, messages: {1}",
                        step, messages.Count),
                    ConsoleColor.DarkGray);

                var body = new JObject
                {
                    ["model"]        = AiConfig.ResolveModel(AgentConfig.Model),
                    ["input"]        = JArray.FromObject(messages),
                    ["tools"]        = JArray.FromObject(tools),
                    ["instructions"] = AgentConfig.Instructions
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);

                if (parsed?.Error != null)
                    throw new InvalidOperationException(parsed.Error.Message);

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

                // Append function_call items to conversation
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

                // Execute tool calls
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
