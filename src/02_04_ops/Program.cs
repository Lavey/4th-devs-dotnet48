using System;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Ops.Agent;

namespace FourthDevs.Ops
{
    /// <summary>
    /// Lesson 09 – Daily Ops (02_04_ops)
    ///
    /// Multi-agent daily operations generator.
    /// An orchestrator agent reads a workflow, delegates data-gathering to
    /// specialist sub-agents (mail, calendar, tasks, notes), then synthesises
    /// the results into a daily ops Markdown note saved to workspace/output/.
    ///
    /// Source: 02_04_ops/src/index.ts (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private static readonly string DemoFile = Path.Combine("demo", "example.md");

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine($"  Daily Ops Generator — {today}");
            Console.WriteLine("========================================");
            Console.WriteLine();

            if (!ConfirmRun())
                return;

            string task = string.Join(" ", new[]
            {
                $"Prepare the Daily Ops note for {today}.",
                "Start by reading the workflow instructions from workflows/daily-ops.md using the read_file tool.",
                "Then follow the steps described in the workflow precisely.",
                $"Make sure to write the final output to output/{today}.md"
            });

            string result = await AgentRunner.RunAgentAsync("orchestrator", task);

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("  Result");
            Console.WriteLine("========================================");
            Console.WriteLine();
            Console.WriteLine(result);
        }

        static bool ConfirmRun()
        {
            Console.WriteLine("⚠️  UWAGA: Uruchomienie tego agenta może zużyć zauważalną liczbę tokenów.");
            Console.WriteLine("   Jeśli nie chcesz uruchamiać go teraz, najpierw sprawdź plik demo:");
            Console.WriteLine($"   Demo: {DemoFile}");
            Console.WriteLine();
            Console.Write("Czy chcesz kontynuować? (yes/y): ");
            string answer = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
            if (answer != "yes" && answer != "y")
            {
                Console.WriteLine("Przerwano.");
                return false;
            }
            return true;
        }
    }
}
