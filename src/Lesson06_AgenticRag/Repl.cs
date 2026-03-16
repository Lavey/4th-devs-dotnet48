using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FourthDevs.Lesson06_AgenticRag
{
    /// <summary>
    /// Interactive REPL for the Agentic RAG agent.
    /// Reads user queries, delegates them to the agent, and maintains
    /// conversation history across turns.
    ///
    /// Special commands: 'exit' | 'clear'
    ///
    /// Mirrors 02_01_agentic_rag/src/repl.js in the source repo.
    /// </summary>
    internal static class Repl
    {
        /// <summary>
        /// Runs the interactive REPL loop until the user types 'exit'.
        /// </summary>
        internal static async Task RunAsync()
        {
            var history = new List<object>();

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
                    history.Clear();
                    ColorLine("  [Conversation cleared]\n", ConsoleColor.DarkGray);
                    continue;
                }

                try
                {
                    AgentResult result = await Agent.RunAsync(trimmed, history);
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

        // ----------------------------------------------------------------
        // Console helper
        // ----------------------------------------------------------------

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
