using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Lesson05_Agent.Db;
using FourthDevs.Lesson05_Agent.Events;
using FourthDevs.Lesson05_Agent.Tools;
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
    ///   POST /api/chat/agents/:agentId/deliver
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

        private static string AuthToken     => Cfg("AUTH_TOKEN")     ?? string.Empty;
        private static string Host          => Cfg("HOST")           ?? "127.0.0.1";
        private static int    Port          => int.TryParse(Cfg("PORT"), out int p) ? p : 3000;
        private static int    AgentMaxTurns => int.TryParse(Cfg("AGENT_MAX_TURNS"), out int t) ? t : DefaultMaxTurns;

        // ----------------------------------------------------------------
        // Shared in-memory state (injected into tool executors + agent runner)
        // ----------------------------------------------------------------

        private static readonly Dictionary<string, SessionData>  Sessions  =
            new Dictionary<string, SessionData>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, AgentRunData> AgentRuns =
            new Dictionary<string, AgentRunData>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _lock = new object();

        private static Mcp.McpClientManager _mcpManager;
        private static AgentEventEmitter    _events;
        private static AgentDb              _database;

        // ----------------------------------------------------------------
        // Entry point
        // ----------------------------------------------------------------

        static void Main(string[] args)
        {
            string workspaceRoot = GetWorkspacePath();
            Directory.CreateDirectory(workspaceRoot);

            // Initialize event emitter and subscribe console logger
            _events = new AgentEventEmitter();
            EventLogger.Subscribe(_events);

            // Initialize SQLite database (optional — errors are logged, not fatal)
            string dbUrl = Cfg("DATABASE_URL");
            if (string.IsNullOrWhiteSpace(dbUrl))
                dbUrl = "file:" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".data", "agent.db");
            try
            {
                _database = new AgentDb(dbUrl);
                Console.WriteLine("Database   : " + dbUrl);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[db] Initialization error: " + ex.Message);
            }

            // Inject shared state into extracted classes
            AgentRunner.WorkspaceRoot     = workspaceRoot;
            AgentRunner.AgentMaxTurns     = AgentMaxTurns;
            AgentRunner.Events            = _events;
            AgentRunner.Database          = _database;
            AgentToolExecutors.WorkspaceRoot = workspaceRoot;
            AgentToolExecutors.AgentRuns     = AgentRuns;
            AgentToolExecutors.Sessions      = Sessions;
            AgentToolExecutors.StateLock     = _lock;

            // Initialize MCP client manager (non-blocking – errors are logged, not fatal)
            _mcpManager = new Mcp.McpClientManager();
            string mcpJsonPath = ResolveMcpJsonPath(workspaceRoot);
            if (File.Exists(mcpJsonPath))
            {
                Console.WriteLine("Loading MCP config from: " + mcpJsonPath);
                try
                {
                    _mcpManager.InitializeAsync(mcpJsonPath).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[mcp] Initialization error: " + ex.Message);
                }
            }
            AgentRunner.McpManager = _mcpManager;

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
            Console.WriteLine("Workspace  : " + workspaceRoot);
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
            var serverList = new List<object>();
            if (_mcpManager != null)
            {
                foreach (var name in _mcpManager.GetServerNames())
                    serverList.Add(new { name, status = "connected" });
            }

            return WriteJsonAsync(resp, 200, new
            {
                data  = new { servers = serverList },
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

            string input        = request["input"]?.ToString() ?? string.Empty;
            string agentName    = request["agent"]?.ToString();
            string sessionId    = request["sessionId"]?.ToString();
            string model        = request["model"]?.ToString();
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
                systemPrompt = AgentRunner.LoadAgentTemplate(agentName);

            // Determine model
            string resolvedModel = AiConfig.ResolveModel(
                string.IsNullOrWhiteSpace(model) ? DefaultModel : SanitizeModel(model));

            // Run agent
            string agentId = Guid.NewGuid().ToString();
            string traceId = Guid.NewGuid().ToString();
            var runData    = new AgentRunData { Id = agentId, SessionId = sessionId, Status = "running" };
            lock (_lock) AgentRuns[agentId] = runData;

            // Persist session + agent row in DB
            if (_database != null)
            {
                try
                {
                    _database.UpsertSession(sessionId, null, agentId, "active");
                    _database.UpsertAgent(new AgentRow
                    {
                        Id          = agentId,
                        SessionId   = sessionId,
                        RootAgentId = agentId,
                        Depth       = 0,
                        Task        = systemPrompt ?? string.Empty,
                        Config      = JsonConvert.SerializeObject(new { model = resolvedModel }),
                        Status      = "running",
                        WaitingFor  = "[]",
                        TurnCount   = 0,
                        CreatedAt   = AgentDb.ToUnixMs(DateTime.UtcNow),
                        StartedAt   = AgentDb.ToUnixMs(DateTime.UtcNow)
                    });
                }
                catch (Exception dbEx)
                {
                    Console.Error.WriteLine("[db] persist error: " + dbEx.Message);
                }
            }

            try
            {
                var conversation = new List<object>(session.History);

                if (!string.IsNullOrEmpty(systemPrompt))
                    conversation.Insert(0,
                        new { type = "message", role = "system", content = systemPrompt });

                conversation.Add(new { type = "message", role = "user", content = input });

                try
                {
                    var result = await AgentRunner.RunAgentLoopAsync(
                        conversation, resolvedModel,
                        traceId, sessionId, agentId, agentName);

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

        static async Task HandleDeliverAsync(
            string agentId, HttpListenerRequest req, HttpListenerResponse resp)
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

            string callId = request["callId"]?.ToString();
            string output = request["output"]?.ToString() ?? string.Empty;

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

            // Emit agent.resumed event
            if (_events != null)
            {
                _events.Emit(new AgentResumedEvent
                {
                    Ctx = EventFactory.CreateContext(
                        Guid.NewGuid().ToString(), run.SessionId, agentId, agentId, 0),
                    DeliveredCallId = entry.CallId,
                    Remaining       = 0
                });
            }

            try
            {
                string result = await AgentRunner.RunAgentLoopAsync(
                    conversation, model,
                    sessionId: run.SessionId, agentId: agentId);

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
                        session.History  = historyItems;
                        run.Status       = "completed";
                        run.WaitingFor   = null;
                        run.Conversation = null;
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

        static string GetWorkspacePath()
        {
            string cfg = Cfg("WORKSPACE_PATH");
            if (!string.IsNullOrWhiteSpace(cfg)) return cfg;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");
        }

        static string ResolveMcpJsonPath(string workspaceRoot)
        {
            // 1. Check MCP_CONFIG env/app setting
            string cfg = Cfg("MCP_CONFIG");
            if (!string.IsNullOrWhiteSpace(cfg)) return cfg;

            // 2. Look for .mcp.json next to the workspace directory (project root)
            string projectDir = Path.GetDirectoryName(workspaceRoot) ?? workspaceRoot;
            string candidate  = Path.Combine(projectDir, ".mcp.json");
            if (File.Exists(candidate)) return candidate;

            // 3. Look in the workspace directory itself
            return Path.Combine(workspaceRoot, ".mcp.json");
        }

        // ----------------------------------------------------------------
        // HTTP helpers
        // ----------------------------------------------------------------

        static async Task WriteJsonAsync(HttpListenerResponse resp, int statusCode, object body)
        {
            string json  = JsonConvert.SerializeObject(body);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            resp.StatusCode      = statusCode;
            resp.ContentType     = "application/json; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.Headers["Access-Control-Allow-Origin"] = "*";

            await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            resp.OutputStream.Close();
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
}
