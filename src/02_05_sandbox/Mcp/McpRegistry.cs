using System;
using System.Collections.Generic;

namespace FourthDevs.Sandbox.Mcp
{
    /// <summary>
    /// Static registry describing available MCP servers and their tools.
    /// Tracks which tool schemas the agent has loaded during a session.
    ///
    /// Mirrors 02_05_sandbox/src/mcp-registry.ts (i-am-alice/4th-devs).
    /// </summary>
    internal static class McpRegistry
    {
        // ----------------------------------------------------------------
        // Server / tool metadata
        // ----------------------------------------------------------------

        private static readonly Dictionary<string, ServerMeta> _servers =
            new Dictionary<string, ServerMeta>(StringComparer.OrdinalIgnoreCase)
            {
                ["todo"] = new ServerMeta
                {
                    Name        = "todo",
                    Description = "Todo management server for creating, retrieving, updating, and deleting todos",
                    Tools       = new List<ToolMeta>
                    {
                        new ToolMeta { Name = "create", Description = "Create a new todo item" },
                        new ToolMeta { Name = "get",    Description = "Get a todo by ID" },
                        new ToolMeta { Name = "list",   Description = "List all todos, optionally filtered by completion status" },
                        new ToolMeta { Name = "update", Description = "Update an existing todo item" },
                        new ToolMeta { Name = "delete", Description = "Delete a todo item by ID" },
                    }
                }
            };

        // TypeScript declarations exposed to the agent when a schema is loaded
        private static readonly Dictionary<string, string> _typescript =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["todo__create"] =
                    "interface CreateInput { title: string; }\n" +
                    "interface Todo { id: string; title: string; completed: boolean; createdAt: string; updatedAt: string; }\n" +
                    "interface TodoResponse { todo: Todo; }\n" +
                    "/** Create a new todo item */\n" +
                    "function create(input: CreateInput): TodoResponse;",

                ["todo__get"] =
                    "interface GetInput { id: string; }\n" +
                    "interface Todo { id: string; title: string; completed: boolean; createdAt: string; updatedAt: string; }\n" +
                    "interface TodoResponse { todo: Todo; }\n" +
                    "/** Get a todo by ID */\n" +
                    "function get(input: GetInput): TodoResponse;",

                ["todo__list"] =
                    "interface ListInput { completed?: boolean; }\n" +
                    "interface Todo { id: string; title: string; completed: boolean; createdAt: string; updatedAt: string; }\n" +
                    "interface TodoListResponse { todos: Todo[]; }\n" +
                    "/** List all todos, optionally filtered by completion status */\n" +
                    "function list(input?: ListInput): TodoListResponse;",

                ["todo__update"] =
                    "interface UpdateInput { id: string; title?: string; completed?: boolean; }\n" +
                    "interface Todo { id: string; title: string; completed: boolean; createdAt: string; updatedAt: string; }\n" +
                    "interface TodoResponse { todo: Todo; }\n" +
                    "/** Update an existing todo item */\n" +
                    "function update(input: UpdateInput): TodoResponse;",

                ["todo__delete"] =
                    "interface DeleteInput { id: string; }\n" +
                    "interface DeleteResponse { success: boolean; }\n" +
                    "/** Delete a todo item by ID */\n" +
                    "function remove(input: DeleteInput): DeleteResponse;",
            };

        // ----------------------------------------------------------------
        // Session state: loaded tools
        // ----------------------------------------------------------------

        private static readonly HashSet<string> _loadedToolKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Resets the set of loaded tools for a new session.
        /// </summary>
        public static void ResetSession()
        {
            _loadedToolKeys.Clear();
        }

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        /// <summary>Returns a summary of all available servers.</summary>
        public static IEnumerable<object> ListServers()
        {
            foreach (var s in _servers.Values)
                yield return new { name = s.Name, description = s.Description };
        }

        /// <summary>Returns tool metadata for a server, or null if not found.</summary>
        public static IList<ToolMeta> ListTools(string serverName)
        {
            return _servers.TryGetValue(serverName, out ServerMeta s) ? s.Tools : null;
        }

        /// <summary>
        /// Returns the TypeScript declaration for a tool and marks it as loaded.
        /// Returns null if the server or tool is not found.
        /// </summary>
        public static string GetToolSchema(string serverName, string toolName)
        {
            string key = $"{serverName}__{toolName}";
            if (!_typescript.TryGetValue(key, out string ts))
                return null;

            _loadedToolKeys.Add(key);
            return ts;
        }

        /// <summary>
        /// Returns the set of (serverName, toolName) pairs the agent has loaded.
        /// </summary>
        public static IEnumerable<Tuple<string, string>> GetLoadedTools()
        {
            foreach (string key in _loadedToolKeys)
            {
                int sep = key.IndexOf("__", StringComparison.Ordinal);
                if (sep > 0)
                    yield return Tuple.Create(key.Substring(0, sep), key.Substring(sep + 2));
            }
        }
    }

    // ----------------------------------------------------------------
    // Helper DTOs
    // ----------------------------------------------------------------

    internal sealed class ServerMeta
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<ToolMeta> Tools { get; set; } = new List<ToolMeta>();
    }

    internal sealed class ToolMeta
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
