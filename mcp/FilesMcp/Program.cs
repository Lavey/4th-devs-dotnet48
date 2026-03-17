using System;
using System.IO;
using System.Text;
using FourthDevs.FilesMcp.Config;
using FourthDevs.FilesMcp.Tools;
using FourthDevs.FilesMcp.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.FilesMcp
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            Console.InputEncoding  = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            var config = EnvironmentConfig.Load();
            Logger.Initialize(config.LogLevel);

            Logger.Info("FilesMcp server starting");
            foreach (var desc in config.MountPoints)
                Logger.Info($"  Mount: {desc.Alias} → {desc.AbsolutePath}");

            var fsRead   = new FsReadTool(config);
            var fsSearch = new FsSearchTool(config);
            var fsWrite  = new FsWriteTool(config);
            var fsManage = new FsManageTool(config);

            // Use a dedicated StreamWriter with AutoFlush so every write is immediately
            // flushed to stdout without interfering with the protocol.
            var stdout = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8)
            {
                AutoFlush = true
            };

            string line;
            while ((line = Console.In.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                JObject request;
                try
                {
                    request = JObject.Parse(line);
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to parse JSON request: " + ex.Message);
                    continue;
                }

                JToken id     = request["id"];
                string method = (string)request["method"];
                JObject reqParams = (request["params"] as JObject) ?? new JObject();

                Logger.Debug($"→ method={method} id={id}");

                // Notifications have no id and require no response
                bool isNotification = id == null || id.Type == JTokenType.Null;
                if (method == "notifications/initialized")
                {
                    Logger.Info("Client initialized.");
                    continue;
                }
                if (isNotification)
                {
                    Logger.Debug($"  Notification '{method}' — no response.");
                    continue;
                }

                string response;
                try
                {
                    response = HandleRequest(id, method, reqParams, fsRead, fsSearch, fsWrite, fsManage);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Unhandled error for method '{method}': {ex}");
                    response = MakeError(id, -32603, ex.Message);
                }

                Logger.Debug($"← {response?.Length ?? 0} chars");
                stdout.WriteLine(response);
            }

            Logger.Info("FilesMcp server shutting down (stdin closed).");
        }

        private static string HandleRequest(JToken id, string method, JObject reqParams,
            FsReadTool fsRead, FsSearchTool fsSearch, FsWriteTool fsWrite, FsManageTool fsManage)
        {
            switch (method)
            {
                case "initialize":
                    return MakeResult(id, new JObject
                    {
                        ["protocolVersion"] = "2024-11-05",
                        ["serverInfo"] = new JObject
                        {
                            ["name"]    = "files-mcp",
                            ["version"] = "1.0.0"
                        },
                        ["capabilities"] = new JObject
                        {
                            ["tools"] = new JObject()
                        }
                    });

                case "ping":
                    return MakeResult(id, new JObject());

                case "tools/list":
                    return MakeResult(id, new JObject
                    {
                        ["tools"] = new JArray
                        {
                            fsRead.GetToolDefinition(),
                            fsSearch.GetToolDefinition(),
                            fsWrite.GetToolDefinition(),
                            fsManage.GetToolDefinition()
                        }
                    });

                case "tools/call":
                {
                    string toolName = (string)reqParams["name"];
                    var toolArgs    = (reqParams["arguments"] as JObject) ?? new JObject();

                    string toolResult;
                    switch (toolName)
                    {
                        case "fs_read":   toolResult = fsRead.Execute(toolArgs);   break;
                        case "fs_search": toolResult = fsSearch.Execute(toolArgs); break;
                        case "fs_write":  toolResult = fsWrite.Execute(toolArgs);  break;
                        case "fs_manage": toolResult = fsManage.Execute(toolArgs); break;
                        default:
                            return MakeError(id, -32603, $"Unknown tool: {toolName}");
                    }

                    return MakeResult(id, new JObject
                    {
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "text",
                                ["text"] = toolResult
                            }
                        }
                    });
                }

                default:
                    return MakeError(id, -32601, $"Method not found: {method}");
            }
        }

        private static string MakeResult(JToken id, JObject result)
        {
            var response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["result"]  = result
            };
            return response.ToString(Formatting.None);
        }

        private static string MakeError(JToken id, int code, string message)
        {
            var response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["error"]   = new JObject
                {
                    ["code"]    = code,
                    ["message"] = message
                }
            };
            return response.ToString(Formatting.None);
        }
    }
}
