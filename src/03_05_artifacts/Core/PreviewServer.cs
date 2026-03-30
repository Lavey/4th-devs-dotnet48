using System;
using System.Net;
using System.Text;
using System.Threading;
using FourthDevs.Artifacts.Models;
using Newtonsoft.Json;

namespace FourthDevs.Artifacts.Core
{
    internal sealed class PreviewServer : IDisposable
    {
        private readonly HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;
        private volatile ArtifactDocument _current;
        private readonly object _lock = new object();

        public string Url { get; }

        public PreviewServer(string host, int port)
        {
            Url = string.Format("http://{0}:{1}/", host, port);
            _listener = new HttpListener();
            _listener.Prefixes.Add(Url);
        }

        public void UpdateArtifact(ArtifactDocument artifact)
        {
            lock (_lock)
            {
                _current = artifact;
            }
        }

        public void Start()
        {
            _listener.Start();
            _running = true;
            _thread = new Thread(Listen) { IsBackground = true, Name = "PreviewServer" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _listener.Stop(); }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            try { _listener.Close(); }
            catch { }
        }

        private void Listen()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_running) break;
                    Console.Error.WriteLine("[preview-server] Listener error: " + ex.Message);
                    break;
                }

                // Handle each request on a thread pool thread
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { HandleRequest(ctx); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[preview-server] Error: " + ex.Message);
                    }
                });
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 204;
                resp.Close();
                return;
            }

            string path = req.Url.AbsolutePath.TrimEnd('/');
            if (string.IsNullOrEmpty(path)) path = "/";

            ArtifactDocument artifact;
            lock (_lock) { artifact = _current; }

            if (path == "" || path == "/" || path == "/index.html")
            {
                ServePreviewUi(resp, artifact);
            }
            else if (path == "/artifact")
            {
                ServeArtifactHtml(resp, artifact);
            }
            else if (path == "/api/state")
            {
                ServeApiState(resp, artifact);
            }
            else
            {
                resp.StatusCode = 404;
                resp.Close();
            }
        }

        private static void ServePreviewUi(HttpListenerResponse resp, ArtifactDocument artifact)
        {
            string title = artifact != null ? HtmlEncode(artifact.Title) : "No artifact yet";
            string html = @"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8""/>
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
  <title>Artifact Preview</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: ui-sans-serif, system-ui, sans-serif; background: #0f172a; color: #e2e8f0; display: flex; flex-direction: column; height: 100vh; }
    header { display: flex; align-items: center; justify-content: space-between; padding: 10px 16px; background: #1e293b; border-bottom: 1px solid #334155; flex-shrink: 0; }
    header h1 { font-size: 14px; font-weight: 600; color: #94a3b8; }
    #artifact-title { font-size: 14px; font-weight: 600; color: #f1f5f9; }
    button { background: #3b82f6; color: #fff; border: none; border-radius: 6px; padding: 5px 12px; font-size: 12px; cursor: pointer; }
    button:hover { background: #2563eb; }
    iframe { flex: 1; border: none; width: 100%; background: #fff; }
    #status { font-size: 11px; color: #64748b; margin-left: 12px; }
  </style>
</head>
<body>
  <header>
    <h1>Artifact Preview</h1>
    <span id=""artifact-title"">" + title + @"</span>
    <div style=""display:flex;align-items:center;gap:8px;"">
      <span id=""status"">Loading…</span>
      <button onclick=""refresh()"">↺ Refresh</button>
    </div>
  </header>
  <iframe id=""frame"" src=""/artifact""></iframe>
  <script>
    var pollInterval = 2000;
    var lastId = null;

    function refresh() {
      document.getElementById('frame').src = '/artifact?' + Date.now();
    }

    function poll() {
      fetch('/api/state')
        .then(function(r) { return r.json(); })
        .then(function(data) {
          var id = data.id || null;
          if (id && id !== lastId) {
            lastId = id;
            document.getElementById('artifact-title').textContent = data.title || 'Artifact';
            refresh();
            document.getElementById('status').textContent = 'Updated';
            setTimeout(function() { document.getElementById('status').textContent = ''; }, 2000);
          }
        })
        .catch(function() {});
    }

    setInterval(poll, pollInterval);
    poll();
  </script>
</body>
</html>";

            Respond(resp, 200, "text/html; charset=utf-8", html);
        }

        private static void ServeArtifactHtml(HttpListenerResponse resp, ArtifactDocument artifact)
        {
            if (artifact == null)
            {
                string placeholder = @"<!doctype html>
<html><head><meta charset=""UTF-8""/><title>Waiting…</title>
<style>body{background:#0f172a;color:#94a3b8;display:flex;align-items:center;justify-content:center;height:100vh;font-family:sans-serif;margin:0;}</style>
</head><body><p>No artifact yet. Ask the agent to build something!</p></body></html>";
                Respond(resp, 200, "text/html; charset=utf-8", placeholder);
            }
            else
            {
                Respond(resp, 200, "text/html; charset=utf-8", artifact.Html ?? string.Empty);
            }
        }

        private static void ServeApiState(HttpListenerResponse resp, ArtifactDocument artifact)
        {
            object state;
            if (artifact == null)
            {
                state = new { id = (string)null, title = (string)null, hasArtifact = false };
            }
            else
            {
                state = new
                {
                    id = artifact.Id,
                    title = artifact.Title,
                    model = artifact.Model,
                    packs = artifact.Packs,
                    createdAt = artifact.CreatedAt,
                    hasArtifact = true
                };
            }

            string json = JsonConvert.SerializeObject(state, Formatting.Indented);
            Respond(resp, 200, "application/json; charset=utf-8", json);
        }

        private static void Respond(HttpListenerResponse resp, int statusCode, string contentType, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            resp.StatusCode = statusCode;
            resp.ContentType = contentType;
            resp.ContentLength64 = bytes.Length;
            try
            {
                resp.OutputStream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                resp.Close();
            }
        }

        private static string HtmlEncode(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
