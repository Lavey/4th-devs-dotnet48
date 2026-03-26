using System;
using System.Collections.Generic;
using FourthDevs.Video.Agent;
using FourthDevs.Video.Native;

namespace FourthDevs.Video
{
    internal static class Program
    {
        private const string DefaultModel = "gpt-4.1";

        static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  Lesson 04 – Video Processing Agent (01_04_video)");
            Console.WriteLine("=================================================");
            Console.WriteLine();
            Console.WriteLine("Type a request to analyze a video, or:");
            Console.WriteLine("  'clear' – reset conversation history");
            Console.WriteLine("  'exit'  – quit");
            Console.WriteLine();

            var tools = VideoTools.CreateTools();
            var conversation = new List<object>();
            AgentRunner.InitConversation(conversation);

            while (true)
            {
                Console.Write("You: ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input)) continue;

                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    break;

                if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    conversation.Clear();
                    AgentRunner.InitConversation(conversation);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[Conversation cleared]");
                    Console.ResetColor();
                    Console.WriteLine();
                    continue;
                }

                try
                {
                    string response = AgentRunner.RunAsync(DefaultModel, input, tools, conversation)
                        .GetAwaiter().GetResult();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\nAgent: " + response);
                    Console.ResetColor();
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[error] " + ex.Message);
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }

            Console.WriteLine("Goodbye.");
        }
    }
}
