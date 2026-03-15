using System.Collections.Generic;
using FourthDevs.Common.Models;

namespace FourthDevs.Lesson05_Confirmation.Tools
{
    /// <summary>
    /// Builds the tool schema definitions exposed to the confirmation agent.
    /// Mirrors 01_05_confirmation/src/native/tools.js (nativeTools array)
    /// in the source repo.
    /// </summary>
    internal static class ToolDefinitions
    {
        internal static List<ToolDefinition> Build()
        {
            return new List<ToolDefinition>
            {
                ListFiles(),
                ReadFile(),
                WriteFile(),
                SearchFiles(),
                SendEmail()
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
                Description = "List files and directories inside workspace/ (or a sub-path)",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Relative path inside workspace (use '.' for root)" }
                    },
                    required             = new[] { "path" },
                    additionalProperties = false
                },
                Strict = true
            };
        }

        private static ToolDefinition ReadFile()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "read_file",
                Description = "Read the text content of a file inside workspace/",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Relative file path inside workspace/" }
                    },
                    required             = new[] { "path" },
                    additionalProperties = false
                },
                Strict = true
            };
        }

        private static ToolDefinition WriteFile()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "write_file",
                Description = "Create or overwrite a file inside workspace/ with given text content",
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

        private static ToolDefinition SearchFiles()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "search_files",
                Description = "Search for files in workspace/ whose names match a pattern (supports * wildcard)",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        pattern = new { type = "string", description = "Filename pattern, e.g. '*.md' or 'report*'" }
                    },
                    required             = new[] { "pattern" },
                    additionalProperties = false
                },
                Strict = true
            };
        }

        private static ToolDefinition SendEmail()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "send_email",
                Description = "Send an email to one or more recipients. Recipients must be in workspace/whitelist.json.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        to       = new { type = "array", items = new { type = "string" }, description = "Recipient email address(es). Must be in the whitelist." },
                        subject  = new { type = "string", description = "Email subject line." },
                        body     = new { type = "string", description = "Email content. Plain text or HTML." },
                        format   = new { type = "string", @enum = new[] { "text", "html" }, description = "Content format: 'text' or 'html'. Default: text" },
                        reply_to = new { type = "string", description = "Optional reply-to email address." }
                    },
                    required             = new[] { "to", "subject", "body" },
                    additionalProperties = false
                },
                Strict = false
            };
        }
    }
}
