using System;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Lesson06_AgenticRag.Tools;

namespace FourthDevs.Lesson06_AgenticRag
{
    /// <summary>
    /// Lesson 06 – Agentic RAG
    ///
    /// Interactive REPL where an AI agent answers questions by iteratively
    /// exploring, searching, and reading documents in the workspace/ folder.
    ///
    /// The agent uses three file tools:
    ///   list_files   — discover what documents are available
    ///   search_files — find relevant content by keyword
    ///   read_file    — read a specific document
    ///
    /// Conversation history is maintained between turns so follow-up
    /// questions work naturally.
    ///
    /// REPL commands: 'exit' | 'clear'
    ///
    /// Source: 02_01_agentic_rag/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            string workspaceRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "workspace");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, "docs"));

            // Inject workspace root into tool executors
            ToolExecutors.WorkspaceRoot = workspaceRoot;

            PrintBanner(workspaceRoot);

            if (!ConfirmRun()) return;

            await Repl.RunAsync();
        }

        // ----------------------------------------------------------------
        // Startup confirmation (mirrors app.js confirmRun)
        // ----------------------------------------------------------------

        static bool ConfirmRun()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                "\n⚠  NOTE: Running this agent may consume a noticeable number of tokens.");
            Console.WriteLine(
                "   Put your documents in the workspace/docs/ folder before starting.");
            Console.ResetColor();
            Console.WriteLine();
            Console.Write("Continue? (yes/y): ");
            string answer = Console.ReadLine()?.Trim().ToLowerInvariant() ?? string.Empty;

            if (answer != "yes" && answer != "y")
            {
                Console.WriteLine("Aborted.");
                return false;
            }

            Console.WriteLine();
            return true;
        }

        // ----------------------------------------------------------------
        // Banner
        // ----------------------------------------------------------------

        static void PrintBanner(string workspaceRoot)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║        Agentic RAG  (Lesson 06)          ║");
            Console.WriteLine("║  Commands: 'exit' | 'clear'              ║");
            Console.WriteLine("╚══════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Workspace: " + workspaceRoot);
            Console.WriteLine(
                "Place documents in workspace/docs/ to make them searchable.\n");
        }
    }
}
