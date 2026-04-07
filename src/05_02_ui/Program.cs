using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.ChatUi.Server;

namespace FourthDevs.ChatUi
{
    /// <summary>
    /// 05_02_ui — Streaming Chat UI with mock agent backend and SSE streaming.
    /// Port of: i-am-alice/4th-devs/05_02_ui (Svelte 5 + Bun → .NET 4.8 + HttpListener)
    /// </summary>
    internal static class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".data");
            Directory.CreateDirectory(dataDir);

            using (var server = new ChatServer(dataDir))
            {
                server.Start();

                string url = "http://localhost:3300";
                Console.WriteLine("[05_02_ui] server at {0} — Ctrl+C to stop", url);

                OpenBrowser(url);

                var exit = new ManualResetEventSlim(false);
                Console.CancelKeyPress += delegate(object s, ConsoleCancelEventArgs e)
                {
                    e.Cancel = true;
                    exit.Set();
                };

                exit.Wait();
                Console.WriteLine("[05_02_ui] shutting down...");
            }
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch
            {
                // Browser open is best-effort
            }
        }
    }
}
