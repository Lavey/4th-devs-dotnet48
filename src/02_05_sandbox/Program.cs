using System;
using System.Threading.Tasks;
using FourthDevs.Sandbox.Agent;
using FourthDevs.Sandbox.Mcp;

namespace FourthDevs.Sandbox
{
    /// <summary>
    /// Lesson 10 – MCP Sandbox Agent (02_05_sandbox)
    ///
    /// An agent that discovers MCP server tools dynamically, loads their
    /// TypeScript schemas on demand, and executes JavaScript code in an
    /// isolated Jint sandbox to accomplish tasks.
    ///
    /// Default task: create a shopping list, mark an item complete, show what's left.
    ///
    /// Source: 02_05_sandbox/src/index.ts (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string DefaultTask =
            "Create a shopping list with: milk, bread, eggs. " +
            "Then mark milk as completed and show me what's left to buy.";

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("  MCP Sandbox Agent");
            Console.WriteLine("========================================");
            Console.WriteLine();

            // Reset any loaded tool schemas for a clean session
            McpRegistry.ResetSession();
            TodoStore.Reset();

            string task = args.Length > 0
                ? string.Join(" ", args)
                : DefaultTask;

            Console.WriteLine($"Task: {task}");
            Console.WriteLine();

            string result = await AgentRunner.RunAgentAsync("sandbox", task);

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("  Result");
            Console.WriteLine("========================================");
            Console.WriteLine();
            Console.WriteLine(result);
        }
    }
}
