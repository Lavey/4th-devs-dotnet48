using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.CodingAgent.Tools
{
    /// <summary>
    /// Registers filesystem tools and dispatches calls.
    /// Mirrors the MCP tool listing and callTool from mcp.ts.
    /// </summary>
    internal sealed class ToolRegistry
    {
        private readonly string _workspace;
        private readonly Dictionary<string, Func<string, JObject, string>> _handlers;
        private readonly JArray _toolDefinitions;

        public ToolRegistry(string workspace)
        {
            _workspace = workspace;

            _handlers = new Dictionary<string, Func<string, JObject, string>>
            {
                { "read_file",         (ws, a) => FileSystemTools.ReadFile(ws, a) },
                { "write_file",        (ws, a) => FileSystemTools.WriteFile(ws, a) },
                { "list_directory",    (ws, a) => FileSystemTools.ListDirectory(ws, a) },
                { "create_directory",  (ws, a) => FileSystemTools.CreateDirectory(ws, a) },
                { "delete_file",       (ws, a) => FileSystemTools.DeleteFile(ws, a) },
                { "move_file",         (ws, a) => FileSystemTools.MoveFile(ws, a) },
                { "search_files",      (ws, a) => FileSystemTools.SearchFiles(ws, a) }
            };

            _toolDefinitions = BuildDefinitions();
        }

        /// <summary>
        /// Returns tool definitions in OpenAI function format for the API request.
        /// </summary>
        public JArray GetToolDefinitions()
        {
            return _toolDefinitions;
        }

        /// <summary>
        /// Executes a tool by name. Returns the tool output or an error string.
        /// </summary>
        public string Execute(string name, JObject args)
        {
            if (!_handlers.TryGetValue(name, out var handler))
                return string.Format("Error: unknown tool '{0}'", name);

            try
            {
                return handler(_workspace, args);
            }
            catch (Exception ex)
            {
                return string.Format("Error: {0}", ex.Message);
            }
        }

        private static JArray BuildDefinitions()
        {
            var tools = new JArray();

            tools.Add(Tool("read_file",
                "Read the contents of a file. Path is relative to workspace.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["path"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "File path relative to workspace"
                        }
                    },
                    ["required"] = new JArray { "path" }
                }));

            tools.Add(Tool("write_file",
                "Write content to a file. Creates parent directories if needed. Path is relative to workspace.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["path"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "File path relative to workspace"
                        },
                        ["content"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Content to write"
                        }
                    },
                    ["required"] = new JArray { "path", "content" }
                }));

            tools.Add(Tool("list_directory",
                "List files and subdirectories in a directory. Path is relative to workspace.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["path"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Directory path relative to workspace (default: root)"
                        }
                    }
                }));

            tools.Add(Tool("create_directory",
                "Create a directory (and parents). Path is relative to workspace.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["path"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Directory path relative to workspace"
                        }
                    },
                    ["required"] = new JArray { "path" }
                }));

            tools.Add(Tool("delete_file",
                "Delete a file or directory. Path is relative to workspace.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["path"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Path to delete, relative to workspace"
                        }
                    },
                    ["required"] = new JArray { "path" }
                }));

            tools.Add(Tool("move_file",
                "Move or rename a file or directory. Paths are relative to workspace.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["source"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Source path relative to workspace"
                        },
                        ["destination"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Destination path relative to workspace"
                        }
                    },
                    ["required"] = new JArray { "source", "destination" }
                }));

            tools.Add(Tool("search_files",
                "Search for files matching a glob pattern. Returns matching file paths relative to workspace.",
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["pattern"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Glob pattern to match (e.g. *.js, *.html)"
                        },
                        ["path"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Directory to search in, relative to workspace (default: root)"
                        }
                    },
                    ["required"] = new JArray { "pattern" }
                }));

            return tools;
        }

        private static JObject Tool(string name, string description, JObject parameters)
        {
            return new JObject
            {
                ["type"] = "function",
                ["name"] = name,
                ["description"] = description,
                ["parameters"] = parameters,
                ["strict"] = false
            };
        }
    }
}
