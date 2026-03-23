using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Evals.Agent;
using FourthDevs.Evals.Core;
using FourthDevs.Evals.Core.Tracing;
using FourthDevs.Evals.Experiments;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Evals
{
    /// <summary>
    /// Lesson 11 – Evals
    ///
    /// HTTP server with an AI chat agent that uses the Responses API
    /// and supports evaluation experiments against synthetic datasets.
    ///
    /// Modes:
    ///   (no args)       → starts HTTP server
    ///   eval            → runs all evaluations
    ///   eval:tools      → runs tool-use evaluation only
    ///   eval:response   → runs response-correctness evaluation only
    ///
    /// HTTP Endpoints:
    ///   GET  /api/health
    ///   GET  /api/sessions
    ///   POST /api/chat
    ///
    /// Source: 03_01_evals (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const int DefaultPort = 3010;

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            var logger = new Logger(new Dictionary<string, object>
            {
                { "service", "03_01_evals" }
            });

            TracingManager.Init(logger);

            // Check for eval mode
            string evalArg = args.FirstOrDefault(a =>
                a.Equals("eval", StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith("eval:", StringComparison.OrdinalIgnoreCase));

            if (evalArg != null)
            {
                await RunEvalsAsync(logger, evalArg).ConfigureAwait(false);
                return;
            }

            // Default: start HTTP server
            await StartServerAsync(logger).ConfigureAwait(false);
        }

        // -----------------------------------------------------------------
        // Evaluation mode
        // -----------------------------------------------------------------

        private static async Task RunEvalsAsync(Logger logger, string evalArg)
        {
            string mode = evalArg.Contains(":")
                ? evalArg.Substring(evalArg.IndexOf(':') + 1).ToLowerInvariant()
                : "all";

            Console.WriteLine();
            Console.WriteLine("  03_01_evals – Evaluation Mode");
            Console.WriteLine(string.Format("  Running: {0}", mode));
            Console.WriteLine();
            Console.WriteLine("  WARNING: This will send requests to the AI API and consume tokens.");
            Console.WriteLine("  Press Enter to continue or Ctrl+C to cancel...");

            // In non-interactive environments (e.g. piped input), skip the prompt
            if (Console.IsInputRedirected)
            {
                Console.WriteLine("  (non-interactive – proceeding automatically)");
            }
            else
            {
                Console.ReadLine();
            }

            if (mode == "tools" || mode == "all")
            {
                await EvalRunner.RunToolUseEval(logger).ConfigureAwait(false);
            }

            if (mode == "response" || mode == "all")
            {
                await EvalRunner.RunResponseCorrectnessEval(logger).ConfigureAwait(false);
            }

            Console.WriteLine("  Evaluation complete.");
        }

        // -----------------------------------------------------------------
        // HTTP Server mode
        // -----------------------------------------------------------------

        private static async Task StartServerAsync(Logger logger)
        {
            int port = DefaultPort;
            string host = string.Format("http://localhost:{0}", port);

            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(string.Format("http://+:{0}/", port));
                listener.Start();

                logger.Info("Server started", new Dictionary<string, object> { { "port", port } });

                Console.WriteLine();
                Console.WriteLine("  03_01_evals server listening on " + host);
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
                    var _ = HandleRequestAsync(context, logger);
                }
            }
        }

        // -----------------------------------------------------------------
        // Request routing
        // -----------------------------------------------------------------

        private static async Task HandleRequestAsync(
            HttpListenerContext ctx,
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
                        ok = true,
                        service = "03_01_evals",
                        tracing = TracingManager.IsActive ? "configured" : "not_configured"
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
                    await HandleChatAsync(ctx, logger).ConfigureAwait(false);
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
            string sessionId = (string)json["session_id"] ?? Guid.NewGuid().ToString("N");

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
                    var agentResult = await AgentRunner.RunAsync(reqLogger, session, message)
                        .ConfigureAwait(false);
                    Tracer.SetTraceOutput(agentResult.Response);
                    return agentResult;
                }).ConfigureAwait(false);

            await WriteJsonAsync(ctx.Response, 200, new
            {
                session_id = sessionId,
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
