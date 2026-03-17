using System.Collections.Generic;
using Aype.AI.Common.Models;

namespace Aype.AI.Tools.Tools
{
    /// <summary>
    /// Definitions for file-system tools (list_files, read_file, write_file)
    /// operating within the agent workspace directory.
    /// </summary>
    public static class FilesTools
    {
        public static List<ToolDefinition> BuildAll()
        {
            return new List<ToolDefinition>
            {
                ListFiles(),
                ReadFile(),
                WriteFile()
            };
        }

        public static ToolDefinition ListFiles()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "list_files",
                Description = "List files and directories inside the agent workspace.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        path = new
                        {
                            type        = "string",
                            description = "Relative path inside workspace (use '.' for root)"
                        }
                    },
                    required             = new[] { "path" },
                    additionalProperties = false
                },
                Strict = true
            };
        }

        public static ToolDefinition ReadFile()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "read_file",
                Description = "Read the text content of a file inside the agent workspace.",
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

        public static ToolDefinition WriteFile()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "write_file",
                Description = "Create or overwrite a file inside the agent workspace.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        path    = new { type = "string", description = "Relative file path inside workspace/" },
                        content = new { type = "string", description = "Text content to write" }
                    },
                    required             = new[] { "path", "content" },
                    additionalProperties = false
                },
                Strict = true
            };
        }
    }
}
