using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using FourthDevs.Lesson07_HybridRag.Agent;
using FourthDevs.Lesson07_HybridRag.Db;

namespace FourthDevs.Lesson07_HybridRag
{
    /// <summary>
    /// Interactive REPL for the Hybrid RAG agent.
    /// Special commands: 'exit' | 'clear' | 'reindex'
    ///
    /// Mirrors 02_02_hybrid_rag/src/repl.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Repl
    {
        internal static async Task RunAsync(SQLiteConnection db, string workspacePath)
        {
            var history = new List<object>();

            while (true)
            {
                Console.Write("You: ");
                string input = Console.ReadLine();

                if (input == null) break; // EOF

                string trimmed = input.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                string lower = trimmed.ToLowerInvariant();

                if (lower == "exit" || lower == "quit")
                    break;

                if (lower == "clear")
                {
                    history.Clear();
                    ColorLine("  [Conversation cleared]\n", ConsoleColor.DarkGray);
                    continue;
                }

                if (lower == "reindex")
                {
                    ColorLine("  [Re-indexing workspace...]\n", ConsoleColor.DarkGray);
                    await Indexer.IndexWorkspaceAsync(db, workspacePath);
                    ColorLine("  [Re-indexing complete]\n", ConsoleColor.DarkGray);
                    continue;
                }

                try
                {
                    AgentResult result = await HybridAgent.RunAsync(trimmed, history, db);
                    history = result.ConversationHistory;

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Assistant: ");
                    Console.ResetColor();
                    Console.WriteLine(result.Response);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    ColorLine("  [Error] " + ex.Message + "\n", ConsoleColor.Red);
                }
            }
        }

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
