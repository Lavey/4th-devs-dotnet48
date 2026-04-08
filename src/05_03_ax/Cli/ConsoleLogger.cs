using System;
using System.Collections.Generic;
using FourthDevs.AxClassifier.Models;

namespace FourthDevs.AxClassifier.Cli
{
    public static class ConsoleLogger
    {
        private const string Reset = "\x1b[0m";
        private const string Bold = "\x1b[1m";
        private const string Dim = "\x1b[2m";

        private static readonly string Sep =
            Dim + "\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500" +
            "\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500" +
            "\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500" +
            "\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500" + Reset;

        private static readonly Dictionary<string, string> LabelColors =
            new Dictionary<string, string>
            {
                { "urgent",      "\x1b[31m" },
                { "client",      "\x1b[34m" },
                { "internal",    "\x1b[32m" },
                { "newsletter",  "\x1b[36m" },
                { "billing",     "\x1b[33m" },
                { "github",      "\x1b[35m" },
                { "security",    "\x1b[31m" },
                { "spam",        "\x1b[90m" },
                { "automated",   "\x1b[90m" },
                { "needs-reply", "\x1b[33m" },
            };

        private static string ColorLabel(string label)
        {
            string color;
            if (!LabelColors.TryGetValue(label, out color))
                color = "";
            return color + label + Reset;
        }

        private static string PriorityIcon(string p)
        {
            if (p == "high") return "\uD83D\uDD34";
            if (p == "medium") return "\uD83D\uDFE1";
            return "\uD83D\uDFE2";
        }

        private static string ScoreIcon(double score)
        {
            if (score >= 0.8) return "\u2705";
            if (score >= 0.5) return "\uD83D\uDFE1";
            return "\u274C";
        }

        public static void LogStart(int count)
        {
            Console.WriteLine(string.Format(
                "{0}Classifying {1} emails...{2}\n", Bold, count, Reset));
        }

        public static void LogEmail(Email email, ClassificationResult result)
        {
            Console.WriteLine(Sep);
            Console.WriteLine(string.Format("{0}{1}{2}", Bold, email.Subject, Reset));
            Console.WriteLine(string.Format("  From:     {0}", email.From));
            Console.WriteLine(string.Format("  Priority: {0} {1}",
                PriorityIcon(result.Priority), result.Priority));

            var coloredLabels = new List<string>();
            foreach (var l in result.Labels) coloredLabels.Add(ColorLabel(l));
            Console.WriteLine(string.Format("  Labels:   {0}",
                string.Join(", ", coloredLabels)));

            Console.WriteLine(string.Format("  Reply:    {0}",
                result.NeedsReply ? "yes" : "no"));
            Console.WriteLine(string.Format("  Summary:  {0}", result.Summary));
        }

        public static void LogDone()
        {
            Console.WriteLine();
            Console.WriteLine(Sep);
            Console.WriteLine(string.Format("{0}Done.{1}", Bold, Reset));
        }

        public static void LogDemosSource(bool optimized)
        {
            Console.WriteLine(string.Format("{0}Using {1}{2}",
                Dim,
                optimized
                    ? "optimized demos from demos.json"
                    : "fallback examples",
                Reset));
        }

        public static void LogTrainingStart(int count)
        {
            Console.WriteLine(string.Format(
                "{0}Training on {1} examples...{2}\n", Bold, count, Reset));
        }

        public static void LogOptimizationResult(
            double bestScore, int totalCalls, int successfulDemos,
            bool earlyStopped, int demosCount)
        {
            Console.WriteLine(string.Format(
                "\n{0}=== Optimization Result ==={1}", Bold, Reset));
            Console.WriteLine(string.Format("Best score:       {0:F3}", bestScore));
            Console.WriteLine(string.Format("Total calls:      {0}", totalCalls));
            Console.WriteLine(string.Format("Successful demos: {0}", successfulDemos));
            Console.WriteLine(string.Format("Early stopped:    {0}", earlyStopped));
            if (demosCount > 0)
            {
                Console.WriteLine(string.Format(
                    "\nDemos saved to demos.json ({0} entries)", demosCount));
            }
        }

        public static void LogValidationHeader()
        {
            Console.WriteLine(string.Format(
                "\n{0}=== Validation ==={1}", Bold, Reset));
        }

        public static void LogValidationRow(
            double score, string subject, string[] expected, string[] got)
        {
            string subjectTruncated = subject.Length > 50
                ? subject.Substring(0, 50)
                : subject;
            Console.WriteLine(string.Format("{0} {1:F2} | {2}",
                ScoreIcon(score), score, subjectTruncated));
            Console.WriteLine(string.Format(
                "         expected: [{0}]  got: [{1}]",
                string.Join(", ", expected),
                string.Join(", ", got)));
        }

        public static void LogValidationAvg(double avg)
        {
            Console.WriteLine(string.Format(
                "\nAverage validation score: {0:F3}", avg));
        }
    }
}
