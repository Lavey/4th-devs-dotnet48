using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Sandbox.Tools
{
    /// <summary>
    /// Builds tool definition arrays for the Responses API.
    ///
    /// Mirrors 02_05_sandbox/src/tools.ts tool definitions (i-am-alice/4th-devs).
    /// </summary>
    internal static class ToolDefinitions
    {
        private static readonly Dictionary<string, JObject> Registry = BuildRegistry();

        /// <summary>
        /// Returns a JArray of tool definition objects for the given tool names.
        /// Unknown names are silently skipped.
        /// </summary>
        public static JArray BuildFor(IEnumerable<string> names)
        {
            var arr = new JArray();
            foreach (string name in names)
            {
                if (Registry.TryGetValue(name, out JObject def))
                    arr.Add(def);
            }
            return arr;
        }

        // ----------------------------------------------------------------
        // Registry
        // ----------------------------------------------------------------

        private static Dictionary<string, JObject> BuildRegistry()
        {
            var r = new Dictionary<string, JObject>();

            r["list_servers"] = Tool(
                "list_servers",
                "List all available MCP servers",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject(),
                    ["additionalProperties"] = false
                });

            r["list_tools"] = Tool(
                "list_tools",
                "List all tools available from a specific MCP server",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["server"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "The name of the MCP server"
                        }
                    },
                    ["required"] = new JArray { "server" },
                    ["additionalProperties"] = false
                });

            r["get_tool_schema"] = Tool(
                "get_tool_schema",
                "Get the TypeScript schema for a specific tool from an MCP server",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["server"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "The name of the MCP server"
                        },
                        ["tool"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "The name of the tool"
                        }
                    },
                    ["required"] = new JArray { "server", "tool" },
                    ["additionalProperties"] = false
                });

            r["execute_code"] = Tool(
                "execute_code",
                "Execute JavaScript code in an isolated sandbox with access to loaded MCP tool APIs. " +
                "Tool calls are SYNCHRONOUS (no async/await needed). Write top-level statements directly. " +
                "Use console.log() to return output.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["code"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "The JavaScript code to execute"
                        }
                    },
                    ["required"] = new JArray { "code" },
                    ["additionalProperties"] = false
                });

            return r;
        }

        private static JObject Tool(string name, string description, JObject parameters)
            => new JObject
            {
                ["type"]        = "function",
                ["name"]        = name,
                ["description"] = description,
                ["parameters"]  = parameters
            };
    }
}
