using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentSystem.Tools
{
    /// <summary>
    /// Builds tool definition arrays for the Responses API.
    /// Each agent template lists tool names/prefixes it may use;
    /// this class maps names to their JSON schemas.
    ///
    /// Local tools (sum, send_email) are registered by exact name.
    /// File tools (read_file, write_file, list_dir, search_files) provide
    /// filesystem access scoped to the workspace directory.
    /// The delegate tool enables multi-agent delegation.
    ///
    /// Mirrors 04_04_system/src/tools/registry.js (i-am-alice/4th-devs).
    /// </summary>
    internal static class ToolDefinitions
    {
        private static readonly Dictionary<string, JObject> Registry = BuildRegistry();

        /// <summary>
        /// Returns a JArray of tool definition objects for the given tool names.
        /// Names are matched exactly or as a prefix group:
        /// "files" matches all file-related tools (read_file, write_file, list_dir, search_files).
        /// Unknown names are silently skipped.
        /// </summary>
        public static JArray BuildFor(IEnumerable<string> names, bool includeDelegate = true)
        {
            var arr = new JArray();
            var seen = new HashSet<string>();

            foreach (string name in names)
            {
                if (name == "files")
                {
                    // File tools group — matches MCP files server tools
                    AddIfNew(arr, seen, "read_file");
                    AddIfNew(arr, seen, "write_file");
                    AddIfNew(arr, seen, "list_dir");
                    AddIfNew(arr, seen, "search_files");
                }
                else
                {
                    AddIfNew(arr, seen, name);
                }
            }

            if (includeDelegate && !seen.Contains("delegate"))
                AddIfNew(arr, seen, "delegate");

            return arr;
        }

        private static void AddIfNew(JArray arr, HashSet<string> seen, string name)
        {
            if (seen.Contains(name)) return;
            if (Registry.TryGetValue(name, out JObject def))
            {
                arr.Add(def);
                seen.Add(name);
            }
        }

        // ----------------------------------------------------------------
        // Registry
        // ----------------------------------------------------------------

        private static Dictionary<string, JObject> BuildRegistry()
        {
            var r = new Dictionary<string, JObject>();

            // --- File tools (replace MCP files server) ---

            r["read_file"] = Tool(
                "read_file",
                "Read a file from the workspace directory. Path is relative to workspace root.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["path"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "File path relative to workspace root"
                        }
                    },
                    ["required"] = new JArray { "path" }
                });

            r["write_file"] = Tool(
                "write_file",
                "Write content to a file in the workspace directory. Creates parent directories if needed. Path is relative to workspace root.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["path"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "File path relative to workspace root"
                        },
                        ["content"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Content to write"
                        }
                    },
                    ["required"] = new JArray { "path", "content" }
                });

            r["list_dir"] = Tool(
                "list_dir",
                "List files and directories under a given path in the workspace. Path is relative to workspace root. Returns names with trailing '/' for directories.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["path"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Directory path relative to workspace root (e.g. 'ops/daily-news')"
                        }
                    },
                    ["required"] = new JArray { "path" }
                });

            r["search_files"] = Tool(
                "search_files",
                "Search for files in the workspace whose content contains the given query string. Returns matching file paths. Path is relative to workspace root.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Text to search for in file contents"
                        },
                        ["path"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Directory to search in (relative to workspace root). Defaults to root."
                        }
                    },
                    ["required"] = new JArray { "query" }
                });

            // --- Local tools ---

            r["sum"] = Tool(
                "sum",
                "Add two numbers together and return the result.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["a"] = new JObject
                        {
                            ["type"]        = "number",
                            ["description"] = "First number"
                        },
                        ["b"] = new JObject
                        {
                            ["type"]        = "number",
                            ["description"] = "Second number"
                        }
                    },
                    ["required"] = new JArray { "a", "b" }
                });

            r["send_email"] = Tool(
                "send_email",
                "Send an email (simulated). Writes the HTML body to the output folder instead of actually sending. Returns the path of the written file.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["to"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Recipient email address"
                        },
                        ["subject"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Email subject line"
                        },
                        ["html_body"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "HTML content of the email"
                        }
                    },
                    ["required"] = new JArray { "to", "subject", "html_body" }
                });

            // --- Delegation ---

            r["delegate"] = Tool(
                "delegate",
                "Delegate a task to another agent by name. The agent runs independently and returns its text result. Use when a task is better handled by a specialist.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["agent"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Agent name (filename without .md, e.g. 'ellie')"
                        },
                        ["task"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Clear task description for the agent"
                        }
                    },
                    ["required"] = new JArray { "agent", "task" }
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
