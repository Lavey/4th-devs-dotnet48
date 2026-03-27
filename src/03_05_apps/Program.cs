using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Apps.Agent;
using FourthDevs.Apps.Core;
using FourthDevs.Apps.Models;

namespace FourthDevs.Apps
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
            Console.WriteLine("  Lesson 15 – List Manager (03_05_apps)");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            // Resolve paths relative to the executable directory so that
            // todo.md / shopping.md are always next to the binary.
            string baseDir      = AppDomain.CurrentDomain.BaseDirectory;
            string todoPath     = Path.Combine(baseDir, "todo.md");
            string shoppingPath = Path.Combine(baseDir, "shopping.md");

            ListFiles.EnsureListFiles(todoPath, shoppingPath);

            const string host    = "localhost";
            const int    uiPort  = 3500;

            var server = new UiServer(host, uiPort, todoPath, shoppingPath);
            server.Start();

            string uiUrl  = server.Url;
            string mcpUrl = string.Format("http://{0}:{1}/mcp", host, uiPort);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  UI  : " + uiUrl);
            Console.WriteLine("  MCP : " + mcpUrl);
            Console.ResetColor();
            Console.WriteLine();

            OpenBrowser(uiUrl);

            await RunCli(todoPath, shoppingPath, uiUrl);

            server.Stop();
        }

        private static async Task RunCli(string todoPath, string shoppingPath, string uiUrl)
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

                if (string.Equals(input, "exit",  StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "quit",  StringComparison.OrdinalIgnoreCase))
                    break;

                string listsSummary;
                try
                {
                    ListsState state = ListFiles.ReadListsState(todoPath, shoppingPath);
                    listsSummary = ListFiles.SummarizeLists(state);
                }
                catch
                {
                    listsSummary = string.Empty;
                }

                AgentTurnResult result;
                try
                {
                    result = await AgentRunner.RunTurnAsync(input, listsSummary);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: " + ex.Message);
                    Console.ResetColor();
                    continue;
                }

                if (result.Kind == "open_manager")
                {
                    string focus     = result.Focus ?? "todo";
                    string targetUrl = uiUrl + "?focus=" + focus;
                    OpenBrowser(targetUrl);
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Agent: ");
                Console.ResetColor();
                Console.WriteLine(result.Text);
                Console.WriteLine();
            }
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                if (IsWindows())
                {
                    // Use cmd /c start to avoid issues with special chars in the URL
                    Process.Start(new ProcessStartInfo("cmd", "/c start \"\" \"" + url + "\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                else if (IsMac())
                {
                    Process.Start("open", url);
                }
                else
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("[browser] Could not open browser: " + ex.Message);
                Console.ResetColor();
            }
        }

        private static bool IsWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ||
                   Environment.OSVersion.Platform == PlatformID.Win32Windows;
        }

        private static bool IsMac()
        {
            return Environment.OSVersion.Platform == PlatformID.MacOSX;
        }
    }
}
