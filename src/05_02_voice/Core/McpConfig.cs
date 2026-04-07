using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.VoiceAgent.Core
{
    /// <summary>
    /// Single MCP server entry from .mcp.json.
    /// </summary>
    internal sealed class McpServerConfig
    {
        public string Transport { get; set; }
        public string Command { get; set; }
        public string[] Args { get; set; }
        public Dictionary<string, string> Env { get; set; }
    }

    /// <summary>
    /// Loads MCP tool server configuration from a .mcp.json file.
    /// </summary>
    internal static class McpConfig
    {
        private const string FileName = ".mcp.json";

        /// <summary>
        /// Reads .mcp.json from <paramref name="rootDir"/> (or walks up to repo root).
        /// Returns an empty dictionary when no config file is found.
        /// </summary>
        public static Dictionary<string, McpServerConfig> Load(string rootDir)
        {
            string path = FindConfigFile(rootDir);
            if (path == null)
                return new Dictionary<string, McpServerConfig>();

            string json = File.ReadAllText(path);
            var root = JObject.Parse(json);

            var servers = root["mcpServers"] as JObject;
            if (servers == null)
                return new Dictionary<string, McpServerConfig>();

            var result = new Dictionary<string, McpServerConfig>();
            foreach (var prop in servers.Properties())
            {
                var cfg = JsonConvert.DeserializeObject<McpServerConfig>(prop.Value.ToString());
                if (cfg != null)
                    result[prop.Name] = cfg;
            }
            return result;
        }

        private static string FindConfigFile(string startDir)
        {
            string dir = startDir;
            while (dir != null)
            {
                string candidate = Path.Combine(dir, FileName);
                if (File.Exists(candidate))
                    return candidate;

                string parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }
            return null;
        }
    }
}
