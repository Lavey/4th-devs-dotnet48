using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.ContextAgent.Agent;
using FourthDevs.ContextAgent.Memory;
using FourthDevs.ContextAgent.Session;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ContextAgent
{
    internal static class Program
    {
        private const int Port = 3001;
        private static string _workspaceRoot;
        private static AgentTemplate _template;

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            // Resolve workspace root
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            _workspaceRoot = Path.GetFullPath(Path.Combine(exeDir, "workspace"));

            // Init subsystems
            MemoryProcessor.Init(_workspaceRoot);
            AgentTools.Init(_workspaceRoot);

            // Load agent template
            _template = AgentLoader.Load(_workspaceRoot, "alice");

            Console.WriteLine("========================================");
            Console.WriteLine("  02_05 Agent — Context Engineering Demo");
            Console.WriteLine("  http://localhost:" + Port);
            Console.WriteLine("========================================");
            Console.WriteLine();

            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(string.Format("http://+:{0}/", Port));
                listener.Start();

                while (true)
                {
                    var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                    var handlerTask = HandleRequestAsync(ctx);
#pragma warning disable 4014
                    handlerTask.ContinueWith(
                        t => Console.WriteLine("Unhandled request error: " + t.Exception?.GetBaseException()?.Message),
                        System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore 4014
                }
            }
        }

        private static async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var resp = ctx.Response;

                resp.Headers.Set("Access-Control-Allow-Origin", "*");
                resp.Headers.Set("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                resp.Headers.Set("Access-Control-Allow-Headers", "Content-Type");

                if (req.HttpMethod == "OPTIONS")
                {
                    resp.StatusCode = 204;
                    resp.Close();
                    return;
                }

                string path = req.Url.AbsolutePath.TrimEnd('/');
                string method = req.HttpMethod;

                if (method == "POST" && path == "/api/chat")
                {
                    await HandleChatAsync(ctx).ConfigureAwait(false);
                    return;
                }

                if (method == "GET" && path == "/api/sessions")
                {
                    await WriteJsonAsync(resp, 200, SessionStore.List()).ConfigureAwait(false);
                    return;
                }

                // GET /api/sessions/{id}/memory
                if (method == "GET" && path.StartsWith("/api/sessions/") && path.EndsWith("/memory"))
                {
                    string id = path.Substring("/api/sessions/".Length);
                    id = id.Substring(0, id.Length - "/memory".Length);
                    await HandleGetMemoryAsync(resp, id).ConfigureAwait(false);
                    return;
                }

                // POST /api/sessions/{id}/flush
                if (method == "POST" && path.StartsWith("/api/sessions/") && path.EndsWith("/flush"))
                {
                    string id = path.Substring("/api/sessions/".Length);
                    id = id.Substring(0, id.Length - "/flush".Length);
                    await HandleFlushAsync(resp, id).ConfigureAwait(false);
                    return;
                }

                await WriteJsonAsync(resp, 404, new { error = "Not found" }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Request error: " + ex.Message);
                try
                {
                    await WriteJsonAsync(ctx.Response, 500, new { error = "Internal server error" })
                        .ConfigureAwait(false);
                }
                catch { }
            }
        }

        private static async Task HandleChatAsync(HttpListenerContext ctx)
        {
            string body;
            using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                body = await reader.ReadToEndAsync().ConfigureAwait(false);

            JObject json;
            try { json = JObject.Parse(body); }
            catch
            {
                await WriteJsonAsync(ctx.Response, 400, new { error = "Invalid JSON body" })
                    .ConfigureAwait(false);
                return;
            }

            string message = (string)json["message"];
            string sessionId = (string)json["session_id"];
            if (string.IsNullOrWhiteSpace(sessionId))
                sessionId = Guid.NewGuid().ToString("N");

            if (string.IsNullOrWhiteSpace(message))
            {
                await WriteJsonAsync(ctx.Response, 400, new { error = "'message' is required" })
                    .ConfigureAwait(false);
                return;
            }

            var session = SessionStore.GetOrCreate(sessionId);
            AgentRunResult result;
            try
            {
                result = await AgentRunner.RunAsync(session, message, _template)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await WriteJsonAsync(ctx.Response, 500, new { error = ex.Message })
                    .ConfigureAwait(false);
                return;
            }

            var mem = session.Memory;
            int sealedMsgs = mem.LastObservedIndex;
            int activeMsgs = session.Messages.Count - sealedMsgs;

            await WriteJsonAsync(ctx.Response, 200, new
            {
                session_id = sessionId,
                response = result.Response,
                memory = new
                {
                    hasObservations = !string.IsNullOrEmpty(mem.ActiveObservations),
                    observationTokens = mem.ObservationTokenCount,
                    generation = mem.GenerationCount,
                    totalMessages = session.Messages.Count,
                    sealedMessages = sealedMsgs,
                    activeMessages = activeMsgs
                },
                usage = new
                {
                    totalEstimatedTokens = result.TotalEstimatedTokens,
                    totalActualTokens = result.TotalActualTokens,
                    turns = result.Turns
                }
            }).ConfigureAwait(false);
        }

        private static async Task HandleGetMemoryAsync(HttpListenerResponse resp, string sessionId)
        {
            var session = SessionStore.Get(sessionId);
            if (session == null)
            {
                await WriteJsonAsync(resp, 404, new { error = "Session not found" })
                    .ConfigureAwait(false);
                return;
            }

            var mem = session.Memory;
            await WriteJsonAsync(resp, 200, new
            {
                session_id = sessionId,
                messageCount = session.Messages.Count,
                memory = new
                {
                    activeObservations = mem.ActiveObservations,
                    lastObservedIndex = mem.LastObservedIndex,
                    observationTokenCount = mem.ObservationTokenCount,
                    generationCount = mem.GenerationCount
                }
            }).ConfigureAwait(false);
        }

        private static async Task HandleFlushAsync(HttpListenerResponse resp, string sessionId)
        {
            var session = SessionStore.Get(sessionId);
            if (session == null)
            {
                await WriteJsonAsync(resp, 404, new { error = "Session not found" })
                    .ConfigureAwait(false);
                return;
            }

            try
            {
                await MemoryProcessor.FlushAsync(session).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await WriteJsonAsync(resp, 500, new { error = ex.Message })
                    .ConfigureAwait(false);
                return;
            }

            var mem = session.Memory;
            await WriteJsonAsync(resp, 200, new
            {
                session_id = sessionId,
                memory = new
                {
                    sealedMessages = mem.LastObservedIndex,
                    activeMessages = session.Messages.Count - mem.LastObservedIndex,
                    generation = mem.GenerationCount,
                    observationTokens = mem.ObservationTokenCount
                }
            }).ConfigureAwait(false);
        }

        private static async Task WriteJsonAsync(HttpListenerResponse resp, int statusCode, object data)
        {
            resp.StatusCode = statusCode;
            resp.ContentType = "application/json; charset=utf-8";
            string json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            byte[] buf = Encoding.UTF8.GetBytes(json);
            resp.ContentLength64 = buf.Length;
            await resp.OutputStream.WriteAsync(buf, 0, buf.Length).ConfigureAwait(false);
            resp.Close();
        }
    }
}
