using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FourthDevs.Events.Core;
using FourthDevs.Events.Models;

namespace FourthDevs.Events.Mcp
{
    /// <summary>
    /// MCP client manager. Connects to MCP servers defined in .mcp.json,
    /// prefixes tools as serverName__toolName.
    /// Reuses JSON-RPC 2.0 stdio pattern from 03_02_code.
    /// </summary>
    internal class McpManager : IDisposable
    {
        private readonly Dictionary<string, McpServerInstance> _servers
            = new Dictionary<string, McpServerInstance>();

        private readonly List<McpToolInfo> _allTools = new List<McpToolInfo>();

        public List<McpToolInfo> AllTools { get { return _allTools; } }

        public static McpManager FromConfigFile(string mcpJsonPath)
        {
            if (!File.Exists(mcpJsonPath))
                return null;

            string json = File.ReadAllText(mcpJsonPath, Encoding.UTF8);
            var config = JObject.Parse(json);
            var servers = config["mcpServers"] as JObject;
            if (servers == null || !servers.HasValues)
                return null;

            var manager = new McpManager();
            foreach (var prop in servers.Properties())
            {
                var cfg = prop.Value as JObject;
                if (cfg == null) continue;

                string cmd = cfg["command"]?.ToString();
                if (string.IsNullOrEmpty(cmd)) continue;

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

                string cwd = cfg["cwd"]?.ToString();

                manager._servers[prop.Name] = new McpServerInstance
                {
                    ServerName = prop.Name,
                    Command = cmd,
                    Args = argsList,
                    Env = envDict,
                    Cwd = cwd
                };
            }

            return manager;
        }

        public async Task InitializeAllAsync()
        {
            foreach (var kv in _servers)
            {
                try
                {
                    await kv.Value.StartAsync();
                    var tools = await kv.Value.ListToolsAsync();
                    foreach (var t in tools)
                    {
                        t.ServerName = kv.Key;
                        _allTools.Add(t);
                    }
                    Logger.Info("mcp", "Connected to '" + kv.Key + "' with " + tools.Count + " tool(s)");
                }
                catch (Exception ex)
                {
                    Logger.Error("mcp", "Failed to connect to '" + kv.Key + "': " + ex.Message);
                }
            }
        }

        public async Task<string> CallToolAsync(string prefixedName, JObject args)
        {
            // Parse serverName__toolName
            int sep = prefixedName.IndexOf("__", StringComparison.Ordinal);
            if (sep < 0)
                return "Error: invalid MCP tool name format: " + prefixedName;

            string serverName = prefixedName.Substring(0, sep);
            string toolName = prefixedName.Substring(sep + 2);

            McpServerInstance server;
            if (!_servers.TryGetValue(serverName, out server))
                return "Error: MCP server '" + serverName + "' not found";

            return await server.CallToolAsync(toolName, args);
        }

        public List<ToolDefinition> GetToolDefinitions()
        {
            var defs = new List<ToolDefinition>();
            foreach (var t in _allTools)
            {
                string prefixed = t.ServerName + "__" + t.Name;
                defs.Add(new ToolDefinition
                {
                    Type = "function",
                    Name = prefixed,
                    Description = "[" + t.ServerName + "] " + t.Description,
                    Parameters = t.InputSchema
                });
            }
            return defs;
        }

        public void Dispose()
        {
            foreach (var kv in _servers)
            {
                try { kv.Value.Dispose(); } catch { }
            }
            _servers.Clear();
        }

        // ---- Inner class: single MCP server instance ----

        private class McpServerInstance : IDisposable
        {
            public string ServerName;
            public string Command;
            public List<string> Args;
            public Dictionary<string, string> Env;
            public string Cwd;

            private Process _proc;
            private StreamWriter _stdin;
            private int _nextId = 1;
            private readonly Dictionary<string, TaskCompletionSource<JObject>> _pending
                = new Dictionary<string, TaskCompletionSource<JObject>>();
            private readonly object _pendingLock = new object();

            public async Task StartAsync()
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Command,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                if (Args.Count > 0)
                    psi.Arguments = string.Join(" ", Args);

                if (!string.IsNullOrWhiteSpace(Cwd))
                    psi.WorkingDirectory = Cwd;

                foreach (var kv in Env)
                    psi.EnvironmentVariables[kv.Key] = kv.Value;

                _proc = Process.Start(psi);
                _stdin = _proc.StandardInput;

                var reader = _proc.StandardOutput;
#pragma warning disable CS4014
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
                                    if (tcs != null)
                                        tcs.TrySetResult(obj);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                });
#pragma warning restore CS4014

                await SendRequestAsync("initialize", new JObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JObject(),
                    ["clientInfo"] = new JObject
                    {
                        ["name"] = "03_02_events-agent",
                        ["version"] = "1.0.0"
                    }
                });

                var notification = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["method"] = "notifications/initialized"
                };
                await _stdin.WriteLineAsync(notification.ToString(Formatting.None));
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
                        Name = name,
                        Description = t["description"]?.ToString() ?? string.Empty,
                        InputSchema = (t["inputSchema"] as JObject) ?? new JObject()
                    });
                }
                return tools;
            }

            public async Task<string> CallToolAsync(string toolName, JObject args)
            {
                var response = await SendRequestAsync("tools/call", new JObject
                {
                    ["name"] = toolName,
                    ["arguments"] = args
                });

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

            public void Dispose()
            {
                try { _proc?.Kill(); } catch { }
                try { _proc?.Dispose(); } catch { }
                _proc = null;
            }
        }
    }
}
