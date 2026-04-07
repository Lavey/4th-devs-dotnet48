using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.ChatUi.Agent;
using FourthDevs.ChatUi.Mock;
using FourthDevs.ChatUi.Models;
using FourthDevs.ChatUi.Store;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Server
{
    /// <summary>
    /// HttpListener-based HTTP server that serves the dashboard and
    /// exposes the chat API with SSE streaming.
    /// </summary>
    internal sealed class ChatServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly string _dataDir;
        private readonly string _dashboardDir;
        private readonly ConversationStore _store;
        private readonly JsonSerializerSettings _jsonSettings;
        private CancellationTokenSource _cts;

        private static readonly Dictionary<string, string> MimeTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8",
            [".css"] = "text/css; charset=utf-8",
            [".js"] = "application/javascript; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".ico"] = "image/x-icon",
            [".woff"] = "font/woff",
            [".woff2"] = "font/woff2",
            [".txt"] = "text/plain; charset=utf-8",
            [".md"] = "text/markdown; charset=utf-8",
        };

        public ChatServer(string dataDir)
        {
            _dataDir = dataDir;
            _store = new ConversationStore();

            // dashboard/ is next to the exe
            _dashboardDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashboard");

            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            };

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://+:3300/");
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener.Start();

            // Accept connections in the background
            Task.Run(() => AcceptLoop(_cts.Token));
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    // Fire and forget each request
                    var _ = Task.Run(() => HandleRequest(ctx, ct));
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext ctx, CancellationToken ct)
        {
            string path = ctx.Request.Url.AbsolutePath;
            string method = ctx.Request.HttpMethod;

            // Add CORS headers
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
            ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (method == "OPTIONS")
            {
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                return;
            }

            try
            {
                if (path == "/api/health" && method == "GET")
                {
                    await HandleHealth(ctx);
                }
                else if (path == "/api/conversation" && method == "GET")
                {
                    await HandleGetConversation(ctx);
                }
                else if (path == "/api/reset" && method == "POST")
                {
                    await HandleReset(ctx);
                }
                else if (path == "/api/chat" && method == "POST")
                {
                    await HandleChat(ctx, ct);
                }
                else if (path.StartsWith("/api/artifacts/"))
                {
                    await HandleArtifact(ctx, path);
                }
                else
                {
                    await ServeStaticFile(ctx, path);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ChatServer] Error: " + ex.Message);
                try
                {
                    ctx.Response.StatusCode = 500;
                    await WriteJson(ctx, new JObject { ["error"] = ex.Message });
                }
                catch { /* response may already be committed */ }
            }
        }

        // ---- API Handlers ----

        private async Task HandleHealth(HttpListenerContext ctx)
        {
            await WriteJson(ctx, new JObject
            {
                ["ok"] = true,
                ["activeStream"] = _store.ActiveStream
            });
        }

        private async Task HandleGetConversation(HttpListenerContext ctx)
        {
            string modeStr = ctx.Request.QueryString["mode"];
            string historyStr = ctx.Request.QueryString["history"];

            StreamMode mode = StreamMode.mock;
            if (modeStr == "live") mode = StreamMode.live;

            int historyCount = 480;
            int parsed;
            if (int.TryParse(historyStr, out parsed) && parsed >= 0)
                historyCount = parsed;

            var snapshot = _store.GetSnapshot(mode, historyCount);
            string json = JsonConvert.SerializeObject(snapshot, _jsonSettings);
            await WriteJsonRaw(ctx, json);
        }

        private async Task HandleReset(HttpListenerContext ctx)
        {
            string body = await ReadBody(ctx);
            JObject reqBody = null;
            try { reqBody = JObject.Parse(body); }
            catch { reqBody = new JObject(); }

            string modeStr = reqBody["mode"]?.ToString();
            StreamMode mode = modeStr == "live" ? StreamMode.live : StreamMode.mock;

            _store.Reset(mode, 480);
            var snapshot = _store.GetSnapshot();
            string json = JsonConvert.SerializeObject(snapshot, _jsonSettings);
            await WriteJsonRaw(ctx, json);
        }

        private async Task HandleChat(HttpListenerContext ctx, CancellationToken ct)
        {
            if (_store.ActiveStream)
            {
                ctx.Response.StatusCode = 409;
                await WriteJson(ctx, new JObject { ["error"] = "A stream is already active" });
                return;
            }

            string body = await ReadBody(ctx);
            JObject reqBody;
            try { reqBody = JObject.Parse(body); }
            catch
            {
                ctx.Response.StatusCode = 400;
                await WriteJson(ctx, new JObject { ["error"] = "Invalid JSON body" });
                return;
            }

            string prompt = reqBody["prompt"]?.ToString();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                ctx.Response.StatusCode = 400;
                await WriteJson(ctx, new JObject { ["error"] = "prompt is required" });
                return;
            }

            string modeStr = reqBody["mode"]?.ToString();
            StreamMode mode = modeStr == "live" ? StreamMode.live : StreamMode.mock;

            // Add user message
            _store.AddUserMessage(prompt);

            // Create assistant message
            string assistantMsgId = _store.CreateAssistantMessage();

            // SSE headers
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.AddHeader("Cache-Control", "no-cache");
            ctx.Response.AddHeader("Connection", "keep-alive");
            ctx.Response.SendChunked = true;

            _store.ActiveStream = true;

            try
            {
                var outputStream = ctx.Response.OutputStream;

                if (mode == StreamMode.mock)
                {
                    await StreamMock(outputStream, assistantMsgId, ct);
                }
                else
                {
                    await StreamLive(outputStream, assistantMsgId, prompt, ct);
                }

                _store.CompleteMessage(assistantMsgId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ChatServer] Stream error: " + ex.Message);
                _store.ErrorMessage(assistantMsgId);

                try
                {
                    var errorEvt = new ErrorEvent
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 12),
                        MessageId = assistantMsgId,
                        Seq = 9999,
                        At = DateTime.UtcNow.ToString("o"),
                        Message = ex.Message
                    };
                    await WriteSseEvent(ctx.Response.OutputStream, errorEvt);
                }
                catch { /* stream may be broken */ }
            }
            finally
            {
                _store.ActiveStream = false;
                try { ctx.Response.Close(); }
                catch { }
            }
        }

        private async Task StreamMock(Stream outputStream, string assistantMsgId, CancellationToken ct)
        {
            int scenarioIndex = _store.NextMockScenarioIndex();
            var events = MockScenarios.GetScenario(scenarioIndex, assistantMsgId);

            foreach (var de in events)
            {
                if (ct.IsCancellationRequested) break;

                if (de.DelayMs > 0)
                {
                    await Task.Delay(de.DelayMs);
                }

                _store.AppendEvent(assistantMsgId, de.Event);
                await WriteSseEvent(outputStream, de.Event);
            }
        }

        private async Task StreamLive(Stream outputStream, string assistantMsgId, string prompt, CancellationToken ct)
        {
            var runner = new LiveTurnRunner(_dataDir);
            var history = _store.GetHistory();

            // Remove the last message (the assistant placeholder)
            if (history.Count > 0 && history[history.Count - 1].Id == assistantMsgId)
            {
                history.RemoveAt(history.Count - 1);
            }
            // Remove the user message we just added (the runner will add it)
            if (history.Count > 0 && history[history.Count - 1].Role == MessageRole.user)
            {
                history.RemoveAt(history.Count - 1);
            }

            await runner.RunAsync(history, prompt, assistantMsgId, evt =>
            {
                _store.AppendEvent(assistantMsgId, evt);
                WriteSseEvent(outputStream, evt).GetAwaiter().GetResult();
            }, ct);
        }

        private async Task HandleArtifact(HttpListenerContext ctx, string path)
        {
            // /api/artifacts/foo/bar.md → foo/bar.md
            string relative = path.Substring("/api/artifacts/".Length);
            string filePath = Path.Combine(_dataDir, relative.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(filePath))
            {
                ctx.Response.StatusCode = 404;
                await WriteJson(ctx, new JObject { ["error"] = "Artifact not found" });
                return;
            }

            string ext = Path.GetExtension(filePath);
            string mime;
            if (!MimeTypes.TryGetValue(ext, out mime))
                mime = "application/octet-stream";

            ctx.Response.ContentType = mime;
            byte[] bytes = File.ReadAllBytes(filePath);
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        // ---- Static files ----

        private async Task ServeStaticFile(HttpListenerContext ctx, string path)
        {
            if (path == "/") path = "/index.html";

            // Sanitize
            string relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            string filePath = Path.Combine(_dashboardDir, relative);

            // Security: ensure we stay within dashboard dir
            string fullResolved = Path.GetFullPath(filePath);
            string dashboardFull = Path.GetFullPath(_dashboardDir + Path.DirectorySeparatorChar);
            if (!fullResolved.StartsWith(dashboardFull))
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.Close();
                return;
            }

            if (!File.Exists(filePath))
            {
                ctx.Response.StatusCode = 404;
                byte[] notFound = Encoding.UTF8.GetBytes("Not Found");
                ctx.Response.ContentLength64 = notFound.Length;
                await ctx.Response.OutputStream.WriteAsync(notFound, 0, notFound.Length);
                ctx.Response.Close();
                return;
            }

            string ext = Path.GetExtension(filePath);
            string mime;
            if (!MimeTypes.TryGetValue(ext, out mime))
                mime = "application/octet-stream";

            ctx.Response.ContentType = mime;
            byte[] bytes = File.ReadAllBytes(filePath);
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        // ---- Helpers ----

        private async Task WriteSseEvent(Stream stream, BaseStreamEvent evt)
        {
            string json = JsonConvert.SerializeObject(evt, _jsonSettings);
            string sseMessage = "data: " + json + "\n\n";
            byte[] bytes = Encoding.UTF8.GetBytes(sseMessage);
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }

        private async Task WriteJson(HttpListenerContext ctx, JObject obj)
        {
            await WriteJsonRaw(ctx, obj.ToString(Formatting.None));
        }

        private async Task WriteJsonRaw(HttpListenerContext ctx, string json)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        private async Task<string> ReadBody(HttpListenerContext ctx)
        {
            using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public void Dispose()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
            }
        }
    }
}
