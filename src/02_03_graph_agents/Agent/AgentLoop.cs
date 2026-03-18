using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Neo4j.Driver;
using FourthDevs.Common;
using FourthDevs.Lesson08_GraphAgents.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson08_GraphAgents.Agent
{
    /// <summary>
    /// Agent loop result.
    /// </summary>
    internal sealed class AgentResult
    {
        internal string       Response            { get; set; }
        internal List<object> ConversationHistory { get; set; }
    }

    /// <summary>
    /// Runs the graph RAG agent: sends messages to the model, executes tool calls,
    /// and iterates until the model produces a final text response.
    /// Mirrors 02_03_graph_agents/src/agent/index.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class AgentLoop
    {
        internal static async Task<AgentResult> RunAsync(
            string query,
            List<object> conversationHistory,
            IDriver driver)
        {
            var messages = new List<object>(conversationHistory);
            messages.Add(new { type = "message", role = "user", content = query });

            Logger.Query(query);

            for (int step = 1; step <= AgentConfig.MaxSteps; step++)
            {
                Logger.ApiStep(step, messages.Count);

                var body = new JObject
                {
                    ["model"]             = AiConfig.ResolveModel(AgentConfig.Model),
                    ["input"]             = JArray.FromObject(messages),
                    ["tools"]             = JArray.FromObject(ToolDefinitions.All),
                    ["tool_choice"]       = "auto",
                    ["instructions"]      = AgentConfig.Instructions,
                    ["max_output_tokens"] = AgentConfig.MaxOutputTokens,
                    ["reasoning"]         = new JObject
                    {
                        ["effort"]  = "medium",
                        ["summary"] = "auto"
                    }
                };

                string responseJson = await PostAsync(body.ToString(Formatting.None));
                var parsed = JObject.Parse(responseJson);

                if (parsed["error"] != null)
                    throw new InvalidOperationException(
                        parsed["error"]["message"]?.Value<string>() ?? "API error");

                Logger.ApiDone(parsed["usage"]);
                Stats.RecordUsage(parsed["usage"]);

                // Find tool calls in output
                var output    = parsed["output"] as JArray ?? new JArray();
                var toolCalls = new List<JObject>();

                foreach (var item in output)
                {
                    if (item["type"]?.Value<string>() == "function_call")
                        toolCalls.Add((JObject)item);
                }

                if (toolCalls.Count == 0)
                {
                    // Final response
                    string text = ExtractText(parsed) ?? "No response";
                    Logger.Response(text);

                    // Append assistant message to history
                    foreach (var item in output)
                    {
                        if (item["type"]?.Value<string>() == "message")
                            messages.Add(new
                            {
                                type    = "message",
                                role    = "assistant",
                                content = text
                            });
                    }

                    return new AgentResult
                    {
                        Response            = text,
                        ConversationHistory = messages
                    };
                }

                // Append function_call items to conversation
                foreach (var item in output)
                {
                    if (item["type"]?.Value<string>() == "function_call")
                        messages.Add(new
                        {
                            type      = "function_call",
                            call_id   = item["call_id"]?.Value<string>(),
                            name      = item["name"]?.Value<string>(),
                            arguments = item["arguments"]?.Value<string>()
                        });
                }

                // Execute tool calls in parallel
                var resultTasks = new List<Task<(string callId, string output2)>>();
                foreach (var call in toolCalls)
                {
                    var callId   = call["call_id"]?.Value<string>() ?? string.Empty;
                    var toolName = call["name"]?.Value<string>() ?? string.Empty;
                    var argsStr  = call["arguments"]?.Value<string>() ?? "{}";
                    JObject toolArgs;
                    try { toolArgs = JObject.Parse(argsStr); }
                    catch { toolArgs = new JObject(); }

                    var capturedId   = callId;
                    var capturedName = toolName;
                    var capturedArgs = toolArgs;
                    resultTasks.Add(Task.Run(async () =>
                    {
                        string result = await ToolExecutors.ExecuteAsync(driver, capturedName, capturedArgs);
                        return (capturedId, result);
                    }));
                }

                var results = await Task.WhenAll(resultTasks);
                foreach (var (callId, toolOutput) in results)
                {
                    messages.Add(new
                    {
                        type    = "function_call_output",
                        call_id = callId,
                        output  = toolOutput
                    });
                }
            }

            throw new InvalidOperationException(
                string.Format("Max steps ({0}) reached", AgentConfig.MaxSteps));
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static string ExtractText(JObject response)
        {
            var output = response["output"] as JArray;
            if (output == null) return null;

            foreach (var item in output)
            {
                if (item["type"]?.Value<string>() == "message")
                {
                    var content = item["content"] as JArray;
                    if (content != null)
                        foreach (var part in content)
                            if (part["type"]?.Value<string>() == "output_text")
                                return part["text"]?.Value<string>();

                    // Fallback: content as string
                    if (item["content"]?.Type == JTokenType.String)
                        return item["content"].Value<string>();
                }
            }

            return null;
        }

        private static async Task<string> PostAsync(string jsonBody)
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
    }
}
