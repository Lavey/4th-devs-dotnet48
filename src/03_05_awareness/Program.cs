using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Awareness.Agent;
using FourthDevs.Awareness.Core;
using FourthDevs.Awareness.Models;

namespace FourthDevs.Awareness
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  Lesson 15 – Awareness Agent (03_05_awareness)");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            try
            {
                WorkspaceInit.EnsureWorkspace();

                List<ChatLogEntry> history = await ChatHistory.LoadRecentHistoryAsync(16);
                List<Message> messages = ChatHistory.HistoryToMessages(history);

                string sessionId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                Session session = AgentRunner.CreateSession(sessionId, messages);

                Console.WriteLine("Workspace ready. Type your message (or 'exit' to quit).");
                Console.WriteLine();

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("You: ");
                    Console.ResetColor();

                    string input = Console.ReadLine();
                    if (input == null) break;
                    input = input.Trim();

                    if (string.IsNullOrEmpty(input)) continue;
                    if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(input, "quit", StringComparison.OrdinalIgnoreCase))
                        break;

                    try
                    {
                        AgentResponse response = await AgentRunner.RunTurnAsync(session, input);

                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("Agent: ");
                        Console.ResetColor();
                        Console.WriteLine(response.Text);
                        Console.WriteLine();

                        await ChatHistory.AppendConversationLogsAsync(sessionId, input, response.Text);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: " + ex.Message);
                        Console.ResetColor();
                    }
                }

                Console.WriteLine("Goodbye.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Fatal error: " + ex.Message);
                Console.ResetColor();
                Environment.Exit(1);
            }
        }
    }
}
