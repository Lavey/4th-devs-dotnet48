using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Garden.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Garden.Tools
{
    /// <summary>
    /// Central registry for local tool definitions and execution.
    /// Port of 04_01_garden/src/tools/index.ts.
    /// </summary>
    internal static class ToolRegistry
    {
        private static readonly Dictionary<string, LocalToolDefinition> Registry =
            new Dictionary<string, LocalToolDefinition>(StringComparer.OrdinalIgnoreCase);

        static ToolRegistry()
        {
            Register(TerminalTool.Definition);
            Register(CodeModeTool.Definition);
            Register(GitPushTool.Definition);
        }

        private static void Register(LocalToolDefinition tool)
        {
            Registry[tool.Name] = tool;
        }

        public static LocalToolDefinition FindTool(string name)
        {
            LocalToolDefinition tool;
            return Registry.TryGetValue(name, out tool) ? tool : null;
        }

        /// <summary>
        /// Builds a JArray of tool definitions for the given tool names.
        /// Includes built-in web_search tool when requested.
        /// </summary>
        public static JArray Definitions(List<string> names)
        {
            var arr = new JArray();
            if (names == null) return arr;

            foreach (string name in names)
            {
                if (name == "web_search")
                {
                    arr.Add(new JObject
                    {
                        ["type"] = "web_search",
                        ["search_context_size"] = "medium"
                    });
                    continue;
                }

                LocalToolDefinition tool;
                if (Registry.TryGetValue(name, out tool))
                {
                    arr.Add(new JObject
                    {
                        ["type"] = "function",
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = tool.Parameters
                    });
                }
            }
            return arr;
        }

        public static async Task<ToolExecutionResult> ExecuteAsync(string name, JObject args)
        {
            LocalToolDefinition tool = FindTool(name);
            if (tool == null)
                return new ToolExecutionResult(false, "Unknown tool: " + name);

            return await tool.Handler(args);
        }
    }
}
