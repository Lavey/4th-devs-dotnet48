using System.Collections.Generic;
using System.Linq;
using FourthDevs.Events.Mcp;
using FourthDevs.Events.Models;

namespace FourthDevs.Events.Tools
{
    internal static class ToolRegistry
    {
        private static readonly List<Tool> AllTools = new List<Tool>
        {
            WebSearchTool.Create(),
            HumanTool.Create(),
            RenderHtmlTool.Create()
        };

        public static IReadOnlyList<Tool> GetAllTools()
        {
            return AllTools;
        }

        public static Tool FindTool(string name)
        {
            return AllTools.FirstOrDefault(t => t.Definition.Name == name);
        }

        /// <summary>
        /// Resolve tools by name list: returns local tools that match, plus MCP stubs.
        /// </summary>
        public static List<Tool> ResolveTools(List<string> toolNames, McpManager mcp)
        {
            var result = new List<Tool>();
            if (toolNames == null) return result;

            foreach (string name in toolNames)
            {
                // Built-in tools
                var local = FindTool(name);
                if (local != null)
                {
                    result.Add(local);
                    continue;
                }

                // MCP tools (prefixed names with __)
                if (name.Contains("__") && mcp != null)
                {
                    var mcpDef = mcp.GetToolDefinition(name);
                    if (mcpDef != null)
                    {
                        result.Add(mcpDef);
                    }
                }
            }

            return result;
        }
    }
}
