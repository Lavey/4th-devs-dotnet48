using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.Events.Autonomy;
using FourthDevs.Events.Config;
using FourthDevs.Events.Core;
using FourthDevs.Events.Features;
using FourthDevs.Events.Mcp;
using FourthDevs.Events.Workflows;

namespace FourthDevs.Events
{
    internal class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            // Parse flags
            int rounds = GetIntFlag(args, "--rounds", 8);
            int delayMs = GetIntFlag(args, "--delay-ms", 750);
            bool autoHuman = GetBoolFlag(args, "--auto-human", true);
            string workflowId = GetStringFlag(args, "--workflow", null);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== 03_02_events: Multi-Agent Event Architecture ===");
            Console.ResetColor();
            Console.WriteLine();

            // Validate API key
            string apiKey = FourthDevs.Common.AiConfig.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: AI_API_KEY / OPENAI_API_KEY is not configured.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\u26A0 UWAGA: To demo uruchomi wielu agent\u00F3w, kt\u00F3rzy b\u0119d\u0105 wykonywa\u0107 zapytania do API.");
            Console.WriteLine("  Rounds: " + rounds + " | Delay: " + delayMs + "ms | Auto-human: " + autoHuman);
            Console.ResetColor();
            Console.WriteLine();
            Console.Write("Continue? [Y/n] ");
            string confirm = Console.ReadLine();
            if (!string.IsNullOrEmpty(confirm) &&
                !confirm.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) &&
                !confirm.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted.");
                return;
            }

            // Resolve workflow
            Logger.Info("main", "Resolving workflow...");
            var resolution = AutonomyRuntime.Resolve(workflowId);

            if (resolution.Mode == "no-go")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("NO-GO: " + resolution.NoGoMessage);
                Console.ResetColor();
                return;
            }

            var workflow = resolution.Workflow;
            Logger.Info("main", "Using workflow: " + workflow.Id + " (" + resolution.Mode + " mode)");
            Logger.Info("main", "Agents: " + string.Join(", ", workflow.AgentOrder));

            // Initialize workspace
            Bootstrap.EnsureWorkspace(workflow);

            // Try to create MCP manager
            McpManager mcp = null;
            string mcpJsonPath = Path.Combine(EnvConfig.WorkspacePath, ".mcp.json");
            if (File.Exists(mcpJsonPath))
            {
                mcp = McpManager.FromConfigFile(mcpJsonPath);
                if (mcp != null)
                {
                    await mcp.InitializeAllAsync();
                    Logger.Info("main", "MCP initialized with " + mcp.AllTools.Count + " tool(s)");
                }
            }

            // Setup graceful shutdown
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Logger.Info("main", "Shutdown requested...");
                cts.Cancel();
            };

            try
            {
                // Run heartbeat loop
                await HeartbeatLoop.RunAsync(workflow, rounds, delayMs, autoHuman, mcp, cts.Token);

                Logger.Info("main", "Heartbeat loop finished.");

                // Print summary
                Console.WriteLine();
                var counts = TaskManager.CountByStatus();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=== Task Summary ===");
                foreach (var kv in counts)
                    Console.WriteLine("  " + kv.Key + ": " + kv.Value);
                Console.ResetColor();
            }
            catch (OperationCanceledException)
            {
                Logger.Info("main", "Cancelled.");
            }
            catch (Exception ex)
            {
                Logger.Error("main", "Fatal: " + ex.Message);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: " + ex);
                Console.ResetColor();
            }
            finally
            {
                if (mcp != null) mcp.Dispose();
            }
        }

        // ---- Flag parsing helpers ----

        private static string GetStringFlag(string[] args, string name, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
                if (args[i].StartsWith(name + "="))
                    return args[i].Substring(name.Length + 1);
            }
            // Check last arg for = syntax
            if (args.Length > 0 && args[args.Length - 1].StartsWith(name + "="))
                return args[args.Length - 1].Substring(name.Length + 1);
            return defaultValue;
        }

        private static int GetIntFlag(string[] args, string name, int defaultValue)
        {
            string val = GetStringFlag(args, name, null);
            return EnvConfig.ParsePositiveInt(val, defaultValue);
        }

        private static bool GetBoolFlag(string[] args, string name, bool defaultValue)
        {
            string val = GetStringFlag(args, name, null);
            return EnvConfig.ParseBoolean(val, defaultValue);
        }
    }
}
