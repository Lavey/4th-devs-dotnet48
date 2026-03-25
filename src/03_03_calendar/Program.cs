// Lesson 13 – Calendar Agent (03_03_calendar)
// Source: 03_03_calendar (i-am-alice/4th-devs)
// Two phases: Add Events + Notification Webhooks

using System;
using System.Threading.Tasks;
using FourthDevs.Calendar.Agent;
using FourthDevs.Common;

namespace FourthDevs.Calendar
{
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

            Console.WriteLine();
            Console.WriteLine("  03_03_calendar — Calendar Agent");
            Console.WriteLine(string.Format("  Model: {0}", model));
            Console.WriteLine();
            Console.Write("  Czy chcesz kontynuować? (t/N): ");
            string answer = Console.ReadLine();
            if (!string.Equals(answer, "t", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(answer, "T", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  Anulowano.");
                return;
            }

            try
            {
                var result = await AgentRunner.RunAsync(model).ConfigureAwait(false);

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine();
                Console.WriteLine("  ╔════════════════════════════════════════════════════════╗");
                Console.WriteLine("  ║                       Result                           ║");
                Console.WriteLine("  ╚════════════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine(string.Format("  Events created:        {0}", result.EventsCreated));
                Console.WriteLine(string.Format("  Notifications sent:    {0}", result.NotificationsSent));
                Console.WriteLine(string.Format("  Add phase steps:       {0}", result.AddPhase.Count));
                Console.WriteLine(string.Format("  Notification webhooks: {0}", result.NotificationPhase.Count));
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(string.Format("Fatal: {0}", ex.Message));
                Console.ResetColor();
                Environment.ExitCode = 1;
            }
        }
    }
}
