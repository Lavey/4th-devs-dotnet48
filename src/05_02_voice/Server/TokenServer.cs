using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.VoiceAgent.Core;
using Newtonsoft.Json;

namespace FourthDevs.VoiceAgent.Server
{
    /// <summary>
    /// HttpListener-based HTTP server that issues LiveKit tokens and
    /// serves the voice-agent dashboard from the dashboard/ directory.
    /// </summary>
    internal sealed class TokenServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly string _dashboardDir;
        private readonly int _port;
        private readonly string _livekitApiKey;
        private readonly string _livekitApiSecret;
        private readonly string _livekitUrl;
        private CancellationTokenSource _cts;

        private static readonly Dictionary<string, string> MimeTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8",
            [".css"]  = "text/css; charset=utf-8",
            [".js"]   = "application/javascript; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
            [".png"]  = "image/png",
            [".jpg"]  = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"]  = "image/gif",
            [".svg"]  = "image/svg+xml",
            [".ico"]  = "image/x-icon",
            [".woff"] = "font/woff",
            [".woff2"] = "font/woff2",
            [".txt"]  = "text/plain; charset=utf-8",
            [".md"]   = "text/markdown; charset=utf-8",
        };

        public TokenServer()
        {
            _dashboardDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashboard");

            string portStr = Get("TOKEN_SERVER_PORT");
            int parsed;
            _port = int.TryParse(portStr, out parsed) && parsed > 0 ? parsed : 3310;

            _livekitApiKey    = Get("LIVEKIT_API_KEY");
            _livekitApiSecret = Get("LIVEKIT_API_SECRET");
            _livekitUrl       = Get("LIVEKIT_URL");

            if (string.IsNullOrWhiteSpace(_livekitApiKey))   _livekitApiKey   = "devkey";
            if (string.IsNullOrWhiteSpace(_livekitApiSecret)) _livekitApiSecret = "secret";
            if (string.IsNullOrWhiteSpace(_livekitUrl))       _livekitUrl       = "ws://localhost:7880";

            string prefix = string.Format("http://+:{0}/", _port);
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
        }

        public int Port { get { return _port; } }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener.Start();
            Task.Run(() => AcceptLoop(_cts.Token));
        }

        // ----------------------------------------------------------------
        //  Accept loop
        // ----------------------------------------------------------------

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    var _ = Task.Run(() => HandleRequest(ctx));
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
            }
        }

        // ----------------------------------------------------------------
        //  Request router
        // ----------------------------------------------------------------

        private async Task HandleRequest(HttpListenerContext ctx)
        {
            string path   = ctx.Request.Url.AbsolutePath;
            string method = ctx.Request.HttpMethod;

            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
            ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (method == "OPTIONS")
            {
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                return;
            }

            try
            {
                if (path == "/api/config" && method == "GET")
                {
                    await HandleConfig(ctx);
                }
                else if (path == "/api/token" && method == "GET")
                {
                    await HandleToken(ctx);
                }
                else
                {
                    await ServeStaticFile(ctx, path);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TokenServer] Error: " + ex.Message);
                try
                {
                    ctx.Response.StatusCode = 500;
                    await WriteJson(ctx, new { error = ex.Message });
                }
                catch { /* response may already be committed */ }
            }
        }

        // ----------------------------------------------------------------
        //  API: /api/config
        // ----------------------------------------------------------------

        private async Task HandleConfig(HttpListenerContext ctx)
        {
            var mode = VoiceModeResolver.Resolve();
            await WriteJson(ctx, new { agentMode = mode });
        }

        // ----------------------------------------------------------------
        //  API: /api/token
        // ----------------------------------------------------------------

        private async Task HandleToken(HttpListenerContext ctx)
        {
            var mode = VoiceModeResolver.Resolve();

            string room     = "voice-agent-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string identity = "user-" + Guid.NewGuid().ToString("N").Substring(0, 6);

            string token = TokenGenerator.GenerateToken(
                _livekitApiKey,
                _livekitApiSecret,
                identity,
                room);

            await WriteJson(ctx, new
            {
                token     = token,
                url       = _livekitUrl,
                room      = room,
                identity  = identity,
                agentMode = mode
            });
        }

        // ----------------------------------------------------------------
        //  Static files
        // ----------------------------------------------------------------

        private async Task ServeStaticFile(HttpListenerContext ctx, string path)
        {
            if (path == "/" || path == string.Empty) path = "/index.html";

            string relative  = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            string filePath  = Path.Combine(_dashboardDir, relative);

            string fullResolved  = Path.GetFullPath(filePath);
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

        // ----------------------------------------------------------------
        //  Helpers
        // ----------------------------------------------------------------

        private async Task WriteJson(HttpListenerContext ctx, object data)
        {
            string json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            });
            ctx.Response.ContentType = "application/json; charset=utf-8";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        private static string Get(string key)
        {
            return (ConfigurationManager.AppSettings[key] ?? string.Empty).Trim();
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
