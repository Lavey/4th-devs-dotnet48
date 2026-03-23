using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Observability.Agent;
using FourthDevs.Observability.Core;
using FourthDevs.Observability.Core.Tracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Observability
{
    /// <summary>
    /// Lesson 11 – Observability
    ///
    /// HTTP server with an AI chat agent that demonstrates observability and tracing
    /// with Langfuse integration. The agent has two tools (get_current_time, sum_numbers)
    /// and maintains conversation sessions.
    ///
    /// Endpoints:
    ///   GET  /api/health
    ///   GET  /api/sessions
    ///   POST /api/chat
    ///
    /// Source: 03_01_observability/src/index.ts (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const int DefaultPort = 3000;

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var logger = new Logger(new Dictionary<string, object>
            {
                { "service", "03_01_observability" }
            });

            TracingManager.Init(logger);

            int port = DefaultPort;
            string host = string.Format("http://localhost:{0}", port);

            using (var client = new ChatCompletionsClient())
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(string.Format("http://+:{0}/", port));
                listener.Start();

                logger.Info("Server started", new Dictionary<string, object> { { "port", port } });

                Console.WriteLine();
                Console.WriteLine("  03_01_observability server listening on " + host);
                Console.WriteLine();
                Console.WriteLine("  Endpoints:");
                Console.WriteLine("    GET  " + host + "/api/health");
                Console.WriteLine("    GET  " + host + "/api/sessions");
                Console.WriteLine("    POST " + host + "/api/chat");
                Console.WriteLine();

                while (true)
                {
                    var context = await listener.GetContextAsync().ConfigureAwait(false);
                    // Fire-and-forget: handle each request on the thread-pool
                    var _ = HandleRequestAsync(context, client, logger);
                }
            }
        }

        // -----------------------------------------------------------------
        // Request routing
        // -----------------------------------------------------------------

        private static async Task HandleRequestAsync(
            HttpListenerContext ctx,
            ChatCompletionsClient client,
            Logger logger)
        {
            try
            {
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                // CORS headers
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

                if (method == "GET" && path == "/api/health")
                {
                    await WriteJsonAsync(resp, 200, new
                    {
                        status = "ok",
                        service = "03_01_observability"
                    }).ConfigureAwait(false);
                    return;
                }

                if (method == "GET" && path == "/api/sessions")
                {
                    await WriteJsonAsync(resp, 200, new
                    {
                        data = SessionStore.ListSessions()
                    }).ConfigureAwait(false);
                    return;
                }

                if (method == "POST" && path == "/api/chat")
                {
                    await HandleChatAsync(ctx, client, logger).ConfigureAwait(false);
                    return;
                }

                // 404
                await WriteJsonAsync(resp, 404, new { error = "Not found" }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Error("Unhandled request error", new Dictionary<string, object>
                {
                    { "error", ex.Message }
                });

                try
                {
                    await WriteJsonAsync(ctx.Response, 500, new
                    {
                        error = "Internal server error"
                    }).ConfigureAwait(false);
                }
                catch
                {
                    // Response may already be closed
                }
            }
        }

        // -----------------------------------------------------------------
        // POST /api/chat handler
        // -----------------------------------------------------------------

        private static async Task HandleChatAsync(
            HttpListenerContext ctx,
            ChatCompletionsClient client,
            Logger logger)
        {
            string body;
            using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            JObject json;
            try
            {
                json = JObject.Parse(body);
            }
            catch
            {
                await WriteJsonAsync(ctx.Response, 400, new
                {
                    error = "Invalid JSON body"
                }).ConfigureAwait(false);
                return;
            }

            string message = (string)json["message"];
            string sessionId = (string)json["sessionId"] ?? Guid.NewGuid().ToString("N");

            if (string.IsNullOrWhiteSpace(message))
            {
                await WriteJsonAsync(ctx.Response, 400, new
                {
                    error = "'message' field is required"
                }).ConfigureAwait(false);
                return;
            }

            var session = SessionStore.GetSession(sessionId);
            var reqLogger = logger.Child(new Dictionary<string, object>
            {
                { "sessionId", sessionId }
            });

            reqLogger.Info("Chat request received", new Dictionary<string, object>
            {
                { "message", message }
            });

            var result = await Tracer.WithTrace(
                new TraceParams
                {
                    Name = "chat",
                    SessionId = sessionId,
                    Input = message
                },
                async () =>
                {
                    var agentResult = await AgentRunner.RunAsync(client, reqLogger, session, message)
                        .ConfigureAwait(false);
                    Tracer.SetTraceOutput(agentResult.Response);
                    return agentResult;
                }).ConfigureAwait(false);

            await WriteJsonAsync(ctx.Response, 200, new
            {
                sessionId = sessionId,
                response = result.Response,
                turns = result.Turns,
                usage = result.Usage
            }).ConfigureAwait(false);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

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
