using System;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.CodingAgent.Agent;
using FourthDevs.CodingAgent.Config;
using FourthDevs.CodingAgent.Logging;
using FourthDevs.CodingAgent.Models;
using FourthDevs.CodingAgent.Tools;

namespace FourthDevs.CodingAgent
{
    /// <summary>
    /// Interactive CLI for the coding agent.
    /// Mirrors index.ts from the TypeScript original.
    /// </summary>
    internal static class Program
    {
        private const string Cyan  = "\x1b[36m";
        private const string Green = "\x1b[32m";
        private const string Dim   = "\x1b[2m";
        private const string Red   = "\x1b[31m";
        private const string Reset = "\x1b[0m";

        private static readonly string Welcome = string.Format(@"
========================================
  05_03 Coding Agent (.NET 4.8)
========================================

  A small coding agent with:
  - an explicit agent loop
  - filesystem tools
  - rolling memory
  - structured logs

  Commands:
    {0}/demo{3}   - Build a Snake game
    {0}/clear{3}  - Start a new session
    {0}/quit{3}   - Exit

  The agent works inside {1}workspace/{3}.
", Cyan, Dim, Green, Reset);

        private static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            Console.WriteLine(Welcome);

            // Ensure workspace exists
            string workspace = AgentConfig.GetWorkspacePath();
            Directory.CreateDirectory(workspace);

            // Create tool registry
            var tools = new ToolRegistry(workspace);
            Console.WriteLine("  {0}[tools]{1} Registered {2} filesystem tool(s)",
                Dim, Reset, tools.GetToolDefinitions().Count);

            // Create initial session
            Session session;
            AgentLogger logger;
            AgentRunner runner;
            CreateSession(tools, out session, out logger, out runner);

            while (true)
            {
                Console.Write("{0}You:{1} ", Cyan, Reset);
                string raw = Console.ReadLine();

                // EOF (Ctrl+D / piped input ended)
                if (raw == null)
                    break;

                string trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (trimmed == "/quit" || trimmed == "/exit")
                    break;

                if (trimmed == "/clear")
                {
                    logger.Info("cli", "Session cleared");
                    CreateSession(tools, out session, out logger, out runner);
                    Console.WriteLine();
                    continue;
                }

                bool isDemo = trimmed == "/demo";
                string message = isDemo ? AgentConfig.DemoTask : trimmed;

                try
                {
                    string response = await runner.RunAsync(session, message);
                    Console.WriteLine();
                    Console.WriteLine("{0}Agent:{1} {2}", Green, Reset, response);
                    Console.WriteLine();

                    if (isDemo)
                    {
                        Console.WriteLine("  {0}To view the Snake game, open workspace/snake/index.html in a browser.{1}", Dim, Reset);
                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("cli", ex, "Request failed");
                    Console.WriteLine();
                    Console.WriteLine("{0}Error: {1}{2}", Red, ex.Message, Reset);
                    Console.WriteLine();
                }
            }
        }

        private static void CreateSession(
            ToolRegistry tools,
            out Session session,
            out AgentLogger logger,
            out AgentRunner runner)
        {
            string id = Guid.NewGuid().ToString();
            session = new Session(id);
            logger = new AgentLogger(id);
            runner = new AgentRunner(tools, logger);

            logger.Info("cli", string.Format("Started session {0}", id.Substring(0, 8)));
        }
    }
}
