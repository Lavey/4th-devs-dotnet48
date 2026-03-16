using System.Collections.Generic;
using FourthDevs.Common.Models;

namespace FourthDevs.Lesson06_AgenticRag.Tools
{
    /// <summary>
    /// Builds the tool schema definitions exposed to the Agentic RAG agent.
    /// Mirrors the MCP file-server tools (list, search, read) from
    /// 02_01_agentic_rag/src/mcp/client.js in the source repo.
    /// </summary>
    internal static class ToolDefinitions
    {
        internal static List<ToolDefinition> Build()
        {
            return new List<ToolDefinition>
            {
                ListFiles(),
                SearchFiles(),
                ReadFile()
            };
        }

        // ----------------------------------------------------------------
        // Individual tool definitions
        // ----------------------------------------------------------------

        private static ToolDefinition ListFiles()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "list_files",
                Description = "List files and directories inside the workspace (or a sub-path). " +
                              "Use this first to discover what documents are available.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        path = new
                        {
                            type        = "string",
                            description = "Relative path inside workspace/ (use '.' for root)"
                        }
                    },
                    required             = new[] { "path" },
                    additionalProperties = false
                },
                Strict = true
            };
        }

        private static ToolDefinition SearchFiles()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "search_files",
                Description = "Search for a keyword or phrase inside all text files in the workspace. " +
                              "Returns matching lines with file paths and line numbers. " +
                              "Use this to find relevant sections before reading whole files.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        query = new
                        {
                            type        = "string",
                            description = "Keyword or phrase to search for (case-insensitive)"
                        },
                        pattern = new
                        {
                            type        = "string",
                            description = "Optional filename glob pattern to limit the search, e.g. '*.md'"
                        }
                    },
                    required             = new[] { "query" },
                    additionalProperties = false
                },
                Strict = false
            };
        }

        private static ToolDefinition ReadFile()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "read_file",
                Description = "Read the full text content of a file inside the workspace. " +
                              "Only read a file after using search_files to confirm it is relevant.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        path = new
                        {
                            type        = "string",
                            description = "Relative file path inside workspace/"
                        }
                    },
                    required             = new[] { "path" },
                    additionalProperties = false
                },
                Strict = true
            };
        }
    }
}
