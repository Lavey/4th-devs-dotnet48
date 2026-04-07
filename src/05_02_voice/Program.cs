using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.VoiceAgent.Core;
using FourthDevs.VoiceAgent.Server;

namespace FourthDevs.VoiceAgent
{
    /// <summary>
    /// 05_02_voice — Voice Agent with LiveKit, MCP tools, and multiple voice
    /// provider modes (Gemini Realtime, OpenAI, ElevenLabs).
    /// Port of: i-am-alice/4th-devs/05_02_voice (Node.js + Hono → .NET 4.8 + HttpListener)
    /// </summary>
    internal static class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var mode = VoiceModeResolver.Resolve();
            Console.WriteLine("[05_02_voice] Voice mode: " + mode.Label + " (" + mode.Id + ")");

            if (mode.Status != "ready")
            {
                Console.WriteLine("[05_02_voice] Warning: " + (mode.Error ?? mode.Description));
            }

            var mcpServers = McpConfig.Load(AppDomain.CurrentDomain.BaseDirectory);
            if (mcpServers.Count > 0)
            {
                Console.WriteLine("[05_02_voice] MCP servers found: " + string.Join(", ", mcpServers.Keys));
            }

            using (var server = new TokenServer())
            {
                server.Start();

                string url = string.Format("http://localhost:{0}", server.Port);
                Console.WriteLine("[05_02_voice] Token server at {0} — Ctrl+C to stop", url);
                Console.WriteLine("[05_02_voice] Note: LiveKit agent requires a running LiveKit server");

                OpenBrowser(url);

                var exit = new ManualResetEventSlim(false);
                Console.CancelKeyPress += delegate(object s, ConsoleCancelEventArgs e)
                {
                    e.Cancel = true;
                    exit.Set();
                };

                exit.Wait();
                Console.WriteLine("[05_02_voice] shutting down...");
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
