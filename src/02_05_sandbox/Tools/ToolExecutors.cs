using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Sandbox.Mcp;
using FourthDevs.Sandbox.Sandbox;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Sandbox.Tools
{
    /// <summary>
    /// Implements the four sandbox agent tools:
    /// <c>list_servers</c>, <c>list_tools</c>, <c>get_tool_schema</c>, <c>execute_code</c>.
    ///
    /// Mirrors 02_05_sandbox/src/tools.ts handlers (i-am-alice/4th-devs).
    /// </summary>
    internal static class ToolExecutors
    {
        public static Task<string> ExecuteAsync(string name, JObject args)
        {
            switch (name)
            {
                case "list_servers":   return Task.FromResult(ListServers());
                case "list_tools":     return Task.FromResult(ListTools(args));
                case "get_tool_schema":return Task.FromResult(GetToolSchema(args));
                case "execute_code":   return Task.FromResult(ExecuteCode(args));
                default:
                    return Task.FromResult($"Unknown tool: {name}");
            }
        }

        // ----------------------------------------------------------------
        // Tool implementations
        // ----------------------------------------------------------------

        private static string ListServers()
        {
            try
            {
                var servers = McpRegistry.ListServers().ToArray();
                return JsonConvert.SerializeObject(servers, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private static string ListTools(JObject args)
        {
            try
            {
                string server = (string)args["server"];
                if (string.IsNullOrWhiteSpace(server))
                    return "Error: server parameter must be a string";

                IList<ToolMeta> tools = McpRegistry.ListTools(server);
                if (tools == null)
                    return $"Error: Server \"{server}\" not found";

                return JsonConvert.SerializeObject(tools, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private static string GetToolSchema(JObject args)
        {
            try
            {
                string server = (string)args["server"];
                string tool   = (string)args["tool"];

                if (string.IsNullOrWhiteSpace(server))
                    return "Error: server parameter must be a string";
                if (string.IsNullOrWhiteSpace(tool))
                    return "Error: tool parameter must be a string";

                string typescript = McpRegistry.GetToolSchema(server, tool);
                if (typescript == null)
                    return $"Error: Tool \"{tool}\" not found in server \"{server}\"";

                return $"TypeScript definition loaded:\n\n{typescript}\n\nYou can now use {server}.{tool}() in execute_code.";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private static string ExecuteCode(JObject args)
        {
            try
            {
                string code = (string)args["code"];
                if (string.IsNullOrWhiteSpace(code))
                    return "Error: code parameter must be a string";

                SandboxResult sandboxResult = SandboxExecutor.Execute(code);

                if (sandboxResult.Error != null)
                {
                    Console.WriteLine($"[sandbox] Error: {sandboxResult.Error}");
                    string logsText = sandboxResult.Logs.Count > 0
                        ? string.Join("\n", sandboxResult.Logs)
                        : string.Empty;
                    return string.IsNullOrEmpty(logsText)
                        ? $"Error: {sandboxResult.Error}"
                        : $"Error: {sandboxResult.Error}\n\nLogs:\n{logsText}";
                }

                if (sandboxResult.Logs.Count > 0)
                {
                    Console.WriteLine($"[sandbox] Output ({sandboxResult.Logs.Count} lines):\n{string.Join("\n", sandboxResult.Logs)}");
                    return string.Join("\n", sandboxResult.Logs);
                }

                Console.WriteLine("[sandbox] No output captured");
                return "(executed successfully, no output)";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
