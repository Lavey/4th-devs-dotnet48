using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FourthDevs.McpApps.Agent;
using FourthDevs.McpApps.Core;
using FourthDevs.McpApps.Store;

namespace FourthDevs.McpApps
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
            Console.WriteLine("  Marketing Ops Agent (04_05_apps)");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            TodoStore.EnsureWorkspace();

            const string host = "localhost";
            const int port = 4500;

            var server = new AppServer(host, port);
            server.Start();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  UI : " + server.Url);
            Console.ResetColor();
            Console.WriteLine();

            OpenBrowser(server.Url);

            await RunCli();

            server.Stop();
        }

        private static async Task RunCli()
        {
            Console.WriteLine("Type your message (or 'exit' to quit).");
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
                    var result = await AgentRunner.RunTurnAsync(input, null);
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("Agent: ");
                    Console.ResetColor();
                    Console.WriteLine(result.Text);
                    if (result.ToolExecutions != null && result.ToolExecutions.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("  [" + result.ToolExecutions.Count + " tool call(s)]");
                        Console.ResetColor();
                    }
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: " + ex.Message);
                    Console.ResetColor();
                }
            }
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    Process.Start(new ProcessStartInfo("cmd", "/c start \"\" \"" + url + "\"") { CreateNoWindow = true, UseShellExecute = false });
                else if (Environment.OSVersion.Platform == PlatformID.MacOSX)
                    Process.Start("open", url);
                else
                    Process.Start("xdg-open", url);
            }
            catch { }
        }
    }
}
