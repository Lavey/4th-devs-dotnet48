using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Neo4j.Driver;
using FourthDevs.Lesson08_GraphAgents.Agent;
using FourthDevs.Lesson08_GraphAgents.Graph;
using FourthDevs.Lesson08_GraphAgents.Helpers;

namespace FourthDevs.Lesson08_GraphAgents
{
    /// <summary>
    /// Interactive REPL for the Graph RAG agent.
    /// Special commands: 'exit' | 'clear' | 'reindex' | 'reindex --force'
    /// Mirrors 02_03_graph_agents/src/repl.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Repl
    {
        internal static async Task RunAsync(IDriver driver, string workspacePath)
        {
            var history = new List<object>();

            while (true)
            {
                Console.Write("You: ");
                string input = Console.ReadLine();

                if (input == null) break; // EOF / Ctrl+Z

                string trimmed = input.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                string lower = trimmed.ToLowerInvariant();

                if (lower == "exit" || lower == "quit")
                    break;

                if (lower == "clear")
                {
                    history.Clear();
                    Stats.ResetStats();
                    Logger.Success("Conversation cleared\n");
                    continue;
                }

                if (lower.StartsWith("reindex"))
                {
                    bool force = lower.Contains("--force");
                    if (force)
                    {
                        Logger.Start("Clearing graph...");
                        await Indexer.ClearGraphAsync(driver);
                    }
                    Logger.Start("Re-indexing workspace...");
                    await Indexer.IndexWorkspaceAsync(driver, workspacePath);
                    Logger.Success("Re-indexing complete\n");
                    continue;
                }

                try
                {
                    var result = await AgentLoop.RunAsync(trimmed, history, driver);
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
                    Logger.Error("Error", ex.Message);
                    Console.WriteLine();
                }
            }
        }
    }
}
