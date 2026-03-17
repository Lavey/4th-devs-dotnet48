using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using Aype.AI.AgentHybridRag.Agent;
using Aype.AI.AgentHybridRag.Db;
using Aype.AI.Common.Models;

namespace Aype.AI.AgentHybridRag
{
    /// <summary>
    /// Interactive REPL for the Hybrid RAG agent.
    /// Special commands: 'exit' | 'clear' | 'reindex'
    /// </summary>
    internal static class Repl
    {
        internal static async Task RunAsync(SQLiteConnection db, string workspacePath)
        {
            var history = new List<InputMessage>();

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
