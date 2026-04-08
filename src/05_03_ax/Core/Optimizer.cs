using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.AxClassifier.Cli;
using FourthDevs.AxClassifier.Models;
using FourthDevs.Common;

namespace FourthDevs.AxClassifier.Core
{
    /// <summary>
    /// BootstrapFewShot-style optimizer.
    /// Runs the classifier on training examples, scores each prediction,
    /// collects successful traces as demonstrations, then validates on a held-out set.
    /// </summary>
    public sealed class Optimizer
    {
        private const int MaxRounds = 4;
        private const int MaxDemos = 4;
        private const int MaxExamples = 8;
        private const double TargetScore = 0.85;

        public async Task RunAsync(
            ResponsesApiClient client,
            List<LabeledEmail> trainingSet,
            List<LabeledEmail> validationSet)
        {
            ConsoleLogger.LogTrainingStart(trainingSet.Count);

            var allDemos = new List<LabeledEmail>();
            double bestScore = 0;
            int totalCalls = 0;
            int successfulDemos = 0;
            bool earlyStopped = false;

            for (int round = 0; round < MaxRounds; round++)
            {
                Console.WriteLine(string.Format(
                    "\n--- Round {0}/{1} ---", round + 1, MaxRounds));

                var classifier = new Classifier(client);

                // Use current best demos + up to MaxExamples from previous rounds
                if (allDemos.Count > 0)
                {
                    var examplesForRound = new List<LabeledEmail>();
                    int count = Math.Min(allDemos.Count, MaxExamples);
                    for (int i = 0; i < count; i++)
                        examplesForRound.Add(allDemos[i]);
                    classifier.SetExamples(examplesForRound);
                }

                double roundScore = 0;
                var roundDemos = new List<LabeledEmail>();

                foreach (var example in trainingSet)
                {
                    totalCalls++;
                    try
                    {
                        var prediction = await classifier.ClassifyRawAsync(
                            example.EmailFrom, example.EmailSubject, example.EmailBody);
                        double score = Metric.Score(prediction, example);
                        roundScore += score;

                        Console.WriteLine(string.Format(
                            "  {0} {1:F2} | {2}",
                            score >= 0.8 ? "\u2705" : score >= 0.5 ? "\uD83D\uDFE1" : "\u274C",
                            score,
                            Truncate(example.EmailSubject, 50)));

                        if (score >= 0.8 && roundDemos.Count < MaxDemos)
                        {
                            successfulDemos++;
                            roundDemos.Add(new LabeledEmail
                            {
                                EmailFrom = example.EmailFrom,
                                EmailSubject = example.EmailSubject,
                                EmailBody = example.EmailBody,
                                Labels = prediction.Labels.ToArray(),
                                Priority = prediction.Priority,
                                NeedsReply = prediction.NeedsReply,
                                Summary = prediction.Summary
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format(
                            "  \u274C Error: {0}", ex.Message));
                    }
                }

                double avgScore = roundScore / trainingSet.Count;
                Console.WriteLine(string.Format(
                    "  Round avg score: {0:F3}", avgScore));

                // Keep best demos
                if (avgScore > bestScore)
                {
                    bestScore = avgScore;
                    if (roundDemos.Count > 0)
                        allDemos = roundDemos;
                }

                if (bestScore >= TargetScore)
                {
                    earlyStopped = true;
                    Console.WriteLine(string.Format(
                        "  Target score {0:F2} reached, stopping early.", TargetScore));
                    break;
                }
            }

            // Save demos
            if (allDemos.Count > 0)
                DemoStore.Save(allDemos);

            ConsoleLogger.LogOptimizationResult(
                bestScore, totalCalls, successfulDemos, earlyStopped, allDemos.Count);

            // Validation
            ConsoleLogger.LogValidationHeader();

            var valClassifier = new Classifier(client);
            if (allDemos.Count > 0)
                valClassifier.SetExamples(allDemos);

            double totalValScore = 0;
            foreach (var example in validationSet)
            {
                try
                {
                    var prediction = await valClassifier.ClassifyRawAsync(
                        example.EmailFrom, example.EmailSubject, example.EmailBody);
                    double score = Metric.Score(prediction, example);
                    totalValScore += score;

                    ConsoleLogger.LogValidationRow(
                        score,
                        example.EmailSubject,
                        example.Labels,
                        prediction.Labels.ToArray());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("  Error: {0}", ex.Message));
                }
            }

            ConsoleLogger.LogValidationAvg(totalValScore / validationSet.Count);
        }

        private static string Truncate(string s, int maxLen)
        {
            if (s == null) return "";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
        }
    }
}
