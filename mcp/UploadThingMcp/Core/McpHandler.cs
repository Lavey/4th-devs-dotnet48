using System;
using FourthDevs.UploadThingMcp.Adapters;
using FourthDevs.UploadThingMcp.Config;
using FourthDevs.UploadThingMcp.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.UploadThingMcp.Core
{
    internal class McpHandler
    {
        private readonly UploadFilesTool _uploadTool;
        private readonly ListFilesTool _listTool;
        private readonly ManageFilesTool _manageTool;

        public McpHandler(EnvironmentConfig config)
        {
            var client = new UploadThingClient(config.UploadThingToken);
            _uploadTool = new UploadFilesTool(client);
            _listTool   = new ListFilesTool(client);
            _manageTool = new ManageFilesTool(client);
        }

        // Returns null for notifications (no response needed).
        public string Handle(string requestJson)
        {
            JObject request;
            try
            {
                request = JObject.Parse(requestJson);
            }
            catch (Exception ex)
            {
                return MakeError(null, -32700, "Parse error: " + ex.Message);
            }

            JToken id     = request["id"];
            string method = (string)request["method"];
            var reqParams = (request["params"] as JObject) ?? new JObject();

            bool isNotification = id == null || id.Type == JTokenType.Null;
            if (method == "notifications/initialized" || isNotification)
                return null;

            try
            {
                return HandleMethod(id, method, reqParams);
            }
            catch (Exception ex)
            {
                return MakeError(id, -32603, ex.Message);
            }
        }

        private string HandleMethod(JToken id, string method, JObject reqParams)
        {
            switch (method)
            {
                case "initialize":
                    return MakeResult(id, new JObject
                    {
                        ["protocolVersion"] = "2024-11-05",
                        ["serverInfo"] = new JObject
                        {
                            ["name"]    = "uploadthing-mcp",
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
                            _uploadTool.GetToolDefinition(),
                            _listTool.GetToolDefinition(),
                            _manageTool.GetToolDefinition()
                        }
                    });

                case "tools/call":
                {
                    string toolName = (string)reqParams["name"];
                    var toolArgs    = (reqParams["arguments"] as JObject) ?? new JObject();

                    string toolResult;
                    switch (toolName)
                    {
                        case "upload_files":  toolResult = _uploadTool.Execute(toolArgs);  break;
                        case "list_files":    toolResult = _listTool.Execute(toolArgs);    break;
                        case "manage_files":  toolResult = _manageTool.Execute(toolArgs);  break;
                        default:
                            return MakeError(id, -32603, "Unknown tool: " + toolName);
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
                    return MakeError(id, -32601, "Method not found: " + method);
            }
        }

        private static string MakeResult(JToken id, JObject result)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["result"]  = result
            }.ToString(Formatting.None);
        }

        private static string MakeError(JToken id, int code, string message)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id ?? JValue.CreateNull(),
                ["error"]   = new JObject
                {
                    ["code"]    = code,
                    ["message"] = message
                }
            }.ToString(Formatting.None);
        }
    }
}
