using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.Code.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Code.Core
{
    /// <summary>
    /// HTTP bridge server that exposes host-side MCP tools to sandboxed
    /// Deno code at runtime.
    ///
    /// The bridge runs an HttpListener on localhost. Sandboxed code calls
    /// POST /{toolName} with JSON body, and the bridge dispatches to the
    /// registered tool handler, returning the JSON result.
    ///
    /// Mirrors the bridge section of sandbox.ts from 03_02_code (i-am-alice/4th-devs).
    /// </summary>
    internal class Bridge : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Dictionary<string, Func<JObject, Task<object>>> _handlers;
        private CancellationTokenSource _cts;
        private Task _listenTask;

        public int Port { get; }

        public Bridge(List<LocalToolDefinition> tools)
        {
            Port = FindFreePort();
            _handlers = new Dictionary<string, Func<JObject, Task<object>>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var tool in tools)
            {
                _handlers[tool.Name] = tool.Handler;
            }

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:" + Port + "/");
        }

        /// <summary>
        /// Starts the bridge HTTP server in the background.
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener.Start();
            Console.WriteLine("[bridge] Listening on http://127.0.0.1:" + Port + "/");
            _listenTask = ListenLoopAsync(_cts.Token);
        }

        /// <summary>
        /// Stops the bridge server.
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();
            try { _listener.Stop(); } catch { }
        }

        /// <summary>
        /// Generates a TypeScript prelude that creates a <c>tools</c> object
        /// with methods for each tool that call back to the bridge.
        /// </summary>
        public static string GeneratePrelude(int port, List<LocalToolDefinition> tools)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ---- Bridge prelude (auto-generated) ----");
            sb.AppendLine("const _bridgePort = " + port + ";");
            sb.AppendLine();
            sb.AppendLine("async function _callBridge(toolName: string, args: Record<string, unknown>): Promise<unknown> {");
            sb.AppendLine("  const resp = await fetch(`http://127.0.0.1:${_bridgePort}/${toolName}`, {");
            sb.AppendLine("    method: 'POST',");
            sb.AppendLine("    headers: { 'Content-Type': 'application/json' },");
            sb.AppendLine("    body: JSON.stringify(args),");
            sb.AppendLine("  });");
            sb.AppendLine("  return resp.json();");
            sb.AppendLine("}");
            sb.AppendLine();

            // Build the tools object
            sb.AppendLine("const tools = {");
            for (int i = 0; i < tools.Count; i++)
            {
                var tool = tools[i];
                string comma = i < tools.Count - 1 ? "," : "";
                sb.AppendLine("  " + SanitizeIdentifier(tool.Name)
                    + ": (args: Record<string, unknown> = {}) => _callBridge('"
                    + EscapeJs(tool.Name) + "', args)" + comma);
            }
            sb.AppendLine("};");
            sb.AppendLine();

            return sb.ToString();
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }

                // Process in background, don't block the accept loop
                var context = ctx;
#pragma warning disable CS4014 // fire-and-forget by design
                Task.Run(async () =>
                {
                    try
                    {
                        await HandleRequestAsync(context);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[bridge] Error handling request: " + ex.Message);
                        try
                        {
                            context.Response.StatusCode = 500;
                            byte[] errBytes = Encoding.UTF8.GetBytes(
                                JsonConvert.SerializeObject(new { error = ex.Message }));
                            context.Response.ContentType = "application/json";
                            context.Response.OutputStream.Write(errBytes, 0, errBytes.Length);
                            context.Response.Close();
                        }
                        catch { }
                    }
                });
#pragma warning restore CS4014
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            var request = ctx.Request;
            var response = ctx.Response;

            // Extract tool name from path: /{toolName}
            string path = request.Url.AbsolutePath.TrimStart('/');

            if (request.HttpMethod != "POST")
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            Func<JObject, Task<object>> handler;
            if (!_handlers.TryGetValue(path, out handler))
            {
                response.StatusCode = 404;
                byte[] notFound = Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new { error = "Unknown tool: " + path }));
                response.ContentType = "application/json";
                response.OutputStream.Write(notFound, 0, notFound.Length);
                response.Close();
                return;
            }

            // Read request body
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            JObject args;
            try
            {
                args = string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
            }
            catch
            {
                args = new JObject();
            }

            // Call the handler
            object result = await handler(args);

            // Serialize and send response
            string resultJson = JsonConvert.SerializeObject(result, Formatting.None);
            byte[] resultBytes = Encoding.UTF8.GetBytes(resultJson);
            response.StatusCode = 200;
            response.ContentType = "application/json";
            response.OutputStream.Write(resultBytes, 0, resultBytes.Length);
            response.Close();
        }

        private static int FindFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string SanitizeIdentifier(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }

        private static string EscapeJs(string s)
        {
            return s.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
