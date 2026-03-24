using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Code.Core
{
    /// <summary>
    /// Lightweight MCP client that communicates with a locally spawned stdio server
    /// process using JSON-RPC 2.0.
    ///
    /// Mirrors mcp.ts from 03_02_code (i-am-alice/4th-devs).
    /// </summary>
    internal class McpClient : IDisposable
    {
        private readonly string _serverName;
        private readonly string _command;
        private readonly List<string> _args;
        private readonly Dictionary<string, string> _env;
        private readonly string _cwd;

        private Process _proc;
        private StreamWriter _stdin;
        private int _nextId = 1;

        private readonly Dictionary<string, TaskCompletionSource<JObject>> _pending
            = new Dictionary<string, TaskCompletionSource<JObject>>();
        private readonly object _pendingLock = new object();

        public string ServerName => _serverName;

        public McpClient(string serverName, string command, List<string> args,
            Dictionary<string, string> env = null, string cwd = null)
        {
            _serverName = serverName;
            _command = command;
            _args = args ?? new List<string>();
            _env = env ?? new Dictionary<string, string>();
            _cwd = cwd;
        }

        /// <summary>
        /// Loads MCP client configuration from an mcp.json file and returns a
        /// client for the first (or named) server entry.
        /// </summary>
        public static McpClient FromConfig(string mcpJsonPath, string serverName = null)
        {
            if (!File.Exists(mcpJsonPath))
                throw new FileNotFoundException("mcp.json not found: " + mcpJsonPath);

            string json = File.ReadAllText(mcpJsonPath, Encoding.UTF8);
            var config = JObject.Parse(json);
            var servers = config["mcpServers"] as JObject;
            if (servers == null || !servers.HasValues)
                throw new InvalidOperationException("No mcpServers defined in mcp.json");

            JProperty entry = null;
            foreach (var prop in servers.Properties())
            {
                if (serverName == null || prop.Name == serverName)
                {
                    entry = prop;
                    break;
                }
            }

            if (entry == null)
                throw new InvalidOperationException(
                    serverName != null
                        ? "Server '" + serverName + "' not found in mcp.json"
                        : "No servers found in mcp.json");

            var cfg = entry.Value as JObject;
            string cmd = cfg["command"]?.ToString();
            var argsList = new List<string>();
            var argsArr = cfg["args"] as JArray;
            if (argsArr != null)
            {
                foreach (var a in argsArr)
                    argsList.Add(a.ToString());
            }

            var envDict = new Dictionary<string, string>();
            var envObj = cfg["env"] as JObject;
            if (envObj != null)
            {
                foreach (var kv in envObj)
                    envDict[kv.Key] = kv.Value?.ToString() ?? string.Empty;
            }

            string cwdVal = cfg["cwd"]?.ToString();

            return new McpClient(entry.Name, cmd, argsList, envDict, cwdVal);
        }

        /// <summary>
        /// Starts the server process, sends initialize + initialized, and
        /// makes the client ready for tool calls.
        /// </summary>
        public async Task InitializeAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName = _command,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (_args.Count > 0)
            {
                var escaped = new List<string>();
                foreach (var a in _args) escaped.Add(EscapeArg(a));
                psi.Arguments = string.Join(" ", escaped);
            }

            if (!string.IsNullOrWhiteSpace(_cwd))
                psi.WorkingDirectory = _cwd;

            foreach (var kv in _env)
                psi.EnvironmentVariables[kv.Key] = kv.Value;

            _proc = Process.Start(psi);
            _stdin = _proc.StandardInput;

            // Background reader thread for JSON-RPC responses
            var reader = _proc.StandardOutput;
#pragma warning disable CS4014 // fire-and-forget background reader
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
#pragma warning restore CS4014

            // Send initialize request
            await SendRequestAsync("initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject
                {
                    ["name"] = "03_02_code-agent",
                    ["version"] = "1.0.0"
                }
            });

            // Send initialized notification (no id, no response expected)
            var notification = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/initialized"
            };
            await _stdin.WriteLineAsync(notification.ToString(Formatting.None));
        }

        /// <summary>
        /// Lists available tools from the MCP server.
        /// Returns tuples of (name, description, inputSchema).
        /// </summary>
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
                    Name = name,
                    Description = t["description"]?.ToString() ?? string.Empty,
                    InputSchema = (t["inputSchema"] as JObject) ?? new JObject()
                });
            }
            return tools;
        }

        /// <summary>
        /// Calls a tool on the MCP server by name, passing the given arguments.
        /// </summary>
        public async Task<string> CallToolAsync(string toolName, JObject args)
        {
            var response = await SendRequestAsync("tools/call", new JObject
            {
                ["name"] = toolName,
                ["arguments"] = args
            });

            // Check for error
            var error = response["error"];
            if (error != null)
                return "MCP error: " + (error["message"]?.ToString() ?? error.ToString());

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
                ["id"] = id,
                ["method"] = method,
                ["params"] = reqParams
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

    /// <summary>
    /// Describes a tool exposed by an MCP server.
    /// </summary>
    internal class McpToolInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject InputSchema { get; set; }
    }
}
