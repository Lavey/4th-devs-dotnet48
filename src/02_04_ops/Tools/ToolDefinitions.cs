using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Ops.Tools
{
    /// <summary>
    /// Builds tool definition arrays for the Responses API.
    /// Each agent template lists the tool names it may use;
    /// this class maps names to their JSON schemas.
    /// </summary>
    internal static class ToolDefinitions
    {
        // Registry: name → JSON schema
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

            r["get_mail"] = Tool(
                "get_mail",
                "Read all emails from the mail inbox. Returns JSON array of emails.",
                new JObject { ["type"] = "object", ["properties"] = new JObject() });

            r["get_calendar"] = Tool(
                "get_calendar",
                "Read all calendar events. Returns JSON array of events.",
                new JObject { ["type"] = "object", ["properties"] = new JObject() });

            r["get_tasks"] = Tool(
                "get_tasks",
                "Read all tasks. Returns JSON array of tasks.",
                new JObject { ["type"] = "object", ["properties"] = new JObject() });

            r["get_notes"] = Tool(
                "get_notes",
                "Read all notes. Returns JSON array of notes.",
                new JObject { ["type"] = "object", ["properties"] = new JObject() });

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
                            ["description"] = "File path relative to workspace"
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
                            ["description"] = "File path relative to workspace"
                        },
                        ["content"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Content to write"
                        }
                    },
                    ["required"] = new JArray { "path", "content" }
                });

            r["delegate"] = Tool(
                "delegate",
                "Delegate a task to another specialist agent. Available agents: mail, calendar, tasks, notes.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["agent"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Name of the agent to delegate to (mail | calendar | tasks | notes)"
                        },
                        ["task"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Task description to delegate"
                        }
                    },
                    ["required"] = new JArray { "agent", "task" }
                });

            return r;
        }

        private static JObject Tool(string name, string description, JObject parameters)
            => new JObject
            {
                ["type"]     = "function",
                ["name"]     = name,
                ["description"] = description,
                ["parameters"]  = parameters
            };
    }
}
