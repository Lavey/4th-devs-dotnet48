using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson05_Agent.Mcp
{
    // ─────────────────────────────────────────────────────────────────────────
    // Interface
    // ─────────────────────────────────────────────────────────────────────────

    internal interface IMcpClient : IDisposable
    {
        string ServerName { get; }
        Task InitializeAsync();
        Task<List<McpToolInfo>> ListToolsAsync();
        Task<string> CallToolAsync(string toolName, JObject args);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stdio transport
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MCP client that communicates with a locally spawned stdio server process.
    /// Mirrors the StdioClientTransport from @modelcontextprotocol/sdk.
    /// </summary>
    internal class StdioMcpClient : IMcpClient
    {
        private readonly McpServerConfig _cfg;
        private Process _proc;
        private StreamWriter _stdin;
        private int _nextId = 1;

        // Pending responses keyed by request id
        private readonly Dictionary<string, TaskCompletionSource<JObject>> _pending
            = new Dictionary<string, TaskCompletionSource<JObject>>();
        private readonly object _pendingLock = new object();

        public string ServerName { get; }

        public StdioMcpClient(string serverName, McpServerConfig cfg)
        {
            ServerName = serverName;
            _cfg       = cfg;
        }

        public Task InitializeAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName               = _cfg.Command,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = false,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            if (_cfg.Args != null)
            {
                var argParts = new List<string>();
                foreach (var a in _cfg.Args) argParts.Add(EscapeArg(a));
                psi.Arguments = string.Join(" ", argParts);
            }

            if (!string.IsNullOrWhiteSpace(_cfg.Cwd))
                psi.WorkingDirectory = _cfg.Cwd;

            if (_cfg.Env != null)
                foreach (var kv in _cfg.Env) psi.EnvironmentVariables[kv.Key] = kv.Value;

            _proc  = Process.Start(psi);
            _stdin = _proc.StandardInput;

            // Background reader thread
            var reader = _proc.StandardOutput;
            Task.Run(async () =>
            {
                try
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                        try
                        {
                            var obj = JObject.Parse(line);
                            var idToken = obj["id"];
                            if (idToken != null && idToken.Type != JTokenType.Null)
                            {
                                string key = idToken.ToString();
                                TaskCompletionSource<JObject> tcs;
                                lock (_pendingLock)
                                    _pending.TryGetValue(key, out tcs);
                                tcs?.TrySetResult(obj);
                            }
                        }
                        catch { /* skip malformed lines */ }
                    }
                }
                catch { /* process exited */ }
            });

            // Send initialize
            return SendRequestAsync("initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"]    = new JObject(),
                ["clientInfo"]      = new JObject { ["name"] = "lesson05-agent", ["version"] = "1.0.0" }
            });
        }

        public async Task<List<McpToolInfo>> ListToolsAsync()
        {
            var response = await SendRequestAsync("tools/list", new JObject());
            var tools = new List<McpToolInfo>();
            var toolsArr = response["result"]?["tools"] as JArray;
            if (toolsArr == null) return tools;

            foreach (var t in toolsArr)
            {
                string name = t["name"]?.ToString();
                if (string.IsNullOrEmpty(name)) continue;
                tools.Add(new McpToolInfo
                {
                    ServerName   = ServerName,
                    OriginalName = name,
                    PrefixedName = ServerName + "__" + name,
                    Description  = t["description"]?.ToString(),
                    InputSchema  = (t["inputSchema"] as JObject) ?? new JObject()
                });
            }
            return tools;
        }

        public async Task<string> CallToolAsync(string toolName, JObject args)
        {
            var response = await SendRequestAsync("tools/call", new JObject
            {
                ["name"]      = toolName,
                ["arguments"] = args
            });

            var content = response["result"]?["content"] as JArray;
            if (content == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var c in content)
            {
                if ((string)c["type"] == "text")
                    sb.AppendLine(c["text"]?.ToString());
            }
            return sb.ToString().Trim();
        }

        private async Task<JObject> SendRequestAsync(string method, JObject reqParams)
        {
            string id = (_nextId++).ToString();
            var req = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["method"]  = method,
                ["params"]  = reqParams
            };

            var tcs = new TaskCompletionSource<JObject>();
            lock (_pendingLock) _pending[id] = tcs;

            await _stdin.WriteLineAsync(req.ToString(Formatting.None));

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                cts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
                var result = await tcs.Task;
                lock (_pendingLock) _pending.Remove(id);
                return result;
            }
        }

        private static string EscapeArg(string arg)
        {
            if (arg.IndexOf(' ') < 0 && arg.IndexOf('"') < 0) return arg;
            return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        public void Dispose()
        {
            try { _proc?.Kill(); } catch { }
            try { _proc?.Dispose(); } catch { }
            _proc = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HTTP transport
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MCP client that communicates with an HTTP MCP server via JSON-RPC POST.
    /// Mirrors the StreamableHTTPClientTransport from @modelcontextprotocol/sdk.
    /// </summary>
    internal class HttpMcpClient : IMcpClient
    {
        private readonly McpServerConfig _cfg;
        private readonly HttpClient _http = new HttpClient();
        private int _nextId = 1;

        public string ServerName { get; }

        public HttpMcpClient(string serverName, McpServerConfig cfg)
        {
            ServerName = serverName;
            _cfg       = cfg;

            if (cfg.Headers != null)
                foreach (var kv in cfg.Headers)
                    _http.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
        }

        public async Task InitializeAsync()
        {
            await SendRequestAsync("initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"]    = new JObject(),
                ["clientInfo"]      = new JObject { ["name"] = "lesson05-agent", ["version"] = "1.0.0" }
            });
        }

        public async Task<List<McpToolInfo>> ListToolsAsync()
        {
            var response = await SendRequestAsync("tools/list", new JObject());
            var tools = new List<McpToolInfo>();
            var toolsArr = response["result"]?["tools"] as JArray;
            if (toolsArr == null) return tools;

            foreach (var t in toolsArr)
            {
                string name = t["name"]?.ToString();
                if (string.IsNullOrEmpty(name)) continue;
                tools.Add(new McpToolInfo
                {
                    ServerName   = ServerName,
                    OriginalName = name,
                    PrefixedName = ServerName + "__" + name,
                    Description  = t["description"]?.ToString(),
                    InputSchema  = (t["inputSchema"] as JObject) ?? new JObject()
                });
            }
            return tools;
        }

        public async Task<string> CallToolAsync(string toolName, JObject args)
        {
            var response = await SendRequestAsync("tools/call", new JObject
            {
                ["name"]      = toolName,
                ["arguments"] = args
            });

            var content = response["result"]?["content"] as JArray;
            if (content == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var c in content)
            {
                if ((string)c["type"] == "text")
                    sb.AppendLine(c["text"]?.ToString());
            }
            return sb.ToString().Trim();
        }

        private async Task<JObject> SendRequestAsync(string method, JObject reqParams)
        {
            string id = (_nextId++).ToString();
            var req = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["method"]  = method,
                ["params"]  = reqParams
            };

            string url = _cfg.Url?.TrimEnd('/');
            if (!url.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
                url += "/mcp";

            using (var content = new StringContent(
                req.ToString(Formatting.None), Encoding.UTF8, "application/json"))
            using (var resp = await _http.PostAsync(url, content))
            {
                string body = await resp.Content.ReadAsStringAsync();
                return JObject.Parse(body);
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Manager
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Manages all MCP server connections for the agent.
    /// Mirrors 01_05_agent/src/mcp/client.ts (createMcpManager).
    /// </summary>
    internal class McpClientManager : IDisposable
    {
        private readonly List<IMcpClient> _clients = new List<IMcpClient>();
        private List<McpToolInfo> _cachedTools;

        public bool HasServers => _clients.Count > 0;

        /// <summary>
        /// Load .mcp.json from <paramref name="mcpJsonPath"/>, connect to all configured
        /// servers, and cache the tool list.
        /// </summary>
        public async Task InitializeAsync(string mcpJsonPath)
        {
            var config = McpConfig.Load(mcpJsonPath);

            foreach (var kv in config.McpServers)
            {
                string name = kv.Key;
                var   cfg   = kv.Value;

                IMcpClient client = cfg.Transport == "http"
                    ? (IMcpClient)new HttpMcpClient(name, cfg)
                    : new StdioMcpClient(name, cfg);

                try
                {
                    await client.InitializeAsync();
                    _clients.Add(client);
                    Console.Error.WriteLine($"[mcp] Connected to server: {name}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[mcp] Failed to connect to server '{name}': {ex.Message}");
                    client.Dispose();
                }
            }

            _cachedTools = await RefreshToolsAsync();
        }

        private async Task<List<McpToolInfo>> RefreshToolsAsync()
        {
            var all = new List<McpToolInfo>();
            foreach (var client in _clients)
            {
                try
                {
                    var tools = await client.ListToolsAsync();
                    all.AddRange(tools);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[mcp] Failed to list tools for '{client.ServerName}': {ex.Message}");
                }
            }
            return all;
        }

        /// <summary>Returns all available MCP tools (cached after initialization).</summary>
        public List<McpToolInfo> GetTools() => _cachedTools ?? new List<McpToolInfo>();

        /// <summary>Returns the connected server names.</summary>
        public IEnumerable<string> GetServerNames()
        {
            foreach (var c in _clients) yield return c.ServerName;
        }

        /// <summary>
        /// Execute a tool call.  The <paramref name="prefixedName"/> must be in the
        /// form <c>serverName__toolName</c>.
        /// </summary>
        public async Task<string> CallToolAsync(string prefixedName, JObject args)
        {
            const string sep = "__";
            int idx = prefixedName.IndexOf(sep, StringComparison.Ordinal);
            if (idx < 0)
                throw new InvalidOperationException($"Invalid MCP tool name (missing __): {prefixedName}");

            string serverName = prefixedName.Substring(0, idx);
            string toolName   = prefixedName.Substring(idx + sep.Length);

            IMcpClient target = null;
            foreach (var c in _clients)
            {
                if (c.ServerName == serverName) { target = c; break; }
            }

            if (target == null)
                throw new InvalidOperationException($"MCP server not connected: {serverName}");

            return await target.CallToolAsync(toolName, args);
        }

        public void Dispose()
        {
            foreach (var c in _clients) { try { c.Dispose(); } catch { } }
            _clients.Clear();
        }
    }
}
