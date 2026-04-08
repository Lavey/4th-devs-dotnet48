using System;

namespace FourthDevs.CodingAgent.Config
{
    /// <summary>
    /// Constants and prompts for the coding agent.
    /// Mirrors config.ts from the TypeScript original.
    /// </summary>
    internal static class AgentConfig
    {
        public const string Model = "gpt-5.4";
        public const string MemoryModel = "gpt-4.1-mini";
        public const int MaxTurns = 30;
        public const string ReasoningEffort = "medium";

        public const int KeepRecentMessages = 10;
        public const int CompactAfterMessages = 18;
        public const int CompactAfterChars = 18000;

        public const string SystemPrompt =
@"You are a careful full-stack coding agent working inside a local workspace.

Use the available filesystem tools to inspect, create, and edit files.

Rules:
- Keep changes minimal but complete.
- Read before editing when modifying existing files.
- Put new projects in their own subdirectory inside workspace/.
- Do not create project files directly in the workspace root.
- When you finish, reply with a short summary of what you changed.";

        public const string MemoryPrompt =
@"You summarize conversation state for a coding agent.

Keep only durable, useful context:
- user goals and constraints
- decisions that were made
- files created or changed
- important tool results, errors, and blockers
- unfinished work

Write a short bullet list in plain text.
Do not mention memory mechanics.";

        public const string DemoTask =
@"Build a complete Snake game web application inside a ""snake"" directory.

Requirements:
- Use a dedicated snake/ folder
- Create complete, working files
- Prefer a simple stack (plain HTML/CSS/JS)
- Include both the game UI and score persistence
- Keep the implementation polished and playable";

        /// <summary>
        /// Returns the resolved workspace directory path (project-relative).
        /// </summary>
        public static string GetWorkspacePath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");
        }

        public static string GetMemoryDir()
        {
            return System.IO.Path.Combine(GetWorkspacePath(), "memory");
        }

        public static string GetLogDir()
        {
            return System.IO.Path.Combine(GetWorkspacePath(), "logs");
        }
    }
}
