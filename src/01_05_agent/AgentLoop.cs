using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using FourthDevs.Lesson05_Agent.Db;
using FourthDevs.Lesson05_Agent.Events;
using FourthDevs.Lesson05_Agent.Mcp;
using FourthDevs.Lesson05_Agent.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson05_Agent
{
    /// <summary>
    /// Runs the agentic loop: sends messages to the model, executes tool calls,
    /// and repeats until the model produces a final text response.
    ///
    /// Mirrors 01_05_agent/src/domain/ and the agent execution logic
    /// from the source repo.
    /// </summary>
    internal static class AgentRunner
    {
        private const int MaxToolOutputLogLength = 1000;

        // Maximum number of turns is read from config in Program and
        // injected here before first use.
        internal static int AgentMaxTurns { get; set; } = 10;

        // Workspace root needed to load agent templates.
        internal static string WorkspaceRoot { get; set; }

        // MCP manager shared across all agent loops.
        internal static McpClientManager McpManager { get; set; }

        // Event emitter (optional — when null, no events are emitted).
        internal static AgentEventEmitter Events { get; set; }

        // Database (optional — when null, items are not persisted).
        internal static AgentDb Database { get; set; }

        // ----------------------------------------------------------------
        // Main agent loop
        // ----------------------------------------------------------------

        internal static async Task<string> RunAgentLoopAsync(
            List<object> conversation, string model,
            string traceId    = null,
            string sessionId  = null,
            string agentId    = null,
            string agentName  = null,
            int    depth      = 0)
        {
            if (traceId   == null) traceId   = Guid.NewGuid().ToString();
            if (sessionId == null) sessionId = string.Empty;
            if (agentId   == null) agentId   = Guid.NewGuid().ToString();

            var tools = AgentToolDefinitions.Build(McpManager);
            var sw    = Stopwatch.StartNew();

            // ── agent.started ────────────────────────────────────────
            EmitEvent(new AgentStartedEvent
            {
                Ctx       = MakeCtx(traceId, sessionId, agentId, depth),
                Model     = model,
                Task      = string.Empty,
                AgentName = agentName
            });

            try
            {
                for (int step = 0; step < AgentMaxTurns; step++)
                {
                    int turnNumber = step + 1;

                    // ── turn.started ─────────────────────────────────
                    EmitEvent(new TurnStartedEvent
                    {
                        Ctx       = MakeCtx(traceId, sessionId, agentId, depth),
                        TurnCount = turnNumber
                    });

                    var body = new JObject
                    {
                        ["model"] = model,
                        ["input"] = JArray.FromObject(conversation),
                        ["tools"] = JArray.FromObject(tools)
                    };

                    var genSw = Stopwatch.StartNew();
                    string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                    genSw.Stop();

                    var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);

                    if (parsed?.Error != null)
                        throw new InvalidOperationException(parsed.Error.Message);

                    // ── generation.completed ─────────────────────────
                    TokenUsage genUsage = MapUsage(parsed);
                    EmitEvent(new GenerationCompletedEvent
                    {
                        Ctx        = MakeCtx(traceId, sessionId, agentId, depth),
                        Model      = model,
                        DurationMs = genSw.ElapsedMilliseconds,
                        Usage      = genUsage
                    });

                    var toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                    if (toolCalls.Count == 0)
                    {
                        string text = ResponsesApiClient.ExtractText(parsed);
                        foreach (var item in parsed.Output)
                        {
                            if (item.Type == "message")
                                conversation.Add(new { type = "message", role = "assistant", content = text });
                        }

                        PersistItem(agentId, "message", role: "assistant", content: text);

                        // ── turn.completed ───────────────────────────
                        EmitEvent(new TurnCompletedEvent
                        {
                            Ctx       = MakeCtx(traceId, sessionId, agentId, depth),
                            TurnCount = turnNumber,
                            Usage     = genUsage
                        });

                        // ── agent.completed ──────────────────────────
                        sw.Stop();
                        EmitEvent(new AgentCompletedEvent
                        {
                            Ctx        = MakeCtx(traceId, sessionId, agentId, depth),
                            DurationMs = sw.ElapsedMilliseconds,
                            Usage      = genUsage,
                            Result     = text
                        });

                        return text;
                    }

                    // Append function_call items
                    foreach (var item in parsed.Output)
                    {
                        if (item.Type == "function_call")
                        {
                            conversation.Add(new
                            {
                                type      = "function_call",
                                call_id   = item.CallId,
                                name      = item.Name,
                                arguments = item.Arguments
                            });

                            PersistItem(agentId, "function_call",
                                callId: item.CallId, name: item.Name, arguments: item.Arguments);
                        }
                    }

                    // Execute tools
                    foreach (var call in toolCalls)
                    {
                        var callArgs = JObject.Parse(call.Arguments ?? "{}");

                        // ask_user: pause the agent and wait for human input
                        if (call.Name == "ask_user")
                        {
                            string question = callArgs["question"]?.ToString() ?? string.Empty;

                            // ── agent.waiting ────────────────────────
                            EmitEvent(new AgentWaitingEvent
                            {
                                Ctx        = MakeCtx(traceId, sessionId, agentId, depth),
                                WaitingFor = new List<WaitingForEntry>
                                {
                                    new WaitingForEntry { CallId = call.CallId, Type = "human", Question = question }
                                }
                            });

                            throw new WaitingForHumanException(call.CallId, question);
                        }

                        // ── tool.called ──────────────────────────────
                        EmitEvent(new ToolCalledEvent
                        {
                            Ctx       = MakeCtx(traceId, sessionId, agentId, depth),
                            CallId    = call.CallId,
                            Name      = call.Name,
                            Arguments = call.Arguments ?? "{}"
                        });

                        var toolSw = Stopwatch.StartNew();
                        object result;
                        try
                        {
                            result = await ExecuteAgentToolAsync(call.Name, callArgs, model,
                                traceId, sessionId, agentId, depth);
                        }
                        catch (Exception ex)
                        {
                            toolSw.Stop();
                            // ── tool.failed ──────────────────────────
                            EmitEvent(new ToolFailedEvent
                            {
                                Ctx        = MakeCtx(traceId, sessionId, agentId, depth),
                                CallId     = call.CallId,
                                Name       = call.Name,
                                Arguments  = call.Arguments ?? "{}",
                                Error      = ex.Message,
                                DurationMs = toolSw.ElapsedMilliseconds
                            });
                            throw;
                        }

                        toolSw.Stop();
                        string resultJson = JsonConvert.SerializeObject(result);

                        // ── tool.completed ───────────────────────────
                        EmitEvent(new ToolCompletedEvent
                        {
                            Ctx        = MakeCtx(traceId, sessionId, agentId, depth),
                            CallId     = call.CallId,
                            Name       = call.Name,
                            Arguments  = call.Arguments ?? "{}",
                            Output     = resultJson.Length > MaxToolOutputLogLength
                                ? resultJson.Substring(0, MaxToolOutputLogLength) + "…" : resultJson,
                            DurationMs = toolSw.ElapsedMilliseconds
                        });

                        conversation.Add(new
                        {
                            type    = "function_call_output",
                            call_id = call.CallId,
                            output  = resultJson
                        });

                        PersistItem(agentId, "function_call_output",
                            callId: call.CallId, output: resultJson);
                    }

                    // ── turn.completed ────────────────────────────────
                    EmitEvent(new TurnCompletedEvent
                    {
                        Ctx       = MakeCtx(traceId, sessionId, agentId, depth),
                        TurnCount = turnNumber,
                        Usage     = genUsage
                    });
                }

                throw new InvalidOperationException(
                    string.Format("Agent loop did not finish within {0} turns.", AgentMaxTurns));
            }
            catch (WaitingForHumanException)
            {
                throw; // propagate waiting without marking as failed
            }
            catch (Exception ex)
            {
                sw.Stop();
                // ── agent.failed ─────────────────────────────────────
                EmitEvent(new AgentFailedEvent
                {
                    Ctx   = MakeCtx(traceId, sessionId, agentId, depth),
                    Error = ex.Message
                });
                throw;
            }
        }

        // ----------------------------------------------------------------
        // Tool dispatcher
        // ----------------------------------------------------------------

        private static async Task<object> ExecuteAgentToolAsync(
            string name, JObject args, string model,
            string traceId, string sessionId, string agentId, int depth)
        {
            // Route MCP tools (prefixed as serverName__toolName) to the MCP manager
            if (McpManager != null && name.Contains("__"))
            {
                try
                {
                    string result = await McpManager.CallToolAsync(name, args);
                    return new { result };
                }
                catch (Exception ex)
                {
                    return new { error = ex.Message };
                }
            }

            switch (name)
            {
                case "calculator":   return AgentToolExecutors.ExecuteCalculator(args);
                case "list_files":   return AgentToolExecutors.ExecuteListFiles(args);
                case "read_file":    return AgentToolExecutors.ExecuteReadFile(args);
                case "write_file":   return AgentToolExecutors.ExecuteWriteFile(args);
                case "delegate":     return await ExecuteDelegateAsync(args, model,
                                            traceId, sessionId, agentId, depth);
                case "send_message": return AgentToolExecutors.ExecuteSendMessage(args);
                default:
                    return new { error = "Unknown tool: " + name };
            }
        }

        // ----------------------------------------------------------------
        // delegate tool — runs a child agent loop recursively
        // ----------------------------------------------------------------

        private static async Task<object> ExecuteDelegateAsync(
            JObject args, string model,
            string traceId, string sessionId, string parentAgentId, int depth)
        {
            string agentName = args["agent"]?.ToString();
            string task      = args["task"]?.ToString();

            if (string.IsNullOrEmpty(agentName) || string.IsNullOrEmpty(task))
                return new { error = "Both \"agent\" and \"task\" are required." };

            string systemPrompt = LoadAgentTemplate(agentName);
            if (systemPrompt == null)
                return new { error = string.Format("Agent template not found: {0}", agentName) };

            var childConversation = new List<object>
            {
                new { type = "message", role = "system", content = systemPrompt },
                new { type = "message", role = "user",   content = task }
            };

            try
            {
                string childResult = await RunAgentLoopAsync(
                    childConversation, model,
                    traceId, sessionId,
                    agentId:   Guid.NewGuid().ToString(),
                    agentName: agentName,
                    depth:     depth + 1);
                return new { ok = true, output = childResult };
            }
            catch (WaitingForHumanException)
            {
                return new { error = "Delegate agent requires human input and cannot complete automatically." };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }

        // ----------------------------------------------------------------
        // Agent template loader (mirrors workspace/agents/*.agent.md loading)
        // ----------------------------------------------------------------

        internal static string LoadAgentTemplate(string agentName)
        {
            string agentsDir = Path.Combine(WorkspaceRoot, "agents");
            string agentFile = Path.Combine(agentsDir, agentName + ".agent.md");

            if (!File.Exists(agentFile)) return null;

            string raw = File.ReadAllText(agentFile, Encoding.UTF8);

            // Strip YAML front-matter (--- ... ---) and return body
            int start = raw.IndexOf("---", StringComparison.Ordinal);
            int end   = start >= 0 ? raw.IndexOf("---", start + 3, StringComparison.Ordinal) : -1;

            if (start == 0 && end > 0)
                raw = raw.Substring(end + 3).TrimStart('\r', '\n');

            return raw.Trim();
        }

        // ----------------------------------------------------------------
        // Event helpers
        // ----------------------------------------------------------------

        private static EventContext MakeCtx(string traceId, string sessionId, string agentId, int depth)
        {
            return EventFactory.CreateContext(traceId, sessionId, agentId, agentId, depth);
        }

        private static void EmitEvent(AgentEvent evt)
        {
            if (Events != null)
                Events.Emit(evt);
        }

        private static TokenUsage MapUsage(ResponsesResponse parsed)
        {
            if (parsed?.Usage == null) return null;
            return new TokenUsage
            {
                InputTokens  = parsed.Usage.InputTokens,
                OutputTokens = parsed.Usage.OutputTokens
            };
        }

        // ----------------------------------------------------------------
        // DB persistence helpers
        // ----------------------------------------------------------------

        private static void PersistItem(
            string agentId, string type,
            string role = null, string content = null,
            string callId = null, string name = null,
            string arguments = null, string output = null)
        {
            if (Database == null) return;

            try
            {
                int seq = Database.GetNextSequence(agentId);
                Database.InsertItem(new ItemRow
                {
                    Id        = Guid.NewGuid().ToString(),
                    AgentId   = agentId,
                    Sequence  = seq,
                    Type      = type,
                    Role      = role,
                    Content   = content,
                    CallId    = callId,
                    Name      = name,
                    Arguments = arguments,
                    Output    = output,
                    CreatedAt = AgentDb.ToUnixMs(DateTime.UtcNow)
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[db] Failed to persist item: " + ex.Message);
            }
        }

        // ----------------------------------------------------------------
        // HTTP helper (shared AI API call)
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

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }
    }
}
