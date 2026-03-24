using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Events.Mcp
{
    /// <summary>
    /// MCP configuration types.
    /// </summary>
    internal class McpConfig
    {
        public Dictionary<string, McpServerConfig> McpServers { get; set; }
            = new Dictionary<string, McpServerConfig>();
    }

    internal class McpServerConfig
    {
        public string Command { get; set; }
        public List<string> Args { get; set; } = new List<string>();
        public Dictionary<string, string> Env { get; set; } = new Dictionary<string, string>();
        public string Cwd { get; set; }
    }

    internal class McpToolInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject InputSchema { get; set; }
        public string ServerName { get; set; }
    }
}
