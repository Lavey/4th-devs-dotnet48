using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson05_Agent.Mcp
{
    /// <summary>
    /// Configuration model for .mcp.json.
    /// Mirrors the .mcp.json schema used in the source repo (01_05_agent/.mcp.json).
    /// </summary>
    internal class McpConfig
    {
        [JsonProperty("mcpServers")]
        public Dictionary<string, McpServerConfig> McpServers { get; set; }
            = new Dictionary<string, McpServerConfig>();

        /// <summary>
        /// Load and parse a .mcp.json file.  Returns an empty config if the file
        /// does not exist or cannot be parsed.
        /// </summary>
        public static McpConfig Load(string path)
        {
            if (!System.IO.File.Exists(path))
                return new McpConfig();

            try
            {
                string json = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
                return JsonConvert.DeserializeObject<McpConfig>(json) ?? new McpConfig();
            }
            catch
            {
                return new McpConfig();
            }
        }
    }

    internal class McpServerConfig
    {
        /// <summary>"stdio" (default) or "http"</summary>
        [JsonProperty("transport")]
        public string Transport { get; set; } = "stdio";

        // ── stdio fields ──────────────────────────────────────────────────

        /// <summary>Executable to launch (stdio mode).</summary>
        [JsonProperty("command")]
        public string Command { get; set; }

        /// <summary>Arguments to pass to the executable.</summary>
        [JsonProperty("args")]
        public List<string> Args { get; set; } = new List<string>();

        /// <summary>Extra environment variables for the child process.</summary>
        [JsonProperty("env")]
        public Dictionary<string, string> Env { get; set; } = new Dictionary<string, string>();

        /// <summary>Working directory for the child process (optional).</summary>
        [JsonProperty("cwd")]
        public string Cwd { get; set; }

        // ── http fields ───────────────────────────────────────────────────

        /// <summary>Full URL of the MCP HTTP endpoint (http mode).</summary>
        [JsonProperty("url")]
        public string Url { get; set; }

        /// <summary>Additional HTTP headers to send with each request (http mode).</summary>
        [JsonProperty("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>Flat description of a tool exposed by an MCP server.</summary>
    internal class McpToolInfo
    {
        /// <summary>Server name as defined in .mcp.json.</summary>
        public string ServerName { get; set; }

        /// <summary>Tool name as reported by the server.</summary>
        public string OriginalName { get; set; }

        /// <summary>Prefixed name used as the function name in the Responses API: serverName__toolName.</summary>
        public string PrefixedName { get; set; }

        /// <summary>Human-readable description.</summary>
        public string Description { get; set; }

        /// <summary>JSON Schema for the tool's input parameters.</summary>
        public JObject InputSchema { get; set; }
    }
}
