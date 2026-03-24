using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Code.Core;
using FourthDevs.Code.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Code.Tools
{
    /// <summary>
    /// Creates tool definitions for the agent: MCP tools + code execution.
    ///
    /// Mirrors tools.ts from 03_02_code (i-am-alice/4th-devs).
    /// </summary>
    internal static class ToolFactory
    {
        /// <summary>
        /// Converts MCP tool definitions into <see cref="LocalToolDefinition"/> instances
        /// that route calls through the MCP client.
        /// </summary>
        public static List<LocalToolDefinition> CreateMcpTools(McpClient mcpClient, List<McpToolInfo> mcpTools)
        {
            var tools = new List<LocalToolDefinition>();

            foreach (var mcpTool in mcpTools)
            {
                // Capture the name for the closure
                string toolName = mcpTool.Name;

                tools.Add(new LocalToolDefinition
                {
                    Name = toolName,
                    Description = mcpTool.Description,
                    Parameters = mcpTool.InputSchema,
                    Handler = async (JObject args) =>
                    {
                        string result = await mcpClient.CallToolAsync(toolName, args);
                        return result;
                    }
                });
            }

            return tools;
        }

        /// <summary>
        /// Creates the <c>execute_code</c> tool that runs TypeScript code in
        /// the Deno sandbox.
        /// </summary>
        public static LocalToolDefinition CreateCodeTool(SandboxOptions options)
        {
            return new LocalToolDefinition
            {
                Name = "execute_code",
                Description = "Execute TypeScript code in the Deno sandbox. " +
                    "The code has access to a `tools` object for calling host-side MCP tools " +
                    "(e.g., tools.read_file, tools.write_file, tools.list_directory). " +
                    "Use console.log() to output results.",
                Parameters = JObject.Parse(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""code"": {
                            ""type"": ""string"",
                            ""description"": ""TypeScript code to execute in the Deno sandbox. The code has access to a global `tools` object with methods for each available MCP tool. Use npm: specifiers for npm packages and node: prefix for Node built-ins. Use console.log() to output results.""
                        }
                    },
                    ""required"": [""code""],
                    ""additionalProperties"": false
                }"),
                Handler = async (JObject args) =>
                {
                    string code = args["code"]?.ToString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(code))
                        return new { error = "No code provided" };

                    try
                    {
                        ExecutionResult result = await Sandbox.ExecuteCodeAsync(code, options);
                        return new
                        {
                            stdout = result.Stdout,
                            stderr = result.Stderr,
                            exitCode = result.ExitCode,
                            timedOut = result.TimedOut
                        };
                    }
                    catch (Exception ex)
                    {
                        return new { error = "Execution failed: " + ex.Message };
                    }
                }
            };
        }
    }
}
