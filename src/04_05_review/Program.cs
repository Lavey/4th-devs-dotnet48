using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Review.Core;

namespace FourthDevs.Review
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  Markdown Review Lab (04_05_review)");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string workspacePath = Path.Combine(baseDir, "workspace");

            if (!Directory.Exists(workspacePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Workspace not found at: " + workspacePath);
                Console.ResetColor();
                return;
            }

            Store.Init(workspacePath);

            const string host = "localhost";
            const int port = 4405;

            var server = new ReviewServer(host, port);
            server.Start();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  Server: " + server.Url);
            Console.ResetColor();
            Console.WriteLine();

            OpenBrowser(server.Url);

            Console.WriteLine("Press Enter to stop the server…");
            Console.ReadLine();

            server.Stop();
            Console.WriteLine("Server stopped.");
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                if (IsWindows())
                {
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
