using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using FourthDevs.UploadThingMcp.Config;
using FourthDevs.UploadThingMcp.Core;

namespace FourthDevs.UploadThingMcp.Http
{
    internal class HttpServerHost : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly EnvironmentConfig _config;
        private readonly McpHandler _mcpHandler;
        private readonly ManualResetEventSlim _stopEvent;
        private bool _disposed;

        public HttpServerHost(EnvironmentConfig config)
        {
            _config     = config;
            _mcpHandler = new McpHandler(config);
            _listener   = new HttpListener();
            _listener.Prefixes.Add(string.Format("http://{0}:{1}/", config.Host, config.Port));
            _stopEvent  = new ManualResetEventSlim(false);
        }

        public void Start()
        {
            _listener.Start();
            var thread = new Thread(AcceptLoop) { IsBackground = true, Name = "HttpAccept" };
            thread.Start();
        }

        public void Stop()
        {
            try { _listener.Stop(); } catch { }
            _stopEvent.Set();
        }

        public void WaitForStop()
        {
            _stopEvent.Wait();
        }

        private void AcceptLoop()
        {
            while (_listener.IsListening)
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
                catch (ObjectDisposedException)
                {
                    break;
                }

                ThreadPool.QueueUserWorkItem(_ => ProcessRequest(ctx));
            }
        }

        private void ProcessRequest(HttpListenerContext ctx)
        {
            try
            {
                AddCorsHeaders(ctx.Response);

                string path   = ctx.Request.Url.AbsolutePath.TrimEnd('/');
                string method = ctx.Request.HttpMethod;

                if (method == "OPTIONS")
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                if ((path == "" || path == "/") && method == "GET")
                {
                    ServeStatusPage(ctx);
                    return;
                }

                if (path == "/mcp" && method == "POST")
                {
                    HandleMcp(ctx);
                    return;
                }

                ctx.Response.StatusCode = 404;
                WriteText(ctx.Response, "Not Found");
            }
            catch (Exception ex)
            {
                try
                {
                    ctx.Response.StatusCode = 500;
                    WriteText(ctx.Response, "Internal Server Error: " + ex.Message);
                }
                catch { }
            }
        }

        private void ServeStatusPage(HttpListenerContext ctx)
        {
            bool tokenSet = !string.IsNullOrWhiteSpace(_config.UploadThingToken);
            string html = string.Format(
                "<!DOCTYPE html><html><body>" +
                "<h1>UploadThing MCP Server</h1>" +
                "<p>Status: Running</p>" +
                "<p>MCP endpoint: <code>POST /mcp</code></p>" +
                "<p>Host: {0}:{1}</p>" +
                "<p>Token configured: {2}</p>" +
                "</body></html>",
                _config.Host, _config.Port,
                tokenSet ? "Yes" : "No (set UPLOADTHING_TOKEN)");

            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.StatusCode  = 200;
            WriteText(ctx.Response, html);
        }

        private void HandleMcp(HttpListenerContext ctx)
        {
            string requestBody;
            using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                requestBody = reader.ReadToEnd();

            string responseBody = _mcpHandler.Handle(requestBody);

            if (responseBody == null)
            {
                // Notification: no JSON-RPC response required
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                return;
            }

            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.StatusCode  = 200;
            WriteText(ctx.Response, responseBody);
        }

        private static void AddCorsHeaders(HttpListenerResponse response)
        {
            response.Headers["Access-Control-Allow-Origin"]  = "*";
            response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
        }

        private static void WriteText(HttpListenerResponse response, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try { _listener.Stop(); } catch { }
                try { ((IDisposable)_listener).Dispose(); } catch { }
                _stopEvent.Dispose();
            }
        }
    }
}
