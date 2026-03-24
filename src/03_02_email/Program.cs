using System;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Email.Agent;

namespace FourthDevs.Email
{
    /// <summary>
    /// 03_02_email — Two-phase email agent
    ///
    /// Phase 1: Triage — read, classify, label emails, mark those needing replies
    /// Phase 2: Draft Sessions — isolated AI completion with scoped KB per reply
    ///
    /// Source: 03_02_email (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string DefaultModel = "gpt-4.1";

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            string model = AiConfig.ResolveModel(DefaultModel);

            string task = args.Length > 0
                ? string.Join(" ", args)
                : "Triage both inboxes: read all unread emails, check the knowledge base for context, assign labels, and mark emails that need replies.";

            try
            {
                var result = await AgentRunner.RunAsync(model, task).ConfigureAwait(false);

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine();
                Console.WriteLine("  ╔══════════════════════════════════════════════════════╗");
                Console.WriteLine("  ║                     Result                           ║");
                Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine($"  {result.Response}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Fatal: {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 1;
            }
        }
    }
}
