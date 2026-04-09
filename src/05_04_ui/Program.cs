using System;
using System.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.ChatApp.Server;

namespace FourthDevs.ChatApp
{
    /// <summary>
    /// 05_04_ui — Chat Application UI with thread management, agent selection,
    /// and streaming AI responses.
    /// Port of: i-am-alice/4th-devs/05_04_ui (Svelte 5 → .NET 4.8 + HttpListener)
    /// </summary>
    internal static class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            int port = 5173;
            string portStr = ConfigurationManager.AppSettings["UI_PORT"];
            int parsed;
            if (!string.IsNullOrEmpty(portStr) && int.TryParse(portStr, out parsed))
                port = parsed;

            string apiBaseUrl = ConfigurationManager.AppSettings["API_BASE_URL"]
                ?? "http://127.0.0.1:3000/v1";

            using (var server = new UiServer(port, apiBaseUrl))
            {
                server.Start();

                string url = "http://localhost:" + port;
                Console.WriteLine("[05_04_ui] server at {0} — Ctrl+C to stop", url);
                Console.WriteLine("[05_04_ui] proxying API to {0}", apiBaseUrl);

                OpenBrowser(url);

                var exit = new ManualResetEventSlim(false);
                Console.CancelKeyPress += delegate(object s, ConsoleCancelEventArgs e)
                {
                    e.Cancel = true;
                    exit.Set();
                };

                exit.Wait();
                Console.WriteLine("[05_04_ui] shutting down...");
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
