using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Artifacts.Agent;
using FourthDevs.Artifacts.Core;
using FourthDevs.Artifacts.Models;

namespace FourthDevs.Artifacts
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
            Console.WriteLine("  Lesson 15 – Artifact Agent (03_05_artifacts)");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            const string host = "localhost";
            const int port = 3501;

            using (var server = new PreviewServer(host, port))
            {
                server.Start();

                string serverUrl = server.Url;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("  Preview: " + serverUrl);
                Console.ResetColor();
                Console.WriteLine();

                OpenBrowser(serverUrl);

                await RunCli(server, serverUrl);
            }
        }

        private static async Task RunCli(PreviewServer server, string serverBaseUrl)
        {
            ArtifactDocument currentArtifact = null;

            Console.WriteLine("Artifact agent ready. Describe what you want to build, or 'exit'/'quit' to stop.");
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

                    AgentTurnResult result = await AgentRunner.RunTurnAsync(
                        input, currentArtifact, serverBaseUrl);

                    if (result.Kind == "artifact" && result.Artifact != null)
                    {
                        currentArtifact = result.Artifact;
                        server.UpdateArtifact(currentArtifact);

                        string action = result.Action == "edited" ? "edited" : "created";
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(string.Format(
                            "\n[{0}] \"{1}\" (packs: {2})",
                            action,
                            currentArtifact.Title,
                            string.Join(", ", currentArtifact.Packs)));
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
