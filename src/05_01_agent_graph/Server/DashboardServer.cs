using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Core;
using FourthDevs.AgentGraph.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentGraph.Server
{
    public sealed class DashboardServer : IDisposable
    {
        private readonly Runtime _rt;
        private readonly HttpListener _listener;
        private readonly string _dashboardDir;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public string Url { get; }

        public DashboardServer(Runtime rt, int port = 3300)
        {
            _rt = rt;
            Url = "http://localhost:" + port + "/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(Url);

            // Dashboard files are alongside the assembly
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _dashboardDir = Path.Combine(baseDir, "dashboard");
            if (!Directory.Exists(_dashboardDir))
            {
                // Fallback: look in the project directory (for debugging)
                var projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
                var altDir = Path.Combine(projectDir, "dashboard");
                if (Directory.Exists(altDir)) _dashboardDir = altDir;
            }
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine("[ui] dashboard at " + Url);
            // Start accepting requests in background
            Task.Run(() => AcceptLoop());
        }

        private async Task AcceptLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    // Fire and forget
                    var _ = Task.Run(() => HandleRequest(ctx));
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex) { Console.Error.WriteLine("[ui] accept error: " + ex.Message); }
            }
        }

        private async Task HandleRequest(HttpListenerContext ctx)
        {
            var path = ctx.Request.Url.AbsolutePath;

            try
            {
                if (path == "/")
                {
                    await ServeFile(ctx.Response, Path.Combine(_dashboardDir, "index.html"));
                    return;
                }

                if (path.StartsWith("/dashboard/"))
                {
                    var file = path.Substring("/dashboard/".Length);
                    await ServeFile(ctx.Response, Path.Combine(_dashboardDir, file));
                    return;
                }

                if (path == "/events")
                {
                    await HandleSse(ctx);
                    return;
                }

                if (path == "/api/state")
                {
                    await SendJson(ctx.Response, 200, await GetState());
                    return;
                }

                if (path.StartsWith("/api/artifact/"))
                {
                    var artPath = Uri.UnescapeDataString(path.Substring("/api/artifact/".Length));
                    await HandleArtifactRead(ctx.Response, artPath);
                    return;
                }

                SendText(ctx.Response, 404, "Not found");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ui] request failed: " + ex.Message);
                try { SendText(ctx.Response, 500, "Internal server error"); } catch { /* ignore */ }
            }
        }

        private async Task HandleSse(HttpListenerContext ctx)
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Add("Cache-Control", "no-cache");
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            ctx.Response.SendChunked = true;

            var stream = ctx.Response.OutputStream;
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            await writer.WriteAsync(": connected\n\n");
            await writer.FlushAsync();

            // Replay buffered events
            foreach (var past in EventBus.Replay())
            {
                await writer.WriteAsync("data: " + JsonConvert.SerializeObject(past) + "\n\n");
            }
            await writer.FlushAsync();

            // Subscribe to new events
            var tcs = new TaskCompletionSource<bool>();
            var unsub = EventBus.Subscribe(evt =>
            {
                try
                {
                    writer.Write("data: " + JsonConvert.SerializeObject(evt) + "\n\n");
                    writer.Flush();
                }
                catch { tcs.TrySetResult(true); }
            });

            // Wait until client disconnects or cancellation
            using (_cts.Token.Register(() => tcs.TrySetResult(true)))
            {
                await tcs.Task;
            }

            unsub();
            try { ctx.Response.Close(); } catch { /* ignore */ }
        }

        private async Task HandleArtifactRead(HttpListenerResponse response, string artPath)
        {
            var filePath = Path.Combine(_rt.DataDir, "files", artPath.Replace('/', Path.DirectorySeparatorChar));
            // Prevent path traversal
            var fullPath = Path.GetFullPath(filePath);
            var baseDir = Path.GetFullPath(Path.Combine(_rt.DataDir, "files"));
            if (!fullPath.StartsWith(baseDir))
            {
                await SendJson(response, 404, new { path = artPath, content = (string)null, error = "Not found" });
                return;
            }

            try
            {
                var content = File.ReadAllText(fullPath);
                await SendJson(response, 200, new { path = artPath, content });
            }
            catch
            {
                await SendJson(response, 404, new { path = artPath, content = (string)null, error = "Not found" });
            }
        }

        private async Task<object> GetState()
        {
            var sessions = await _rt.Sessions.All();
            var actors = await _rt.Actors.All();
            var tasks = await _rt.Tasks.All();
            var items = await _rt.Items.All();
            var artifacts = await _rt.Artifacts.All();
            var relations = await _rt.Relations.All();
            return new { sessions, actors, tasks, items, artifacts, relations };
        }

        private static readonly Dictionary<string, string> MimeTypes = new Dictionary<string, string>
        {
            [".html"] = "text/html; charset=utf-8",
            [".css"] = "text/css; charset=utf-8",
            [".js"] = "text/javascript; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
        };

        private static async Task ServeFile(HttpListenerResponse response, string filePath)
        {
            if (!File.Exists(filePath))
            {
                SendText(response, 404, "Not found");
                return;
            }
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string mime;
            if (!MimeTypes.TryGetValue(ext, out mime)) mime = "application/octet-stream";
            response.ContentType = mime;
            var content = File.ReadAllBytes(filePath);
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content, 0, content.Length);
            response.Close();
        }

        private static async Task SendJson(HttpListenerResponse response, int statusCode, object data)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            var json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Include,
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            response.Close();
        }

        private static void SendText(HttpListenerResponse response, int statusCode, string body)
        {
            response.StatusCode = statusCode;
            response.ContentType = "text/plain; charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(body);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { /* ignore */ }
            try { _listener.Close(); } catch { /* ignore */ }
        }
    }
}
