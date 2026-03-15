using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson05_Agent
{
    /// <summary>
    /// Lesson 05 – Agent API Server
    ///
    /// A lightweight HTTP API server that exposes an agent loop over REST.
    /// Clients send chat requests; the server runs the agentic loop (tool calls,
    /// session state) and returns the result.
    ///
    /// Endpoints:
    ///   GET  /health
    ///   GET  /api/mcp/servers
    ///   POST /api/chat/completions
    ///   GET  /api/chat/agents/:agentId
    ///
    /// Authentication: Bearer token (AUTH_TOKEN in App.config).
    ///
    /// Source: 01_05_agent/src/ (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string DefaultModel    = "gpt-4.1-mini";
        private const int    DefaultMaxTurns = 10;

        // ----------------------------------------------------------------
        // Server config (read from App.config)
        // ----------------------------------------------------------------

        private static string AuthToken    => Cfg("AUTH_TOKEN")    ?? string.Empty;
        private static string Host         => Cfg("HOST")          ?? "127.0.0.1";
        private static int    Port         => int.TryParse(Cfg("PORT"), out int p) ? p : 3000;
        private static int    AgentMaxTurns => int.TryParse(Cfg("AGENT_MAX_TURNS"), out int t) ? t : DefaultMaxTurns;

        private static string _workspaceRoot;

        // In-memory storage
        private static readonly Dictionary<string, SessionData> Sessions =
            new Dictionary<string, SessionData>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, AgentRunData> AgentRuns =
            new Dictionary<string, AgentRunData>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _lock = new object();

        // ----------------------------------------------------------------
        // Entry point
        // ----------------------------------------------------------------

        static void Main(string[] args)
        {
            _workspaceRoot = GetWorkspacePath();
            Directory.CreateDirectory(_workspaceRoot);

            string prefix = string.Format("http://{0}:{1}/", Host, Port);
            var listener  = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                Console.Error.WriteLine("Failed to start listener on " + prefix + ": " + ex.Message);
                Console.Error.WriteLine("On Windows you may need to run as Administrator or reserve the URL.");
                Environment.Exit(1);
            }

            Console.WriteLine("=== Agent API Server ===");
            Console.WriteLine("Listening on " + prefix);
            Console.WriteLine("Auth token : " + (string.IsNullOrEmpty(AuthToken) ? "(none)" : AuthToken.Substring(0, Math.Min(8, AuthToken.Length)) + "..."));
            Console.WriteLine("Workspace  : " + _workspaceRoot);
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  curl -s http://" + Host + ":" + Port + "/api/chat/completions \\");
            Console.WriteLine("    -H \"Authorization: Bearer " + AuthToken + "\" \\");
            Console.WriteLine("    -H \"Content-Type: application/json\" \\");
            Console.WriteLine("    -d '{\"agent\":\"alice\",\"input\":\"What is 42 * 17?\"}' | jq");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to stop.");

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                listener.Stop();
            };

            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break;
                }

                Task.Run(() => HandleContextAsync(ctx));
            }
        }

        // ----------------------------------------------------------------
        // Router
        // ----------------------------------------------------------------

        static async Task HandleContextAsync(HttpListenerContext ctx)
        {
            var req  = ctx.Request;
            var resp = ctx.Response;

            try
            {
                string path   = req.Url.AbsolutePath.TrimEnd('/');
                string method = req.HttpMethod.ToUpperInvariant();

                // Health – no auth required
                if (method == "GET" && path == "/health")
                {
                    await WriteJsonAsync(resp, 200, new { status = "ok", timestamp = DateTime.UtcNow.ToString("O") });
                    return;
                }

                // Auth check for all other endpoints
                if (!IsAuthorized(req))
                {
                    await WriteJsonAsync(resp, 401,
                        new { data = (object)null, error = new { message = "Unauthorized" } });
                    return;
                }

                if (method == "GET" && path == "/api/mcp/servers")
                {
                    await HandleListMcpServersAsync(resp);
                    return;
                }

                if (method == "POST" && path == "/api/chat/completions")
                {
                    await HandleChatCompletionsAsync(req, resp);
                    return;
                }

                // GET /api/chat/agents/:agentId
                var agentMatch = Regex.Match(path, @"^/api/chat/agents/([^/]+)$");
                if (method == "GET" && agentMatch.Success)
                {
                    await HandleGetAgentAsync(agentMatch.Groups[1].Value, resp);
                    return;
                }

                // POST /api/chat/agents/:agentId/deliver
                var deliverMatch = Regex.Match(path, @"^/api/chat/agents/([^/]+)/deliver$");
                if (method == "POST" && deliverMatch.Success)
                {
                    await HandleDeliverAsync(deliverMatch.Groups[1].Value, req, resp);
                    return;
                }

                await WriteJsonAsync(resp, 404,
                    new { data = (object)null, error = new { message = "Not found" } });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[error] " + ex.Message);
                try
                {
                    await WriteJsonAsync(resp, 500,
                        new { data = (object)null, error = new { message = "Internal server error" } });
                }
                catch { /* ignore secondary write errors */ }
            }
        }

        // ----------------------------------------------------------------
        // Handlers
        // ----------------------------------------------------------------

        static Task HandleListMcpServersAsync(HttpListenerResponse resp)
        {
            return WriteJsonAsync(resp, 200, new
            {
                data  = new { servers = new object[0] },
                error = (object)null
            });
        }

        static async Task HandleChatCompletionsAsync(
            HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body;
            using (var sr = new StreamReader(req.InputStream, Encoding.UTF8))
                body = await sr.ReadToEndAsync();

            JObject request;
            try { request = JObject.Parse(body); }
            catch
            {
                await WriteJsonAsync(resp, 400,
                    new { data = (object)null, error = new { message = "Invalid JSON body" } });
                return;
            }

            string input       = request["input"]?.ToString() ?? string.Empty;
            string agentName   = request["agent"]?.ToString();
            string sessionId   = request["sessionId"]?.ToString();
            string model       = request["model"]?.ToString();
            string instructions = request["instructions"]?.ToString();

            if (string.IsNullOrWhiteSpace(input))
            {
                await WriteJsonAsync(resp, 400,
                    new { data = (object)null, error = new { message = "'input' is required" } });
                return;
            }

            // Resolve session
            SessionData session;
            if (!string.IsNullOrEmpty(sessionId))
            {
                lock (_lock)
                {
                    if (!Sessions.TryGetValue(sessionId, out session))
                    {
                        session = new SessionData { Id = sessionId };
                        Sessions[sessionId] = session;
                    }
                }
            }
            else
            {
                sessionId = Guid.NewGuid().ToString();
                session   = new SessionData { Id = sessionId };
                lock (_lock) Sessions[sessionId] = session;
            }

            // Load agent template
            string systemPrompt = instructions;
            if (string.IsNullOrEmpty(systemPrompt) && !string.IsNullOrEmpty(agentName))
                systemPrompt = LoadAgentTemplate(agentName);

            // Determine model
            string resolvedModel = AiConfig.ResolveModel(
                string.IsNullOrWhiteSpace(model) ? DefaultModel : SanitizeModel(model));

            // Run agent
            string agentId = Guid.NewGuid().ToString();
            var runData    = new AgentRunData { Id = agentId, SessionId = sessionId, Status = "running" };
            lock (_lock) AgentRuns[agentId] = runData;

            try
            {
                var conversation = new List<object>(session.History);

                if (!string.IsNullOrEmpty(systemPrompt))
                    conversation.Insert(0,
                        new { type = "message", role = "system", content = systemPrompt });

                conversation.Add(new { type = "message", role = "user", content = input });

                try
                {
                    var result = await RunAgentLoopAsync(conversation, resolvedModel);

                    // Persist history (without system prompt)
                    var historyItems = conversation
                        .Where(o =>
                        {
                            var j = JObject.FromObject(o);
                            string t = j["type"]?.ToString() ?? string.Empty;
                            string r = j["role"]?.ToString() ?? string.Empty;
                            return t != "message" || r != "system";
                        })
                        .ToList();

                    lock (_lock)
                    {
                        session.History = historyItems;
                        runData.Status  = "completed";
                    }

                    await WriteJsonAsync(resp, 200, new
                    {
                        data = new
                        {
                            id        = agentId,
                            sessionId,
                            status    = "completed",
                            model     = resolvedModel,
                            output    = new[] { new { type = "text", text = result } },
                            waitingFor = new object[0]
                        },
                        error = (object)null
                    });
                }
                catch (WaitingForHumanException waitEx)
                {
                    var waitEntry = new WaitingForEntry
                    {
                        CallId   = waitEx.CallId,
                        Type     = "human",
                        Question = waitEx.Question
                    };

                    lock (_lock)
                    {
                        runData.Status       = "waiting";
                        runData.Model        = resolvedModel;
                        runData.Conversation = conversation;
                        runData.WaitingFor   = new List<WaitingForEntry> { waitEntry };
                    }

                    await WriteJsonAsync(resp, 202, new
                    {
                        data = new
                        {
                            id         = agentId,
                            sessionId,
                            status     = "waiting",
                            model      = resolvedModel,
                            output     = new object[0],
                            waitingFor = new[]
                            {
                                new { callId = waitEx.CallId, type = "human", question = waitEx.Question }
                            }
                        },
                        error = (object)null
                    });
                }
            }
            catch (Exception ex)
            {
                lock (_lock) runData.Status = "error";
                await WriteJsonAsync(resp, 500, new
                {
                    data  = (object)null,
                    error = new { message = ex.Message }
                });
            }
        }

        static Task HandleGetAgentAsync(string agentId, HttpListenerResponse resp)
        {
            AgentRunData run;
            lock (_lock) AgentRuns.TryGetValue(agentId, out run);

            if (run == null)
            {
                return WriteJsonAsync(resp, 404,
                    new { data = (object)null, error = new { message = "Agent not found" } });
            }

            var waitingFor = run.WaitingFor != null
                ? run.WaitingFor.Select(w => (object)new { callId = w.CallId, type = w.Type, question = w.Question }).ToArray()
                : new object[0];

            return WriteJsonAsync(resp, 200, new
            {
                data = new
                {
                    id        = run.Id,
                    sessionId = run.SessionId,
                    status    = run.Status,
                    waitingFor
                },
                error = (object)null
            });
        }

        static async Task HandleDeliverAsync(string agentId, HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body;
            using (var sr = new StreamReader(req.InputStream, Encoding.UTF8))
                body = await sr.ReadToEndAsync();

            JObject request;
            try { request = JObject.Parse(body); }
            catch
            {
                await WriteJsonAsync(resp, 400,
                    new { data = (object)null, error = new { message = "Invalid JSON body" } });
                return;
            }

            string callId  = request["callId"]?.ToString();
            string output  = request["output"]?.ToString() ?? string.Empty;

            AgentRunData run;
            lock (_lock) AgentRuns.TryGetValue(agentId, out run);

            if (run == null)
            {
                await WriteJsonAsync(resp, 404,
                    new { data = (object)null, error = new { message = "Agent not found" } });
                return;
            }

            if (run.Status != "waiting")
            {
                await WriteJsonAsync(resp, 400,
                    new { data = (object)null, error = new { message = "Agent is not waiting" } });
                return;
            }

            WaitingForEntry entry;
            lock (_lock)
            {
                entry = run.WaitingFor?.FirstOrDefault(w => w.CallId == callId)
                     ?? run.WaitingFor?.FirstOrDefault();
            }

            if (entry == null)
            {
                await WriteJsonAsync(resp, 400,
                    new { data = (object)null, error = new { message = "No matching pending call" } });
                return;
            }

            List<object> conversation;
            string model;
            lock (_lock)
            {
                conversation = run.Conversation;
                model        = run.Model;
            }

            // Inject the human answer as the function call output
            conversation.Add(new
            {
                type    = "function_call_output",
                call_id = entry.CallId,
                output
            });

            try
            {
                string result = await RunAgentLoopAsync(conversation, model);

                // Persist history to the session (skip system messages)
                SessionData session;
                lock (_lock) Sessions.TryGetValue(run.SessionId, out session);
                if (session != null)
                {
                    var historyItems = conversation
                        .Where(o =>
                        {
                            var j = JObject.FromObject(o);
                            string t = j["type"]?.ToString() ?? string.Empty;
                            string r = j["role"]?.ToString() ?? string.Empty;
                            return t != "message" || r != "system";
                        })
                        .ToList();

                    lock (_lock)
                    {
                        session.History      = historyItems;
                        run.Status           = "completed";
                        run.WaitingFor       = null;
                        run.Conversation     = null;
                    }
                }

                await WriteJsonAsync(resp, 200, new
                {
                    data = new
                    {
                        id         = agentId,
                        sessionId  = run.SessionId,
                        status     = "completed",
                        model,
                        output     = new[] { new { type = "text", text = result } },
                        waitingFor = new object[0]
                    },
                    error = (object)null
                });
            }
            catch (WaitingForHumanException waitEx)
            {
                var newEntry = new WaitingForEntry
                {
                    CallId   = waitEx.CallId,
                    Type     = "human",
                    Question = waitEx.Question
                };

                lock (_lock)
                {
                    run.WaitingFor = new List<WaitingForEntry> { newEntry };
                }

                await WriteJsonAsync(resp, 202, new
                {
                    data = new
                    {
                        id         = agentId,
                        sessionId  = run.SessionId,
                        status     = "waiting",
                        model,
                        output     = new object[0],
                        waitingFor = new[]
                        {
                            new { callId = waitEx.CallId, type = "human", question = waitEx.Question }
                        }
                    },
                    error = (object)null
                });
            }
            catch (Exception ex)
            {
                lock (_lock) run.Status = "error";
                await WriteJsonAsync(resp, 500,
                    new { data = (object)null, error = new { message = ex.Message } });
            }
        }

        // ----------------------------------------------------------------
        // Agent loop
        // ----------------------------------------------------------------

        static async Task<string> RunAgentLoopAsync(List<object> conversation, string model)
        {
            var tools = BuildAgentTools();

            for (int step = 0; step < AgentMaxTurns; step++)
            {
                var body = new JObject
                {
                    ["model"] = model,
                    ["input"] = JArray.FromObject(conversation),
                    ["tools"] = JArray.FromObject(tools)
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);

                if (parsed?.Error != null)
                    throw new InvalidOperationException(parsed.Error.Message);

                var toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                if (toolCalls.Count == 0)
                {
                    string text = ResponsesApiClient.ExtractText(parsed);
                    foreach (var item in parsed.Output)
                    {
                        if (item.Type == "message")
                            conversation.Add(new { type = "message", role = "assistant", content = text });
                    }
                    return text;
                }

                // Append function_call items
                foreach (var item in parsed.Output)
                {
                    if (item.Type == "function_call")
                        conversation.Add(new
                        {
                            type      = "function_call",
                            call_id   = item.CallId,
                            name      = item.Name,
                            arguments = item.Arguments
                        });
                }

                // Execute tools
                foreach (var call in toolCalls)
                {
                    var callArgs = JObject.Parse(call.Arguments ?? "{}");

                    // ask_user: pause the agent and wait for human input
                    if (call.Name == "ask_user")
                    {
                        string question = callArgs["question"]?.ToString() ?? string.Empty;
                        throw new WaitingForHumanException(call.CallId, question);
                    }

                    object result    = await ExecuteAgentToolAsync(call.Name, callArgs, model);
                    string resultJson = JsonConvert.SerializeObject(result);

                    Console.WriteLine(string.Format("  [tool] {0} → {1}",
                        call.Name,
                        resultJson.Length > 120 ? resultJson.Substring(0, 120) + "..." : resultJson));

                    conversation.Add(new
                    {
                        type    = "function_call_output",
                        call_id = call.CallId,
                        output  = resultJson
                    });
                }
            }

            throw new InvalidOperationException(
                string.Format("Agent loop did not finish within {0} turns.", AgentMaxTurns));
        }

        // ----------------------------------------------------------------
        // Agent tool definitions
        // ----------------------------------------------------------------

        static List<ToolDefinition> BuildAgentTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "calculator",
                    Description = "Evaluate a mathematical expression and return the numeric result.",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            expression = new { type = "string", description = "Math expression, e.g. '42 * 17' or 'sqrt(144)'" }
                        },
                        required             = new[] { "expression" },
                        additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "list_files",
                    Description = "List files and directories inside the agent workspace.",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Relative path inside workspace (use '.' for root)" }
                        },
                        required             = new[] { "path" },
                        additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "read_file",
                    Description = "Read the text content of a file inside the agent workspace.",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Relative file path inside workspace/" }
                        },
                        required             = new[] { "path" },
                        additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "write_file",
                    Description = "Create or overwrite a file inside the agent workspace.",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            path    = new { type = "string", description = "Relative file path inside workspace/" },
                            content = new { type = "string", description = "Text content to write" }
                        },
                        required             = new[] { "path", "content" },
                        additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "ask_user",
                    Description = "Ask the user a question and wait for their response. " +
                                  "Use this when you need clarification, confirmation, or additional " +
                                  "information that only the user can provide. The agent will pause " +
                                  "until the user responds.",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            question = new { type = "string", description = "The question to ask the user" }
                        },
                        required             = new[] { "question" },
                        additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "delegate",
                    Description = "Delegate a task to another agent and wait for the result. " +
                                  "Use this when a specialised agent can handle part of the work " +
                                  "(e.g. web research, file operations).",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            agent = new { type = "string", description = "Name of the agent template to run (e.g. \"bob\")" },
                            task  = new { type = "string", description = "A clear description of what the child agent should accomplish" }
                        },
                        required             = new[] { "agent", "task" },
                        additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "send_message",
                    Description = "Send a non-blocking message to another running agent. " +
                                  "The message appears in the target agent's context on their next turn. " +
                                  "Use this to share information without waiting for a response.",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            to      = new { type = "string", description = "The agent ID to send the message to" },
                            message = new { type = "string", description = "The message content to deliver" }
                        },
                        required             = new[] { "to", "message" },
                        additionalProperties = false
                    },
                    Strict = true
                }
            };
        }

        // ----------------------------------------------------------------
        // Agent tool execution
        // ----------------------------------------------------------------

        static async Task<object> ExecuteAgentToolAsync(string name, JObject args, string model)
        {
            switch (name)
            {
                case "calculator":   return ExecuteCalculator(args);
                case "list_files":   return ExecuteListFiles(args);
                case "read_file":    return ExecuteReadFile(args);
                case "write_file":   return ExecuteWriteFile(args);
                case "delegate":     return await ExecuteDelegateAsync(args, model);
                case "send_message": return ExecuteSendMessage(args);
                default:
                    return new { error = "Unknown tool: " + name };
            }
        }

        static async Task<object> ExecuteDelegateAsync(JObject args, string model)
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
                string childResult = await RunAgentLoopAsync(childConversation, model);
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

        static object ExecuteSendMessage(JObject args)
        {
            string to      = args["to"]?.ToString();
            string message = args["message"]?.ToString();

            if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(message))
                return new { error = "Both \"to\" and \"message\" are required." };

            lock (_lock)
            {
                AgentRunData targetRun;
                if (!AgentRuns.TryGetValue(to, out targetRun))
                    return new { error = string.Format("Agent not found: {0}", to) };

                SessionData targetSession;
                if (!Sessions.TryGetValue(targetRun.SessionId, out targetSession))
                    return new { error = string.Format("Session not found for agent: {0}", to) };

                targetSession.History.Add(new
                {
                    type    = "message",
                    role    = "system",
                    content = message
                });
            }

            return new { ok = true, output = string.Format("Message delivered to agent {0}", to) };
        }

        static object ExecuteCalculator(JObject args)
        {
            string expr = args["expression"]?.ToString() ?? string.Empty;
            try
            {
                // Use DataTable.Compute for simple arithmetic
                object raw = new System.Data.DataTable().Compute(expr, null);
                double val = Convert.ToDouble(raw);
                return new { expression = expr, result = val };
            }
            catch (Exception ex)
            {
                return new { expression = expr, error = "Evaluation failed: " + ex.Message };
            }
        }

        static object ExecuteListFiles(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? ".";
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };
            if (!Directory.Exists(absPath)) return new { error = "Directory not found: " + rel };

            var entries = new List<object>();
            foreach (string d in Directory.GetDirectories(absPath))
                entries.Add(new { type = "directory", name = Path.GetFileName(d) });
            foreach (string f in Directory.GetFiles(absPath))
                entries.Add(new { type = "file", name = Path.GetFileName(f) });

            return new { path = rel, entries };
        }

        static object ExecuteReadFile(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? string.Empty;
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };
            if (!File.Exists(absPath)) return new { error = "File not found: " + rel };

            return new { path = rel, content = File.ReadAllText(absPath, Encoding.UTF8) };
        }

        static object ExecuteWriteFile(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? string.Empty;
            string content = args["content"]?.ToString() ?? string.Empty;
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };

            string dir = Path.GetDirectoryName(absPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(absPath, content, Encoding.UTF8);
            return new { success = true, path = rel, bytesWritten = Encoding.UTF8.GetByteCount(content) };
        }

        // ----------------------------------------------------------------
        // Agent template loader
        // ----------------------------------------------------------------

        static string LoadAgentTemplate(string agentName)
        {
            string agentsDir  = Path.Combine(_workspaceRoot, "agents");
            string agentFile  = Path.Combine(agentsDir, agentName + ".agent.md");

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
        // Auth helper
        // ----------------------------------------------------------------

        static bool IsAuthorized(HttpListenerRequest req)
        {
            if (string.IsNullOrEmpty(AuthToken)) return true; // no auth configured

            string authHeader = req.Headers["Authorization"] ?? string.Empty;
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return false;

            string token = authHeader.Substring("Bearer ".Length).Trim();
            return string.Equals(token, AuthToken, StringComparison.Ordinal);
        }

        // ----------------------------------------------------------------
        // Workspace helpers
        // ----------------------------------------------------------------

        static string ResolveWorkspacePath(string relativePath)
        {
            string full = Path.GetFullPath(Path.Combine(_workspaceRoot, relativePath));

            return full.StartsWith(_workspaceRoot + Path.DirectorySeparatorChar,
                                   StringComparison.OrdinalIgnoreCase)
                   || string.Equals(full, _workspaceRoot, StringComparison.OrdinalIgnoreCase)
                ? full
                : null;
        }

        static string GetWorkspacePath()
        {
            string cfg = Cfg("WORKSPACE_PATH");
            if (!string.IsNullOrWhiteSpace(cfg)) return cfg;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");
        }

        // ----------------------------------------------------------------
        // HTTP helpers
        // ----------------------------------------------------------------

        static async Task WriteJsonAsync(HttpListenerResponse resp, int statusCode, object body)
        {
            string json  = JsonConvert.SerializeObject(body);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            resp.StatusCode              = statusCode;
            resp.ContentType             = "application/json; charset=utf-8";
            resp.ContentLength64         = bytes.Length;
            resp.Headers["Access-Control-Allow-Origin"] = "*";

            await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            resp.OutputStream.Close();
        }

        static async Task<string> PostRawAsync(string jsonBody)
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

        // ----------------------------------------------------------------
        // Config / model helpers
        // ----------------------------------------------------------------

        static string Cfg(string key)
        {
            return ConfigurationManager.AppSettings[key]?.Trim();
        }

        /// <summary>
        /// Strip "provider:" prefix that a client may include (e.g. "openai:gpt-4.1")
        /// leaving only the model identifier to pass to AiConfig.ResolveModel.
        /// </summary>
        static string SanitizeModel(string model)
        {
            int colon = model.IndexOf(':');
            return colon >= 0 ? model.Substring(colon + 1) : model;
        }
    }

    // ----------------------------------------------------------------
    // Data holders
    // ----------------------------------------------------------------

    internal class SessionData
    {
        public string       Id      { get; set; }
        public List<object> History { get; set; } = new List<object>();
    }

    internal class AgentRunData
    {
        public string                Id           { get; set; }
        public string                SessionId    { get; set; }
        public string                Status       { get; set; }
        public string                Model        { get; set; }
        public List<object>          Conversation { get; set; }
        public List<WaitingForEntry> WaitingFor   { get; set; }
    }

    internal class WaitingForEntry
    {
        public string CallId   { get; set; }
        public string Type     { get; set; }
        public string Question { get; set; }
    }

    internal class WaitingForHumanException : Exception
    {
        public string CallId   { get; }
        public string Question { get; }

        public WaitingForHumanException(string callId, string question)
            : base("Agent is waiting for human input.")
        {
            CallId   = callId;
            Question = question;
        }
    }
}
