using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FourthDevs.ChatApp.Server
{
    /// <summary>
    /// HttpListener-based HTTP server that serves the dashboard and
    /// proxies /api/* requests to the 05_04_api backend.
    /// </summary>
    internal sealed class UiServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly string _dashboardDir;
        private readonly string _apiBaseUrl;
        private readonly HttpClient _httpClient;
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

        public UiServer(int port, string apiBaseUrl)
        {
            _apiBaseUrl = apiBaseUrl.TrimEnd('/');
            _dashboardDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashboard");

            var handler = new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://+:" + port + "/");
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener.Start();
            Task.Run(() => AcceptLoop(_cts.Token));
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    var _ = Task.Run(() => HandleRequest(ctx, ct));
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
            }
        }

        private async Task HandleRequest(HttpListenerContext ctx, CancellationToken ct)
        {
            string path = ctx.Request.Url.AbsolutePath;
            string method = ctx.Request.HttpMethod;

            // CORS headers
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET,POST,PUT,PATCH,DELETE,OPTIONS");
            ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type,Authorization,X-Tenant-Id,X-Account-Id");
            ctx.Response.AddHeader("Access-Control-Allow-Credentials", "true");

            if (method == "OPTIONS")
            {
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                return;
            }

            try
            {
                if (path.StartsWith("/api/"))
                {
                    await ProxyToApi(ctx, path, ct);
                }
                else
                {
                    await ServeStaticFile(ctx, path);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[UiServer] Error: " + ex.Message);
                try
                {
                    ctx.Response.StatusCode = 502;
                    byte[] body = Encoding.UTF8.GetBytes("{\"error\":\"Proxy error: " + ex.Message.Replace("\"", "'") + "\"}");
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.ContentLength64 = body.Length;
                    await ctx.Response.OutputStream.WriteAsync(body, 0, body.Length);
                    ctx.Response.Close();
                }
                catch { /* response may already be committed */ }
            }
        }

        // ---- API Proxy ----

        private async Task ProxyToApi(HttpListenerContext ctx, string path, CancellationToken ct)
        {
            // /api/auth/login → /v1/auth/login
            // Strip "/api" prefix and prepend the API base URL
            string apiPath = path.Substring(4); // remove "/api"
            string targetUrl = _apiBaseUrl + apiPath;

            // Append query string if present
            string query = ctx.Request.Url.Query;
            if (!string.IsNullOrEmpty(query))
                targetUrl += query;

            var request = new HttpRequestMessage(
                new HttpMethod(ctx.Request.HttpMethod), targetUrl);

            // Forward headers
            foreach (string header in ctx.Request.Headers)
            {
                string lower = header.ToLowerInvariant();
                if (lower == "host" || lower == "connection" || lower == "content-length"
                    || lower == "transfer-encoding")
                    continue;

                string val = ctx.Request.Headers[header];
                request.Headers.TryAddWithoutValidation(header, val);
            }

            // Forward cookies as Cookie header
            if (ctx.Request.Cookies.Count > 0)
            {
                var cookieParts = new List<string>();
                foreach (Cookie cookie in ctx.Request.Cookies)
                {
                    cookieParts.Add(cookie.Name + "=" + cookie.Value);
                }
                request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookieParts));
            }

            // Forward body for POST/PUT/PATCH
            if (ctx.Request.HasEntityBody)
            {
                var bodyStream = new MemoryStream();
                await ctx.Request.InputStream.CopyToAsync(bodyStream);
                bodyStream.Position = 0;
                request.Content = new StreamContent(bodyStream);
                if (ctx.Request.ContentType != null)
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", ctx.Request.ContentType);
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (HttpRequestException ex)
            {
                ctx.Response.StatusCode = 502;
                byte[] errBody = Encoding.UTF8.GetBytes("{\"error\":\"Cannot reach API backend: " + ex.Message.Replace("\"", "'") + "\"}");
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.ContentLength64 = errBody.Length;
                await ctx.Response.OutputStream.WriteAsync(errBody, 0, errBody.Length);
                ctx.Response.Close();
                return;
            }

            ctx.Response.StatusCode = (int)response.StatusCode;

            // Forward response headers
            foreach (var h in response.Headers)
            {
                foreach (var v in h.Value)
                {
                    string lower = h.Key.ToLowerInvariant();
                    if (lower == "transfer-encoding") continue;

                    if (lower == "set-cookie")
                    {
                        ctx.Response.AddHeader("Set-Cookie", v);
                    }
                    else
                    {
                        ctx.Response.AddHeader(h.Key, v);
                    }
                }
            }
            foreach (var h in response.Content.Headers)
            {
                foreach (var v in h.Value)
                {
                    string lower = h.Key.ToLowerInvariant();
                    if (lower == "content-length" || lower == "transfer-encoding")
                        continue;

                    if (lower == "content-type")
                    {
                        ctx.Response.ContentType = v;
                    }
                    else
                    {
                        ctx.Response.AddHeader(h.Key, v);
                    }
                }
            }

            // Check if this is an SSE stream
            string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (contentType == "text/event-stream")
            {
                // Stream SSE data through
                ctx.Response.SendChunked = true;
                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.AddHeader("Cache-Control", "no-cache");
                ctx.Response.AddHeader("Connection", "keep-alive");

                try
                {
                    using (var responseStream = await response.Content.ReadAsStreamAsync())
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                        {
                            await ctx.Response.OutputStream.WriteAsync(buffer, 0, bytesRead, ct);
                            await ctx.Response.OutputStream.FlushAsync();
                        }
                    }
                }
                catch (Exception)
                {
                    // Client disconnected or stream ended
                }
                finally
                {
                    try { ctx.Response.Close(); } catch { }
                }
            }
            else
            {
                // Regular response — read full body and forward
                byte[] responseBody = await response.Content.ReadAsByteArrayAsync();
                ctx.Response.ContentLength64 = responseBody.Length;
                if (responseBody.Length > 0)
                {
                    await ctx.Response.OutputStream.WriteAsync(responseBody, 0, responseBody.Length);
                }
                ctx.Response.Close();
            }

            response.Dispose();
        }

        // ---- Static files ----

        private async Task ServeStaticFile(HttpListenerContext ctx, string path)
        {
            if (path == "/") path = "/index.html";

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

        public void Dispose()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            if (_httpClient != null)
            {
                _httpClient.Dispose();
            }
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
            }
        }
    }
}
