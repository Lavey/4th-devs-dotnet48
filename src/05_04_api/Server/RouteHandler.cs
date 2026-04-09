using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.MultiAgentApi.Agent;
using FourthDevs.MultiAgentApi.Auth;
using FourthDevs.MultiAgentApi.Db;
using FourthDevs.MultiAgentApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.MultiAgentApi.Server
{
    /// <summary>
    /// Handles all v1 API routes. Dispatches to the appropriate handler based
    /// on path and method.
    /// </summary>
    internal sealed class RouteHandler
    {
        private readonly DatabaseManager _db;
        private readonly AuthManager _auth;
        private readonly AgentRunner _runner;
        private readonly MemoryManager _memory;
        private readonly ConcurrentQueue<DomainEvent> _eventQueue;

        // Available models for the /v1/system/models endpoint
        private static readonly string[] AvailableModels = new[]
        {
            "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano",
            "gpt-4o", "gpt-4o-mini",
            "o4-mini", "o3", "o3-mini"
        };

        internal RouteHandler(DatabaseManager db, AuthManager auth,
            AgentRunner runner, MemoryManager memory, ConcurrentQueue<DomainEvent> eventQueue)
        {
            _db = db;
            _auth = auth;
            _runner = runner;
            _memory = memory;
            _eventQueue = eventQueue;
        }

        internal async Task HandleAsync(HttpListenerContext ctx, string path, string method,
            CancellationToken ct)
        {
            // ── System routes (no auth required) ──
            if (path == "/v1/system/health" && method == "GET")
            {
                await HandleHealth(ctx);
                return;
            }
            if (path == "/v1/system/models" && method == "GET")
            {
                await HandleModels(ctx);
                return;
            }

            // ── Auth routes ──
            if (path == "/v1/auth/login" && method == "POST")
            {
                await HandleLogin(ctx);
                return;
            }

            // ── Event stream (auth optional for dev) ──
            if (path == "/v1/events/stream" && method == "GET")
            {
                await HandleEventStream(ctx, ct);
                return;
            }

            // ── Authenticated routes ──
            var authCtx = Authenticate(ctx);
            if (authCtx == null)
            {
                throw new ApiException(401, "unauthorized", "Authentication required");
            }

            // Auth session routes
            if (path == "/v1/auth/logout" && method == "POST")
            {
                await HandleLogout(ctx, authCtx);
                return;
            }
            if (path == "/v1/auth/session" && method == "GET")
            {
                await HandleGetSession(ctx, authCtx);
                return;
            }

            // Account
            if (path == "/v1/account" && method == "GET")
            {
                await HandleGetAccount(ctx, authCtx);
                return;
            }

            // Agents CRUD
            if (path == "/v1/agents" && method == "GET")
            {
                await HandleListAgents(ctx, authCtx);
                return;
            }
            if (path == "/v1/agents" && method == "POST")
            {
                await HandleCreateAgent(ctx, authCtx);
                return;
            }
            if (Regex.IsMatch(path, @"^/v1/agents/[^/]+$") && method == "GET")
            {
                string id = path.Substring("/v1/agents/".Length);
                await HandleGetAgent(ctx, authCtx, id);
                return;
            }
            if (Regex.IsMatch(path, @"^/v1/agents/[^/]+$") && (method == "PUT" || method == "PATCH"))
            {
                string id = path.Substring("/v1/agents/".Length);
                await HandleUpdateAgent(ctx, authCtx, id);
                return;
            }
            if (Regex.IsMatch(path, @"^/v1/agents/[^/]+$") && method == "DELETE")
            {
                string id = path.Substring("/v1/agents/".Length);
                await HandleDeleteAgent(ctx, authCtx, id);
                return;
            }

            // Sessions
            if (path == "/v1/sessions" && method == "POST")
            {
                await HandleCreateSession(ctx, authCtx);
                return;
            }
            if (path == "/v1/sessions/bootstrap" && method == "POST")
            {
                await HandleBootstrapSession(ctx, authCtx, ct);
                return;
            }

            // Threads
            if (path == "/v1/threads" && method == "GET")
            {
                await HandleListThreads(ctx, authCtx);
                return;
            }
            if (path == "/v1/threads" && method == "POST")
            {
                await HandleCreateThread(ctx, authCtx);
                return;
            }

            // Thread sub-routes
            if (Regex.IsMatch(path, @"^/v1/threads/[^/]+/interact$") && method == "POST")
            {
                string id = ExtractPathSegment(path, "/v1/threads/", "/interact");
                await HandleThreadInteract(ctx, authCtx, id, ct);
                return;
            }
            if (Regex.IsMatch(path, @"^/v1/threads/[^/]+/messages$") && method == "POST")
            {
                string id = ExtractPathSegment(path, "/v1/threads/", "/messages");
                await HandlePostMessage(ctx, authCtx, id);
                return;
            }
            if (Regex.IsMatch(path, @"^/v1/threads/[^/]+/messages$") && method == "GET")
            {
                string id = ExtractPathSegment(path, "/v1/threads/", "/messages");
                await HandleGetMessages(ctx, authCtx, id);
                return;
            }
            if (Regex.IsMatch(path, @"^/v1/threads/[^/]+/memory$") && method == "POST")
            {
                string id = ExtractPathSegment(path, "/v1/threads/", "/memory");
                await HandleUpdateMemory(ctx, authCtx, id);
                return;
            }
            if (Regex.IsMatch(path, @"^/v1/threads/[^/]+/memory$") && method == "GET")
            {
                string id = ExtractPathSegment(path, "/v1/threads/", "/memory");
                await HandleGetMemory(ctx, authCtx, id);
                return;
            }

            // Runs
            if (Regex.IsMatch(path, @"^/v1/runs/[^/]+/execute$") && method == "POST")
            {
                string id = ExtractPathSegment(path, "/v1/runs/", "/execute");
                await HandleExecuteRun(ctx, authCtx, id, ct);
                return;
            }
            if (Regex.IsMatch(path, @"^/v1/runs/[^/]+/resume$") && method == "POST")
            {
                string id = ExtractPathSegment(path, "/v1/runs/", "/resume");
                await HandleResumeRun(ctx, authCtx, id, ct);
                return;
            }
            if (Regex.IsMatch(path, @"^/v1/runs/[^/]+/cancel$") && method == "POST")
            {
                string id = ExtractPathSegment(path, "/v1/runs/", "/cancel");
                await HandleCancelRun(ctx, authCtx, id);
                return;
            }

            // Files
            if (path == "/v1/files" && method == "GET")
            {
                await HandleListFiles(ctx, authCtx);
                return;
            }
            if (Regex.IsMatch(path, @"^/v1/files/[^/]+/content$") && method == "GET")
            {
                string id = ExtractPathSegment(path, "/v1/files/", "/content");
                await HandleGetFileContent(ctx, authCtx, id);
                return;
            }

            throw new ApiException(404, "not_found", "Route not found: " + method + " " + path);
        }

        // ────────────────────────────────────────────────────────────
        // System handlers
        // ────────────────────────────────────────────────────────────

        private async Task HandleHealth(HttpListenerContext ctx)
        {
            await ApiServer.WriteJson(ctx, new JObject
            {
                ["status"] = "ok",
                ["version"] = "1.0.0",
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }

        private async Task HandleModels(HttpListenerContext ctx)
        {
            var models = new JArray();
            foreach (string model in AvailableModels)
            {
                models.Add(new JObject
                {
                    ["id"] = model,
                    ["name"] = model,
                    ["provider"] = AiConfig.Provider
                });
            }
            await ApiServer.WriteJson(ctx, new JObject { ["models"] = models });
        }

        // ────────────────────────────────────────────────────────────
        // Auth handlers
        // ────────────────────────────────────────────────────────────

        private async Task HandleLogin(HttpListenerContext ctx)
        {
            string body = await ApiServer.ReadBody(ctx);
            JObject req;
            try { req = JObject.Parse(body); }
            catch { throw new ApiException(400, "invalid_json", "Invalid JSON body"); }

            string email = req["email"]?.ToString();
            string password = req["password"]?.ToString();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                throw new ApiException(400, "missing_fields", "email and password are required");

            var account = _db.GetAccountByEmail(email);
            if (account == null)
                throw new ApiException(401, "invalid_credentials", "Invalid email or password");

            if (!_auth.ValidatePassword(account.Id, password))
                throw new ApiException(401, "invalid_credentials", "Invalid email or password");

            string tenantId = _db.GetFirstTenantIdForAccount(account.Id) ?? "ten_seed_default";
            string ipAddress = ctx.Request.RemoteEndPoint?.Address?.ToString();
            string userAgent = ctx.Request.UserAgent;
            string token = _auth.CreateSession(account.Id, tenantId, ipAddress, userAgent);

            // Set cookie
            ctx.Response.AppendCookie(new Cookie("session_token", token)
            {
                Path = "/",
                HttpOnly = true
            });

            await ApiServer.WriteJson(ctx, new JObject
            {
                ["token"] = token,
                ["account"] = JObject.FromObject(account),
                ["tenant_id"] = tenantId
            });
        }

        private async Task HandleLogout(HttpListenerContext ctx, AuthContext authCtx)
        {
            if (!string.IsNullOrWhiteSpace(authCtx.SessionId))
            {
                _db.DeleteAuthSession(authCtx.SessionId);
            }
            await ApiServer.WriteJson(ctx, new JObject { ["ok"] = true });
        }

        private async Task HandleGetSession(HttpListenerContext ctx, AuthContext authCtx)
        {
            await ApiServer.WriteJson(ctx, new JObject
            {
                ["account_id"] = authCtx.AccountId,
                ["tenant_id"] = authCtx.TenantId,
                ["account"] = JObject.FromObject(authCtx.Account)
            });
        }

        // ────────────────────────────────────────────────────────────
        // Account handlers
        // ────────────────────────────────────────────────────────────

        private async Task HandleGetAccount(HttpListenerContext ctx, AuthContext authCtx)
        {
            await ApiServer.WriteJson(ctx, JObject.FromObject(authCtx.Account));
        }

        // ────────────────────────────────────────────────────────────
        // Agent handlers
        // ────────────────────────────────────────────────────────────

        private async Task HandleListAgents(HttpListenerContext ctx, AuthContext authCtx)
        {
            var agents = _db.ListAgents(authCtx.TenantId);
            await ApiServer.WriteJson(ctx, new JObject
            {
                ["agents"] = JArray.FromObject(agents)
            });
        }

        private async Task HandleCreateAgent(HttpListenerContext ctx, AuthContext authCtx)
        {
            string body = await ApiServer.ReadBody(ctx);
            JObject req;
            try { req = JObject.Parse(body); }
            catch { throw new ApiException(400, "invalid_json", "Invalid JSON body"); }

            string slug = req["slug"]?.ToString();
            string displayName = req["display_name"]?.ToString() ?? req["name"]?.ToString();
            string description = req["description"]?.ToString();
            string instructions = req["instructions"]?.ToString();
            string model = req["model"]?.ToString() ?? "gpt-4.1-mini";

            if (string.IsNullOrWhiteSpace(slug))
                slug = Guid.NewGuid().ToString("N").Substring(0, 8);

            var agent = new Models.Agent
            {
                Id = IdGenerator.NewAgentId(),
                TenantId = authCtx.TenantId,
                Slug = slug,
                DisplayName = displayName ?? slug,
                Description = description,
                Status = "active",
                CreatedBy = authCtx.AccountId
            };

            _db.InsertAgent(agent);

            // Create initial revision
            var revision = new AgentRevision
            {
                Id = IdGenerator.NewRevisionId(),
                AgentId = agent.Id,
                Version = 1,
                Instructions = instructions ?? "You are a helpful AI assistant.",
                Model = model,
                Temperature = 0.7,
                MaxTokens = 4096,
                ToolPolicy = "auto",
                MemoryPolicy = "observe",
                IsActive = true,
                CreatedBy = authCtx.AccountId
            };

            _db.InsertRevision(revision);

            ctx.Response.StatusCode = 201;
            await ApiServer.WriteJson(ctx, JObject.FromObject(agent));
        }

        private async Task HandleGetAgent(HttpListenerContext ctx, AuthContext authCtx, string id)
        {
            var agent = _db.GetAgent(id);
            if (agent == null)
                throw new ApiException(404, "not_found", "Agent not found");

            var revision = _db.GetActiveRevision(id);
            var result = JObject.FromObject(agent);
            if (revision != null)
            {
                result["active_revision"] = JObject.FromObject(revision);
            }

            await ApiServer.WriteJson(ctx, result);
        }

        private async Task HandleUpdateAgent(HttpListenerContext ctx, AuthContext authCtx, string id)
        {
            var agent = _db.GetAgent(id);
            if (agent == null)
                throw new ApiException(404, "not_found", "Agent not found");

            string body = await ApiServer.ReadBody(ctx);
            JObject req;
            try { req = JObject.Parse(body); }
            catch { throw new ApiException(400, "invalid_json", "Invalid JSON body"); }

            if (req["slug"] != null) agent.Slug = req["slug"].ToString();
            if (req["display_name"] != null) agent.DisplayName = req["display_name"].ToString();
            if (req["description"] != null) agent.Description = req["description"].ToString();
            if (req["status"] != null) agent.Status = req["status"].ToString();

            _db.UpdateAgent(agent);
            await ApiServer.WriteJson(ctx, JObject.FromObject(agent));
        }

        private async Task HandleDeleteAgent(HttpListenerContext ctx, AuthContext authCtx, string id)
        {
            var agent = _db.GetAgent(id);
            if (agent == null)
                throw new ApiException(404, "not_found", "Agent not found");

            _db.DeleteAgent(id);
            await ApiServer.WriteJson(ctx, new JObject { ["ok"] = true, ["id"] = id });
        }

        // ────────────────────────────────────────────────────────────
        // Session handlers
        // ────────────────────────────────────────────────────────────

        private async Task HandleCreateSession(HttpListenerContext ctx, AuthContext authCtx)
        {
            string body = await ApiServer.ReadBody(ctx);
            JObject req;
            try { req = JObject.Parse(body); }
            catch { req = new JObject(); }

            string agentId = req["agent_id"]?.ToString();
            string title = req["title"]?.ToString();

            var session = new WorkSession
            {
                Id = IdGenerator.NewSessionId(),
                TenantId = authCtx.TenantId,
                AgentId = agentId,
                Title = title ?? "New Session",
                Status = "active",
                CreatedBy = authCtx.AccountId
            };

            _db.InsertWorkSession(session);

            // Create a default thread
            var thread = new SessionThread
            {
                Id = IdGenerator.NewThreadId(),
                SessionId = session.Id,
                AgentId = agentId,
                Title = "Main Thread",
                Status = "active"
            };

            _db.InsertThread(thread);

            session.Threads = new List<SessionThread> { thread };

            ctx.Response.StatusCode = 201;
            await ApiServer.WriteJson(ctx, JObject.FromObject(session));
        }

        private async Task HandleBootstrapSession(HttpListenerContext ctx, AuthContext authCtx,
            CancellationToken ct)
        {
            string body = await ApiServer.ReadBody(ctx);
            JObject req;
            try { req = JObject.Parse(body); }
            catch { req = new JObject(); }

            string agentId = req["agent_id"]?.ToString() ?? "agt_seed_assistant";
            string title = req["title"]?.ToString();
            string firstMessage = req["message"]?.ToString();

            // Create session
            var session = new WorkSession
            {
                Id = IdGenerator.NewSessionId(),
                TenantId = authCtx.TenantId,
                AgentId = agentId,
                Title = title ?? "Chat Session",
                Status = "active",
                CreatedBy = authCtx.AccountId
            };

            _db.InsertWorkSession(session);

            // Create thread
            var thread = new SessionThread
            {
                Id = IdGenerator.NewThreadId(),
                SessionId = session.Id,
                AgentId = agentId,
                Title = "Main Thread",
                Status = "active"
            };

            _db.InsertThread(thread);

            // If there's a first message, post it and start a run
            Run run = null;
            if (!string.IsNullOrWhiteSpace(firstMessage))
            {
                var msg = new SessionMessage
                {
                    Id = IdGenerator.NewMessageId(),
                    ThreadId = thread.Id,
                    Role = "user",
                    Content = firstMessage,
                    CreatedBy = authCtx.AccountId
                };
                _db.InsertMessage(msg);

                run = CreateAndExecuteRun(authCtx, thread.Id, agentId, ct);
            }

            var result = new JObject
            {
                ["session"] = JObject.FromObject(session),
                ["thread"] = JObject.FromObject(thread)
            };

            if (run != null)
            {
                result["run"] = JObject.FromObject(run);
            }

            ctx.Response.StatusCode = 201;
            await ApiServer.WriteJson(ctx, result);
        }

        // ────────────────────────────────────────────────────────────
        // Thread handlers
        // ────────────────────────────────────────────────────────────

        private async Task HandleListThreads(HttpListenerContext ctx, AuthContext authCtx)
        {
            string sessionId = ctx.Request.QueryString["session_id"];
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ApiException(400, "missing_param", "session_id query parameter is required");

            var threads = _db.ListThreadsBySession(sessionId);
            await ApiServer.WriteJson(ctx, new JObject
            {
                ["threads"] = JArray.FromObject(threads)
            });
        }

        private async Task HandleCreateThread(HttpListenerContext ctx, AuthContext authCtx)
        {
            string body = await ApiServer.ReadBody(ctx);
            JObject req;
            try { req = JObject.Parse(body); }
            catch { throw new ApiException(400, "invalid_json", "Invalid JSON body"); }

            string sessionId = req["session_id"]?.ToString();
            string agentId = req["agent_id"]?.ToString();
            string title = req["title"]?.ToString();

            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ApiException(400, "missing_fields", "session_id is required");

            var thread = new SessionThread
            {
                Id = IdGenerator.NewThreadId(),
                SessionId = sessionId,
                AgentId = agentId,
                Title = title ?? "Thread",
                Status = "active"
            };

            _db.InsertThread(thread);

            ctx.Response.StatusCode = 201;
            await ApiServer.WriteJson(ctx, JObject.FromObject(thread));
        }

        private async Task HandleThreadInteract(HttpListenerContext ctx, AuthContext authCtx,
            string threadId, CancellationToken ct)
        {
            var thread = _db.GetThread(threadId);
            if (thread == null)
                throw new ApiException(404, "not_found", "Thread not found");

            string body = await ApiServer.ReadBody(ctx);
            JObject req;
            try { req = JObject.Parse(body); }
            catch { throw new ApiException(400, "invalid_json", "Invalid JSON body"); }

            string message = req["message"]?.ToString();
            string agentId = req["agent_id"]?.ToString() ?? thread.AgentId ?? "agt_seed_assistant";

            // Post user message
            if (!string.IsNullOrWhiteSpace(message))
            {
                var msg = new SessionMessage
                {
                    Id = IdGenerator.NewMessageId(),
                    ThreadId = threadId,
                    Role = "user",
                    Content = message,
                    CreatedBy = authCtx.AccountId
                };
                _db.InsertMessage(msg);
            }

            // Create and execute run
            var run = CreateAndExecuteRun(authCtx, threadId, agentId, ct);

            await ApiServer.WriteJson(ctx, JObject.FromObject(run));
        }

        private async Task HandlePostMessage(HttpListenerContext ctx, AuthContext authCtx,
            string threadId)
        {
            var thread = _db.GetThread(threadId);
            if (thread == null)
                throw new ApiException(404, "not_found", "Thread not found");

            string body = await ApiServer.ReadBody(ctx);
            JObject req;
            try { req = JObject.Parse(body); }
            catch { throw new ApiException(400, "invalid_json", "Invalid JSON body"); }

            string role = req["role"]?.ToString() ?? "user";
            string content = req["content"]?.ToString();

            if (string.IsNullOrWhiteSpace(content))
                throw new ApiException(400, "missing_fields", "content is required");

            var msg = new SessionMessage
            {
                Id = IdGenerator.NewMessageId(),
                ThreadId = threadId,
                Role = role,
                Content = content,
                Metadata = req["metadata"]?.ToString(),
                CreatedBy = authCtx.AccountId
            };

            _db.InsertMessage(msg);

            ctx.Response.StatusCode = 201;
            await ApiServer.WriteJson(ctx, JObject.FromObject(msg));
        }

        private async Task HandleGetMessages(HttpListenerContext ctx, AuthContext authCtx,
            string threadId)
        {
            var thread = _db.GetThread(threadId);
            if (thread == null)
                throw new ApiException(404, "not_found", "Thread not found");

            var messages = _db.ListMessagesByThread(threadId);
            await ApiServer.WriteJson(ctx, new JObject
            {
                ["messages"] = JArray.FromObject(messages)
            });
        }

        private async Task HandleUpdateMemory(HttpListenerContext ctx, AuthContext authCtx,
            string threadId)
        {
            var thread = _db.GetThread(threadId);
            if (thread == null)
                throw new ApiException(404, "not_found", "Thread not found");

            string body = await ApiServer.ReadBody(ctx);
            JObject req;
            try { req = JObject.Parse(body); }
            catch { throw new ApiException(400, "invalid_json", "Invalid JSON body"); }

            string type = req["type"]?.ToString() ?? "observation";
            string content = req["content"]?.ToString();
            string keywords = req["keywords"]?.ToString();
            string scope = req["scope"]?.ToString() ?? "thread_shared";

            if (string.IsNullOrWhiteSpace(content))
                throw new ApiException(400, "missing_fields", "content is required");

            MemoryRecord record;
            if (type == "reflection")
            {
                double confidence = 1.0;
                JToken confToken = req["confidence"];
                if (confToken != null)
                {
                    double parsed;
                    if (double.TryParse(confToken.ToString(), out parsed))
                        confidence = parsed;
                }

                record = _memory.CreateReflection(authCtx.TenantId, thread.AgentId, threadId,
                    thread.SessionId, null, scope, content, keywords, confidence);
            }
            else
            {
                record = _memory.CreateObservation(authCtx.TenantId, thread.AgentId, threadId,
                    thread.SessionId, null, scope, content, keywords);
            }

            ctx.Response.StatusCode = 201;
            await ApiServer.WriteJson(ctx, JObject.FromObject(record));
        }

        private async Task HandleGetMemory(HttpListenerContext ctx, AuthContext authCtx,
            string threadId)
        {
            var thread = _db.GetThread(threadId);
            if (thread == null)
                throw new ApiException(404, "not_found", "Thread not found");

            var memories = _memory.GetThreadMemory(threadId);
            await ApiServer.WriteJson(ctx, new JObject
            {
                ["memories"] = JArray.FromObject(memories)
            });
        }

        // ────────────────────────────────────────────────────────────
        // Run handlers
        // ────────────────────────────────────────────────────────────

        private async Task HandleExecuteRun(HttpListenerContext ctx, AuthContext authCtx,
            string runId, CancellationToken ct)
        {
            var run = _db.GetRun(runId);
            if (run == null)
                throw new ApiException(404, "not_found", "Run not found");

            if (run.Status != "pending")
                throw new ApiException(409, "invalid_state", "Run is not in pending state");

            var result = await _runner.ExecuteRunAsync(run, ct);
            await ApiServer.WriteJson(ctx, JObject.FromObject(result));
        }

        private async Task HandleResumeRun(HttpListenerContext ctx, AuthContext authCtx,
            string runId, CancellationToken ct)
        {
            var run = _db.GetRun(runId);
            if (run == null)
                throw new ApiException(404, "not_found", "Run not found");

            if (run.Status != "waiting")
                throw new ApiException(409, "invalid_state", "Run is not in waiting state");

            string body = await ApiServer.ReadBody(ctx);
            JObject req;
            try { req = JObject.Parse(body); }
            catch { req = new JObject(); }

            // Resume by re-executing
            run.Status = "pending";
            _db.UpdateRunStatus(run.Id, "pending", null);

            var result = await _runner.ExecuteRunAsync(run, ct);
            await ApiServer.WriteJson(ctx, JObject.FromObject(result));
        }

        private async Task HandleCancelRun(HttpListenerContext ctx, AuthContext authCtx,
            string runId)
        {
            var run = _db.GetRun(runId);
            if (run == null)
                throw new ApiException(404, "not_found", "Run not found");

            _db.UpdateRunStatus(run.Id, "cancelled", null);
            run.Status = "cancelled";

            await ApiServer.WriteJson(ctx, JObject.FromObject(run));
        }

        // ────────────────────────────────────────────────────────────
        // File handlers
        // ────────────────────────────────────────────────────────────

        private async Task HandleListFiles(HttpListenerContext ctx, AuthContext authCtx)
        {
            var files = _db.ListFiles(authCtx.TenantId);
            await ApiServer.WriteJson(ctx, new JObject
            {
                ["files"] = JArray.FromObject(files)
            });
        }

        private async Task HandleGetFileContent(HttpListenerContext ctx, AuthContext authCtx,
            string fileId)
        {
            var file = _db.GetFile(fileId);
            if (file == null)
                throw new ApiException(404, "not_found", "File not found");

            if (!File.Exists(file.StoragePath))
                throw new ApiException(404, "not_found", "File content not found on disk");

            ctx.Response.ContentType = file.MimeType;
            ctx.Response.AddHeader("Content-Disposition",
                string.Format("attachment; filename=\"{0}\"", file.Filename));

            byte[] bytes = File.ReadAllBytes(file.StoragePath);
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        // ────────────────────────────────────────────────────────────
        // Event stream (SSE)
        // ────────────────────────────────────────────────────────────

        private async Task HandleEventStream(HttpListenerContext ctx, CancellationToken ct)
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.AddHeader("Cache-Control", "no-cache");
            ctx.Response.AddHeader("Connection", "keep-alive");
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.SendChunked = true;

            var stream = ctx.Response.OutputStream;

            // Send initial ping
            await ApiServer.WriteSseEvent(stream, "ping", new { timestamp = DateTime.UtcNow.ToString("o") });

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Poll for undelivered events
                    var events = _db.GetUndeliveredEvents(50);
                    foreach (var evt in events)
                    {
                        await ApiServer.WriteSseEvent(stream, evt.EventType, new
                        {
                            id = evt.Id,
                            type = evt.EventType,
                            aggregate_type = evt.AggregateType,
                            aggregate_id = evt.AggregateId,
                            payload = evt.Payload,
                            created_at = evt.CreatedAt
                        });
                        _db.MarkEventDelivered(evt.Id);
                    }

                    // Also drain the in-memory queue
                    DomainEvent queuedEvt;
                    while (_eventQueue.TryDequeue(out queuedEvt))
                    {
                        await ApiServer.WriteSseEvent(stream, queuedEvt.EventType, new
                        {
                            id = queuedEvt.Id,
                            type = queuedEvt.EventType,
                            aggregate_type = queuedEvt.AggregateType,
                            aggregate_id = queuedEvt.AggregateId,
                            payload = queuedEvt.Payload,
                            created_at = queuedEvt.CreatedAt
                        });
                    }

                    // Send keepalive
                    await ApiServer.WriteSseEvent(stream, "ping",
                        new { timestamp = DateTime.UtcNow.ToString("o") });

                    await Task.Delay(2000, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception)
            {
                // Client disconnected
            }
            finally
            {
                try { ctx.Response.Close(); }
                catch { }
            }
        }

        // ────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────

        private AuthContext Authenticate(HttpListenerContext ctx)
        {
            string authHeader = ctx.Request.Headers["Authorization"];
            string cookieHeader = ctx.Request.Headers["Cookie"];
            string xAccountId = ctx.Request.Headers["X-Account-Id"];
            string xTenantId = ctx.Request.Headers["X-Tenant-Id"];

            return _auth.Authenticate(authHeader, cookieHeader, xAccountId, xTenantId);
        }

        private Run CreateAndExecuteRun(AuthContext authCtx, string threadId, string agentId,
            CancellationToken ct)
        {
            var run = new Run
            {
                Id = IdGenerator.NewRunId(),
                TenantId = authCtx.TenantId,
                ThreadId = threadId,
                AgentId = agentId,
                Status = "pending",
                TurnCount = 0,
                MaxTurns = 10
            };

            _db.InsertRun(run);

            // Execute asynchronously but wait for result
            try
            {
                var result = _runner.ExecuteRunAsync(run, ct).GetAwaiter().GetResult();
                return result;
            }
            catch (Exception ex)
            {
                run.Status = "failed";
                run.Error = ex.Message;
                _db.UpdateRunStatus(run.Id, run.Status, run.Error);
                return run;
            }
        }

        private static string ExtractPathSegment(string path, string prefix, string suffix)
        {
            int start = prefix.Length;
            int end = path.Length - suffix.Length;
            if (end > start)
                return path.Substring(start, end - start);
            return string.Empty;
        }
    }
}
