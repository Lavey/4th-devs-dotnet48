using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Code.Agent;
using FourthDevs.Code.Core;
using FourthDevs.Code.Models;
using FourthDevs.Code.Prompts;
using FourthDevs.Code.Tools;

namespace FourthDevs.Code
{
    /// <summary>
    /// Lesson 12 – Code Execution Agent (03_02_code)
    ///
    /// An agent that executes TypeScript code in a Deno sandbox,
    /// with MCP file-server integration and an HTTP bridge for
    /// host-side tool access.
    ///
    /// Default task: generate a styled PDF cost report from workspace data.
    ///
    /// Source: 03_02_code/src/index.ts (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string DefaultModel = "gpt-4.1";

        private const string DefaultTask =
            "Read the knowledge files and data files in the workspace, " +
            "then generate a styled PDF cost report and save it as report.pdf " +
            "in the workspace root.";

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // ── Confirmation prompt ────────────────────────────────────────
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ⚠ UWAGA: To demo wykonuje zapytania do API OpenAI/OpenRouter");
            Console.WriteLine("  i uruchamia kod w sandboxie Deno.");
            Console.ResetColor();
            Console.WriteLine();
            Console.Write("  Czy chcesz kontynuować? (t/N): ");
            string answer = Console.ReadLine();
            if (answer == null || (answer.Trim().ToLowerInvariant() != "t"
                && answer.Trim().ToLowerInvariant() != "y"
                && answer.Trim().ToLowerInvariant() != "tak"
                && answer.Trim().ToLowerInvariant() != "yes"))
            {
                Console.WriteLine("  Przerwano.");
                return;
            }
            Console.WriteLine();

            // ── Configuration ──────────────────────────────────────────────
            string permLevelStr = Cfg("PERMISSION_LEVEL") ?? "standard";
            PermissionLevel permLevel = ParsePermissionLevel(permLevelStr);
            string model = Cfg("OPENAI_MODEL") ?? DefaultModel;

            Console.WriteLine("========================================");
            Console.WriteLine("  Code Execution Agent");
            Console.WriteLine("========================================");
            Console.WriteLine($"  Model:      {model}");
            Console.WriteLine($"  Permission: {permLevel}");
            Console.WriteLine();

            // ── Workspace ──────────────────────────────────────────────────
            string workspacePath = GetWorkspacePath();
            Directory.CreateDirectory(workspacePath);
            Directory.CreateDirectory(Path.Combine(workspacePath, "knowledge"));
            Directory.CreateDirectory(Path.Combine(workspacePath, "data"));
            Console.WriteLine($"  Workspace:  {workspacePath}");

            // ── MCP client ─────────────────────────────────────────────────
            string mcpJsonPath = FindMcpJson(workspacePath);
            McpClient mcpClient = null;
            List<McpToolInfo> mcpTools = new List<McpToolInfo>();

            if (mcpJsonPath != null)
            {
                Console.WriteLine($"  MCP config: {mcpJsonPath}");
                try
                {
                    mcpClient = McpClient.FromConfig(mcpJsonPath);
                    await mcpClient.InitializeAsync();
                    mcpTools = await mcpClient.ListToolsAsync();
                    Console.WriteLine($"  MCP tools:  {mcpTools.Count} tool(s) from '{mcpClient.ServerName}'");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[mcp] Failed to connect: " + ex.Message);
                    Console.Error.WriteLine("[mcp] Continuing without MCP tools.");
                    if (mcpClient != null) { mcpClient.Dispose(); mcpClient = null; }
                }
            }
            else
            {
                Console.WriteLine("  MCP config: not found (no mcp.json)");
            }
            Console.WriteLine();

            // ── Deno sandbox ───────────────────────────────────────────────
            try
            {
                await Sandbox.EnsureSandboxAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[sandbox] " + ex.Message);
                Console.Error.WriteLine("[sandbox] Cannot continue without Deno.");
                Cleanup(mcpClient, null);
                return;
            }

            // ── Build tools ────────────────────────────────────────────────
            List<LocalToolDefinition> allTools;
            if (mcpClient != null)
                allTools = ToolFactory.CreateMcpTools(mcpClient, mcpTools);
            else
                allTools = new List<LocalToolDefinition>();

            // ── HTTP bridge ────────────────────────────────────────────────
            Bridge bridge = null;
            string prelude = string.Empty;
            int bridgePort = 0;

            if (allTools.Count > 0)
            {
                bridge = new Bridge(allTools);
                bridge.Start();
                bridgePort = bridge.Port;
                prelude = Bridge.GeneratePrelude(bridgePort, allTools);
            }

            // ── Sandbox options ────────────────────────────────────────────
            var sandboxOptions = new SandboxOptions
            {
                Timeout = 60000,
                PermissionLevel = permLevel,
                Workspace = workspacePath,
                Prelude = prelude,
                BridgePort = bridgePort
            };

            // ── Code execution tool ────────────────────────────────────────
            var codeTool = ToolFactory.CreateCodeTool(sandboxOptions);
            allTools.Add(codeTool);

            // ── Task ───────────────────────────────────────────────────────
            string task = args.Length > 0
                ? string.Join(" ", args)
                : DefaultTask;

            Console.WriteLine($"  Task: {task}");
            Console.WriteLine();

            // ── System prompt ──────────────────────────────────────────────
            string systemPrompt = SystemPrompt.Build(permLevel);

            // ── Agent loop ─────────────────────────────────────────────────
            try
            {
                AgentResult result = await AgentRunner.RunAsync(model, systemPrompt, task, allTools);

                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("  Result");
                Console.WriteLine("========================================");
                Console.WriteLine();
                Console.WriteLine(result.Text);
                Console.WriteLine();
                Console.WriteLine($"  Turns: {result.Turns}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("[agent] Fatal error: " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
            }
            finally
            {
                Cleanup(mcpClient, bridge);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────────────

        private static PermissionLevel ParsePermissionLevel(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "safe": return PermissionLevel.Safe;
                case "network": return PermissionLevel.Network;
                case "full": return PermissionLevel.Full;
                case "standard":
                default: return PermissionLevel.Standard;
            }
        }

        private static string GetWorkspacePath()
        {
            string configured = Cfg("WORKSPACE_PATH");
            if (!string.IsNullOrWhiteSpace(configured))
                return Path.GetFullPath(configured);

            // Default: workspace/ next to the executable
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exeDir, "workspace");
        }

        private static string FindMcpJson(string workspacePath)
        {
            // Check next to the executable first
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string path1 = Path.Combine(exeDir, "mcp.json");
            if (File.Exists(path1)) return path1;

            // Check in workspace directory
            string path2 = Path.Combine(workspacePath, "mcp.json");
            if (File.Exists(path2)) return path2;

            // Check project directory (for development)
            string projectDir = FindProjectDirectory();
            if (projectDir != null)
            {
                string path3 = Path.Combine(projectDir, "mcp.json");
                if (File.Exists(path3)) return path3;
            }

            return null;
        }

        private static string FindProjectDirectory()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                if (File.Exists(Path.Combine(dir, "03_02_code.csproj")))
                    return dir;
                string parent = Path.GetDirectoryName(dir);
                if (parent == null || parent == dir) break;
                dir = parent;
            }
            return null;
        }

        private static void Cleanup(McpClient mcpClient, Bridge bridge)
        {
            if (bridge != null)
            {
                try { bridge.Dispose(); } catch { }
            }
            if (mcpClient != null)
            {
                try { mcpClient.Dispose(); } catch { }
            }
        }

        private static string Cfg(string key)
        {
            string val = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
        }
    }
}
