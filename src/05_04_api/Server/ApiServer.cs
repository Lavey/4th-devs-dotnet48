using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.MultiAgentApi.Auth;
using FourthDevs.MultiAgentApi.Db;
using FourthDevs.MultiAgentApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.MultiAgentApi.Server
{
    /// <summary>
    /// HttpListener-based REST API server with JSON request/response envelope,
    /// CORS handling, route dispatching, SSE event streaming, and error handling.
    /// </summary>
    internal sealed class ApiServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly RouteHandler _routes;
        private readonly string[] _corsOrigins;
        private readonly JsonSerializerSettings _jsonSettings;
        private CancellationTokenSource _cts;

        internal ApiServer(string host, int port, RouteHandler routes, string corsOrigins)
        {
            _routes = routes;
            _corsOrigins = (corsOrigins ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            };

            _listener = new HttpListener();
            string prefix = string.Format("http://{0}:{1}/", host == "0.0.0.0" ? "+" : "+", port);
            _listener.Prefixes.Add(prefix);
        }

        internal void Start()
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

            // CORS
            AddCorsHeaders(ctx);

            if (method == "OPTIONS")
            {
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                return;
            }

            try
            {
                await _routes.HandleAsync(ctx, path, method, ct);
            }
            catch (ApiException apiEx)
            {
                try
                {
                    ctx.Response.StatusCode = apiEx.StatusCode;
                    await WriteJson(ctx, new JObject
                    {
                        ["error"] = new JObject
                        {
                            ["code"] = apiEx.ErrorCode,
                            ["message"] = apiEx.Message
                        }
                    });
                }
                catch { }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ApiServer] Error handling {0} {1}: {2}", method, path, ex.Message);
                try
                {
                    ctx.Response.StatusCode = 500;
                    await WriteJson(ctx, new JObject
                    {
                        ["error"] = new JObject
                        {
                            ["code"] = "internal_error",
                            ["message"] = "Internal server error"
                        }
                    });
                }
                catch { }
            }
        }

        private void AddCorsHeaders(HttpListenerContext ctx)
        {
            string origin = ctx.Request.Headers["Origin"];
            if (!string.IsNullOrWhiteSpace(origin))
            {
                bool allowed = false;
                foreach (string o in _corsOrigins)
                {
                    if (o.Trim() == "*" || o.Trim().Equals(origin, StringComparison.OrdinalIgnoreCase))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (allowed || _corsOrigins.Length == 0)
                {
                    ctx.Response.AddHeader("Access-Control-Allow-Origin", origin);
                }
            }
            else
            {
                ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            }

            ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET,POST,PUT,PATCH,DELETE,OPTIONS");
            ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type,Authorization,X-Tenant-Id,X-Account-Id,X-Idempotency-Key");
            ctx.Response.AddHeader("Access-Control-Allow-Credentials", "true");
            ctx.Response.AddHeader("Access-Control-Max-Age", "86400");
        }

        // ---- Static helpers used by RouteHandler ----

        internal static async Task WriteJson(HttpListenerContext ctx, object obj)
        {
            string json;
            if (obj is JObject jobj)
            {
                json = jobj.ToString(Formatting.None);
            }
            else
            {
                json = JsonConvert.SerializeObject(obj, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.None
                });
            }

            ctx.Response.ContentType = "application/json; charset=utf-8";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        internal static async Task<string> ReadBody(HttpListenerContext ctx)
        {
            using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
            {
                return await reader.ReadToEndAsync();
            }
        }

        internal static async Task WriteSseEvent(Stream stream, string eventType, object data)
        {
            string json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            });
            string sseMessage = string.Format("event: {0}\ndata: {1}\n\n", eventType, json);
            byte[] bytes = Encoding.UTF8.GetBytes(sseMessage);
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }

        internal static async Task WriteSseData(Stream stream, object data)
        {
            string json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            });
            string sseMessage = "data: " + json + "\n\n";
            byte[] bytes = Encoding.UTF8.GetBytes(sseMessage);
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
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

    /// <summary>
    /// Exception type for API errors with HTTP status codes.
    /// </summary>
    internal class ApiException : Exception
    {
        public int StatusCode { get; }
        public string ErrorCode { get; }

        public ApiException(int statusCode, string errorCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }
}
