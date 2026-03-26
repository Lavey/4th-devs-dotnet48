using System;
using System.Collections.Generic;
using FourthDevs.Gmail.Agent;
using FourthDevs.Gmail.Gmail;
using FourthDevs.Gmail.Models;
using FourthDevs.Gmail.Tools;

namespace FourthDevs.Gmail
{
    internal static class Program
    {
        private const string DefaultModel = "gpt-4.1";

        static void Main(string[] args)
        {
            PrintBanner();

            bool isAuth = args.Length > 0 &&
                          string.Equals(args[0], "auth", StringComparison.OrdinalIgnoreCase);

            if (isAuth)
            {
                AuthFlow();
            }
            else
            {
                ChatFlow();
            }
        }

        private static void AuthFlow()
        {
            Console.WriteLine("Opening browser for Gmail OAuth consent flow...");
            Console.WriteLine();

            try
            {
                GmailAuth.RunAuthFlow();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Authentication complete. Token saved to workspace/auth/gmail-token.json");
                Console.ResetColor();
                Console.WriteLine("You can now run the agent without the 'auth' argument.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[error] " + ex.Message);
                Console.ResetColor();
            }
        }

        private static void ChatFlow()
        {
            string accessToken;
            try
            {
                accessToken = GmailAuth.GetValidAccessToken();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[warn] " + ex.Message);
                Console.WriteLine("[warn] Run with 'auth' argument to authenticate.");
                Console.ResetColor();
                return;
            }

            var gmailTools = GmailTools.CreateTools(accessToken);

            var conversation = new List<object>();
            AgentRunner.InitConversation(conversation);

            Console.WriteLine("Gmail agent ready. Type your question or 'exit'/'quit' to stop.");
            Console.WriteLine("  Tip: Run with 'auth' argument to (re)authenticate with Google first.");
            Console.WriteLine();

            while (true)
            {
                Console.Write("You: ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input)) continue;

                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    break;

                try
                {
                    var result = AgentRunner.RunAsync(DefaultModel, input, gmailTools, conversation)
                        .GetAwaiter().GetResult();

                    conversation = result.ConversationHistory;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\nAgent: " + result.Text);
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

        private static void PrintBanner()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  Lesson – Gmail Agent (03_04_gmail)");
            Console.WriteLine("=================================================");
            Console.WriteLine();
        }
    }
}
