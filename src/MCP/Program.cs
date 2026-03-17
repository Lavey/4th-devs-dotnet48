using System;
using System.Text;
using FourthDevs.Mcp.Files.Tools;
using FourthDevs.Mcp.Files.Utils;
using Newtonsoft.Json.Linq;
using FilesConfig = FourthDevs.Mcp.Files.Config.EnvironmentConfig;
using UploadConfig = FourthDevs.Mcp.Upload.Config.EnvironmentConfig;

namespace FourthDevs.Mcp
{
    /// <summary>
    /// Unified MCP server — runs either in "files" (stdio) or "uploadthing" (HTTP) mode.
    ///
    /// Usage:
    ///   MCP.exe files         — starts the FilesMcp stdio server
    ///   MCP.exe uploadthing   — starts the UploadThingMcp HTTP server
    ///
    /// If no argument is supplied the mode is read from the MCP_MODE environment
    /// variable or App.config key MCP_MODE (defaults to "files").
    /// </summary>
    internal static class Program
    {
        static void Main(string[] args)
        {
            string mode = ResolveMode(args);

            switch (mode)
            {
                case "uploadthing":
                    RunUploadThing();
                    break;

                default:
                    RunFiles();
                    break;
            }
        }

        // ----------------------------------------------------------------
        // Mode resolution
        // ----------------------------------------------------------------

        private static string ResolveMode(string[] args)
        {
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
                return args[0].Trim().ToLowerInvariant();

            string env = Environment.GetEnvironmentVariable("MCP_MODE");
            if (!string.IsNullOrWhiteSpace(env)) return env.Trim().ToLowerInvariant();

            try
            {
                string cfg = System.Configuration.ConfigurationManager.AppSettings["MCP_MODE"];
                if (!string.IsNullOrWhiteSpace(cfg)) return cfg.Trim().ToLowerInvariant();
            }
            catch { /* ignore */ }

            return "files";
        }

        // ----------------------------------------------------------------
        // Files mode — stdio MCP server (port of mcp/files-mcp)
        // ----------------------------------------------------------------

        private static void RunFiles()
        {
            Console.InputEncoding  = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            var config = FilesConfig.Load();
            Logger.Initialize(config.LogLevel);

            Logger.Info("MCP server starting in [files] mode");
            foreach (var desc in config.MountPoints)
                Logger.Info($"  Mount: {desc.Alias} → {desc.AbsolutePath}");

            var fsRead   = new FsReadTool(config);
            var fsSearch = new FsSearchTool(config);
            var fsWrite  = new FsWriteTool(config);
            var fsManage = new FsManageTool(config);

            using (var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8)
            {
                AutoFlush = true
            })
            {

            string line;
            while ((line = Console.In.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                JObject request;
                try { request = JObject.Parse(line); }
                catch (Exception ex)
                {
                    Logger.Error("Failed to parse JSON request: " + ex.Message);
                    continue;
                }

                JToken id         = request["id"];
                string method     = (string)request["method"];
                JObject reqParams = (request["params"] as JObject) ?? new JObject();

                Logger.Debug($"→ method={method} id={id}");

                bool isNotification = id == null || id.Type == JTokenType.Null;
                if (method == "notifications/initialized") { Logger.Info("Client initialized."); continue; }
                if (isNotification) { Logger.Debug($"  Notification '{method}' — no response."); continue; }

                string response;
                try
                {
                    response = HandleFilesRequest(id, method, reqParams, fsRead, fsSearch, fsWrite, fsManage);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Unhandled error for method '{method}': {ex}");
                    response = MakeError(id, -32603, ex.Message);
                }

                Logger.Debug($"← {response?.Length ?? 0} chars");
                stdout.WriteLine(response);
            }

            Logger.Info("MCP [files] server shutting down (stdin closed).");
            } // end using stdout
        }

        private static string HandleFilesRequest(JToken id, string method, JObject reqParams,
            FsReadTool fsRead, FsSearchTool fsSearch, FsWriteTool fsWrite, FsManageTool fsManage)
        {
            switch (method)
            {
                case "initialize":
                    return MakeResult(id, new JObject
                    {
                        ["protocolVersion"] = "2024-11-05",
                        ["serverInfo"] = new JObject { ["name"] = "files-mcp", ["version"] = "1.0.0" },
                        ["capabilities"] = new JObject { ["tools"] = new JObject() }
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
                        default: return MakeError(id, -32603, $"Unknown tool: {toolName}");
                    }
                    return MakeResult(id, new JObject
                    {
                        ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = toolResult } }
                    });
                }

                default:
                    return MakeError(id, -32601, $"Method not found: {method}");
            }
        }

        // ----------------------------------------------------------------
        // UploadThing mode — HTTP MCP server (port of mcp/uploadthing-mcp)
        // ----------------------------------------------------------------

        private static void RunUploadThing()
        {
            var config = UploadConfig.Load();
            Console.WriteLine("MCP server starting in [uploadthing] mode");
            Console.WriteLine("Listening on http://{0}:{1}/", config.Host, config.Port);
            Console.WriteLine("Press Ctrl+C to stop.");

            using (var server = new Upload.Http.HttpServerHost(config))
            {
                server.Start();
                Console.CancelKeyPress += (s, e) => { e.Cancel = true; server.Stop(); };
                server.WaitForStop();
            }
        }

        // ----------------------------------------------------------------
        // JSON-RPC helpers
        // ----------------------------------------------------------------

        private static string MakeResult(JToken id, JObject result)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["result"]  = result
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string MakeError(JToken id, int code, string message)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["error"]   = new JObject { ["code"] = code, ["message"] = message }
            }.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
