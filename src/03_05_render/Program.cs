using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FourthDevs.Render.Agent;
using FourthDevs.Render.Core;
using FourthDevs.Render.Models;

namespace FourthDevs.Render
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
            Console.WriteLine("  Lesson 15 – Render Agent (03_05_render)");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            const string host = "localhost";
            const int    port = 3502;

            using (var server = new PreviewServer(host, port))
            {
                server.Start();

                string serverUrl = server.Url;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("  Preview: " + serverUrl);
                Console.ResetColor();
                Console.WriteLine();

                OpenBrowser(serverUrl);

                await RunCli(server);
            }
        }

        private static async Task RunCli(PreviewServer server)
        {
            RenderDocument currentDocument = null;

            Console.WriteLine("Render agent ready. Describe a dashboard to create, or 'exit'/'quit' to stop.");
            Console.WriteLine();

            while (true)
            {
                Console.Write("You: ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "quit", StringComparison.OrdinalIgnoreCase))
                    break;

                try
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("[agent] Processing…");
                    Console.ResetColor();

                    AgentTurnResult result = await AgentRunner.RunTurnAsync(input, currentDocument);

                    if (result.Kind == "render" && result.Document != null)
                    {
                        currentDocument = result.Document;
                        server.UpdateDocument(currentDocument);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(string.Format(
                            "\n[render] \"{0}\" (packs: {1})",
                            currentDocument.Title,
                            string.Join(", ", currentDocument.Packs)));

                        if (!string.IsNullOrEmpty(currentDocument.Summary))
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            Console.WriteLine(currentDocument.Summary);
                        }
                        Console.ResetColor();
                    }

                    if (!string.IsNullOrWhiteSpace(result.Text))
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine("\nAgent: " + result.Text);
                        Console.ResetColor();
                    }

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

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
                return;
            }
            catch { }

            try
            {
                Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
                return;
            }
            catch { }

            try
            {
                Process.Start(new ProcessStartInfo("open", url) { UseShellExecute = false });
            }
            catch { }
        }
    }
}
