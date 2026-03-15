using System.Collections.Generic;
using FourthDevs.Common.Models;

namespace FourthDevs.Lesson05_Agent.Tools
{
    /// <summary>
    /// Builds the tool schema definitions exposed to the agent.
    /// Mirrors 01_05_agent/src/tools/definitions/ in the source repo.
    /// </summary>
    internal static class AgentToolDefinitions
    {
        internal static List<ToolDefinition> Build()
        {
            return new List<ToolDefinition>
            {
                Calculator(),
                ListFiles(),
                ReadFile(),
                WriteFile(),
                AskUser(),
                Delegate(),
                SendMessage()
            };
        }

        // ----------------------------------------------------------------
        // Individual tool definitions (mirrors definitions/ sub-folder)
        // ----------------------------------------------------------------

        private static ToolDefinition Calculator()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "calculator",
                Description = "Evaluate a mathematical expression and return the numeric result.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        expression = new { type = "string", description = "Math expression, e.g. '42 * 17' or 'sqrt(144)'" }
                    },
                    required             = new[] { "expression" },
                    additionalProperties = false
                },
                Strict = true
            };
        }

        private static ToolDefinition ListFiles()
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
                Description = "Read the text content of a file inside the agent workspace.",
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

        private static ToolDefinition AskUser()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "ask_user",
                Description = "Ask the user a question and wait for their response. " +
                              "Use this when you need clarification, confirmation, or additional " +
                              "information that only the user can provide. The agent will pause " +
                              "until the user responds.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        question = new { type = "string", description = "The question to ask the user" }
                    },
                    required             = new[] { "question" },
                    additionalProperties = false
                },
                Strict = true
            };
        }

        private static ToolDefinition Delegate()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "delegate",
                Description = "Delegate a task to another agent and wait for the result. " +
                              "Use this when a specialised agent can handle part of the work " +
                              "(e.g. web research, file operations).",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        agent = new { type = "string", description = "Name of the agent template to run (e.g. \"bob\")" },
                        task  = new { type = "string", description = "A clear description of what the child agent should accomplish" }
                    },
                    required             = new[] { "agent", "task" },
                    additionalProperties = false
                },
                Strict = true
            };
        }

        private static ToolDefinition SendMessage()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "send_message",
                Description = "Send a non-blocking message to another running agent. " +
                              "The message appears in the target agent's context on their next turn. " +
                              "Use this to share information without waiting for a response.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        to      = new { type = "string", description = "The agent ID to send the message to" },
                        message = new { type = "string", description = "The message content to deliver" }
                    },
                    required             = new[] { "to", "message" },
                    additionalProperties = false
                },
                Strict = true
            };
        }
    }
}
