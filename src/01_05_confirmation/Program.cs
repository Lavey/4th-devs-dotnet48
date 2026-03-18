using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Lesson05_Confirmation.Tools;

namespace FourthDevs.Lesson05_Confirmation
{
    /// <summary>
    /// Lesson 05 – Confirmation (File &amp; Email Agent)
    ///
    /// Interactive REPL where an agent can:
    ///   1. Read, write, list and search workspace files
    ///   2. Send emails via the Resend API
    ///
    /// Before executing send_email the agent pauses and asks the user
    /// for confirmation (Y/N) or to trust the tool for the session (T).
    /// Recipients are validated against workspace/whitelist.json.
    ///
    /// REPL commands: 'exit' | 'clear' | 'untrust'
    ///
    /// Source: 01_05_confirmation/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        // ----------------------------------------------------------------
        // Resend config (read directly from App.config)
        // ----------------------------------------------------------------

        private static string ResendApiKey =>
            ConfigurationManager.AppSettings["RESEND_API_KEY"]?.Trim() ?? string.Empty;

        private static string ResendFrom =>
            ConfigurationManager.AppSettings["RESEND_FROM"]?.Trim() ?? string.Empty;

        // ----------------------------------------------------------------
        // Entry point
        // ----------------------------------------------------------------

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            string workspaceRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");
            string whitelistPath = Path.Combine(workspaceRoot, "whitelist.json");

            // Inject workspace config into extracted classes
            ToolExecutors.WorkspaceRoot = workspaceRoot;
            ToolExecutors.WhitelistPath = whitelistPath;
            ToolExecutors.ResendApiKey  = ResendApiKey;
            ToolExecutors.ResendFrom    = ResendFrom;

            EnsureWorkspace(workspaceRoot);

            Console.WriteLine("=== File & Email Agent ===");
            Console.WriteLine("Type your query. Special commands: 'exit' | 'clear' | 'untrust'");
            Console.WriteLine();

            PrintExamples();

            var conversation = new List<object>();

            while (true)
            {
                Console.Write("You: ");
                string input = Console.ReadLine();

                if (input == null) break; // EOF / Ctrl-Z

                string trimmed = input.Trim();

                if (string.IsNullOrEmpty(trimmed)) continue;

                string lower = trimmed.ToLowerInvariant();

                if (lower == "exit" || lower == "quit")
                    break;

                if (lower == "clear")
                {
                    conversation.Clear();
                    ColorLine("  [Conversation cleared]", ConsoleColor.DarkGray);
                    Console.WriteLine();
                    continue;
                }

                if (lower == "untrust")
                {
                    ConfirmationUi.ClearTrustedTools();
                    ColorLine("  [Trusted tools cleared]", ConsoleColor.DarkGray);
                    Console.WriteLine();
                    continue;
                }

                conversation.Add(new { type = "message", role = "user", content = trimmed });

                try
                {
                    string response = await AgentRunner.RunAgentLoop(
                        conversation, ConfirmationUi.ShouldRunTool);

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Agent: ");
                    Console.ResetColor();
                    Console.WriteLine(response);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    ColorLine("  [Error] " + ex.Message, ConsoleColor.Red);
                    Console.WriteLine();
                }
            }
        }

        // ----------------------------------------------------------------
        // Workspace setup
        // ----------------------------------------------------------------

        static void EnsureWorkspace(string workspaceRoot)
        {
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, "docs"));
            Directory.CreateDirectory(Path.Combine(workspaceRoot, "output"));
        }

        // ----------------------------------------------------------------
        // Console helpers
        // ----------------------------------------------------------------

        static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        static void PrintExamples()
        {
            ColorLine("Example queries:", ConsoleColor.DarkGray);
            string[] examples =
            {
                "List all files in the workspace",
                "Read workspace/docs/sample.md and summarise it",
                "Write 'Hello from the agent!' to workspace/output/hello.txt",
                "Send an email to alice@aidevs.pl with subject 'Hello' and a short greeting",
                "Search for any markdown files in the workspace"
            };
            foreach (string ex in examples)
                ColorLine("  • " + ex, ConsoleColor.DarkGray);
            Console.WriteLine();
        }
    }
}
