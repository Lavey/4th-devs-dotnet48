using System;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.AxClassifier.Cli;
using FourthDevs.AxClassifier.Core;
using FourthDevs.AxClassifier.Data;
using FourthDevs.Common;

namespace FourthDevs.AxClassifier
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0 &&
                    string.Equals(args[0], "optimize", StringComparison.OrdinalIgnoreCase))
                {
                    RunOptimize().GetAwaiter().GetResult();
                }
                else
                {
                    RunClassify().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(string.Format("Fatal: {0}", ex.Message));
                Environment.Exit(1);
            }
        }

        private static async Task RunClassify()
        {
            var emails = EmailData.Emails;
            ConsoleLogger.LogStart(emails.Count);

            using (var client = new ResponsesApiClient())
            {
                var classifier = new Classifier(client);

                // Try optimized demos first, fall back to hand-picked examples
                var demos = DemoStore.Load();
                if (demos != null)
                {
                    ConsoleLogger.LogDemosSource(true);
                    classifier.SetExamples(demos);
                }
                else
                {
                    ConsoleLogger.LogDemosSource(false);
                    classifier.SetExamples(FallbackExamples.Examples);
                }

                foreach (var email in emails)
                {
                    var result = await classifier.ClassifyAsync(email);
                    ConsoleLogger.LogEmail(email, result);
                }
            }

            ConsoleLogger.LogDone();
        }

        private static async Task RunOptimize()
        {
            using (var client = new ResponsesApiClient())
            {
                var optimizer = new Optimizer();
                await optimizer.RunAsync(
                    client,
                    TrainingData.TrainingSet,
                    TrainingData.ValidationSet);
            }
        }
    }
}
