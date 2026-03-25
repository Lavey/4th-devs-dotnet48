using System;
using System.Collections.Generic;
using FourthDevs.Browser.Agent;
using FourthDevs.Browser.Browser;
using FourthDevs.Browser.Models;
using FourthDevs.Browser.Tools;

namespace FourthDevs.Browser
{
    internal static class Program
    {
        private const string DefaultModel = "gpt-4.1";

        static void Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLower() : "chat";

            Console.WriteLine("=================================================");
            Console.WriteLine("  Lesson 13 – Browser Agent (03_03_browser)");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            switch (mode)
            {
                case "login":
                    LoginFlow();
                    break;
                default:
                    ChatFlow();
                    break;
            }
        }

        private static void LoginFlow()
        {
            Console.WriteLine("[login] Opening browser for Goodreads login...");
            Console.WriteLine("[login] Please log in to Goodreads in the browser window.");
            Console.WriteLine("[login] Press Enter here when you are done.");
            Console.WriteLine();

            BrowserManager.Launch(headless: false);
            BrowserManager.Navigate("https://www.goodreads.com");

            Console.ReadLine();

            BrowserManager.Close();
            Console.WriteLine("[login] Session saved via Chrome profile. You can now run the chat agent.");
        }

        private static void ChatFlow()
        {
            if (!BrowserManager.SessionExists())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[warn] No Chrome session found. Run with 'login' argument first for authenticated Goodreads access.");
                Console.ResetColor();
                Console.WriteLine();
            }

            BrowserManager.Launch(headless: true);

            var tools = new List<LocalToolDefinition>();
            tools.AddRange(BrowserTools.CreateBrowserTools());
            tools.AddRange(FileTools.CreateFileTools());

            Console.WriteLine("Browser agent ready. Type your question or 'exit'/'quit' to stop.");
            Console.WriteLine("  Tip: Run with 'login' argument to authenticate with Goodreads first.");
            Console.WriteLine();

            string lastResponseId = null;

            while (true)
            {
                Console.Write("You: ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input)) continue;

                if (input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                try
                {
                    var result = AgentRunner.RunAsync(DefaultModel, input, tools, lastResponseId)
                        .GetAwaiter().GetResult();

                    lastResponseId = result.ResponseId;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\nAgent: {result.Text}");
                    Console.ResetColor();
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[error] {ex.Message}");
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }

            BrowserManager.Close();
            Console.WriteLine("Goodbye.");
        }
    }
}
