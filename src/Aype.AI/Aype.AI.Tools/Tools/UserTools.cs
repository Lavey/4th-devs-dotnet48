using System.Collections.Generic;
using Aype.AI.Common.Models;

namespace Aype.AI.Tools.Tools
{
    /// <summary>
    /// Definitions for user-interaction tools (ask_user, delegate, send_message).
    /// </summary>
    public static class UserTools
    {
        public static List<ToolDefinition> BuildAll()
        {
            return new List<ToolDefinition>
            {
                AskUser(),
                Delegate(),
                SendMessage()
            };
        }

        public static ToolDefinition AskUser()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "ask_user",
                Description = "Ask the user a clarifying question and wait for their response.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        question = new
                        {
                            type        = "string",
                            description = "The question to ask the user"
                        }
                    },
                    required             = new[] { "question" },
                    additionalProperties = false
                },
                Strict = true
            };
        }

        public static ToolDefinition Delegate()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "delegate",
                Description = "Delegate a subtask to another agent or process.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        agent = new
                        {
                            type        = "string",
                            description = "Name of the agent to delegate to"
                        },
                        task = new
                        {
                            type        = "string",
                            description = "Description of the task to delegate"
                        }
                    },
                    required             = new[] { "agent", "task" },
                    additionalProperties = false
                },
                Strict = true
            };
        }

        public static ToolDefinition SendMessage()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "send_message",
                Description = "Send a message to a user or system.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        recipient = new
                        {
                            type        = "string",
                            description = "The recipient of the message"
                        },
                        message = new
                        {
                            type        = "string",
                            description = "The message content"
                        }
                    },
                    required             = new[] { "recipient", "message" },
                    additionalProperties = false
                },
                Strict = true
            };
        }
    }
}
