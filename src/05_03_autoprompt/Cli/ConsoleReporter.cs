using System;
using System.Collections.Generic;
using System.Linq;
using FourthDevs.AutoPrompt.Models;

namespace FourthDevs.AutoPrompt.Cli
{
    public class ConsoleReporter
    {
        private static string Dim(string value) { return "\x1b[2m" + value + "\x1b[0m"; }
        private static string Green(string value) { return "\x1b[32m" + value + "\x1b[0m"; }
        private static string Red(string value) { return "\x1b[31m" + value + "\x1b[0m"; }
        private static string Yellow(string value) { return "\x1b[33m" + value + "\x1b[0m"; }
        private static string Bold(string value) { return "\x1b[1m" + value + "\x1b[0m"; }
        private static string Cyan(string value) { return "\x1b[36m" + value + "\x1b[0m"; }

        private static string FormatModelProfile(Config.ModelProfile profile)
        {
            string effort = (profile.Reasoning != null && !string.IsNullOrEmpty(profile.Reasoning.Effort))
                ? profile.Reasoning.Effort
                : "default";
            return string.Format("{0} reasoning:{1}", profile.Model, effort);
        }

        private static string Bar(double value, double max = 1, int width = 30)
        {
            int filled = (int)Math.Round(value / max * width);
            return Green(new string('#', filled)) + Dim(new string('.', width - filled));
        }

        private static void PrintCaseResult(CaseResult result)
        {
            string icon;
            if (result.Score >= 0.8) icon = Green("OK");
            else if (result.Score >= 0.5) icon = Yellow("~");
            else icon = Red("X");

            string sections = "";
            if (result.Breakdown != null)
            {
                var parts = new List<string>();
                foreach (var kvp in result.Breakdown)
                {
                    parts.Add(string.Format("{0}:{1:F2}", kvp.Key.Substring(0, 1), kvp.Value.Score));
                }
                sections = string.Join(" ", parts);
            }

            Console.WriteLine(Dim(string.Format("    {0} case {1}: {2:F4} {3}",
                icon, result.Id, result.Score, sections)));

            if (result.Breakdown == null) return;

            foreach (var kvp in result.Breakdown)
            {
                if (kvp.Value.Issues == null) continue;
                foreach (var issue in kvp.Value.Issues)
                {
                    Console.WriteLine(Dim(string.Format("      - {0}: {1}", kvp.Key, issue)));
                }
            }
        }

        public void OnStart(LoadedProject project, int maxIterations, int evalRuns, int candidateCount)
        {
            Console.WriteLine(Bold("\nautoprompt"));
            Console.WriteLine(Dim("  project: " + project.Name));
            Console.WriteLine(Dim("  tests: " + project.TestCases.Count));
            Console.WriteLine(Dim("  prompt: " + project.PromptPath));
            Console.WriteLine(Dim("  execution: " + FormatModelProfile(project.Models.Execution)));
            Console.WriteLine(Dim("  judge: " + FormatModelProfile(project.Models.Judge)));
            Console.WriteLine(Dim("  improver: " + FormatModelProfile(project.Models.Improver)));
            Console.WriteLine(Dim("  iterations: " + maxIterations));
            Console.WriteLine(Dim("  eval runs: " + evalRuns));
            Console.WriteLine(Dim("  candidates: " + candidateCount + "\n"));
        }

        public void OnBaseline(EvalResult baseline)
        {
            Console.WriteLine(Dim(new string('-', 60)));
            Console.WriteLine(Bold("  baseline"));
            Console.WriteLine(string.Format("  {0} {1} {2}",
                Bar(baseline.Avg),
                Bold(baseline.Avg.ToString("F4")),
                Dim("+/-" + baseline.Spread.ToString("F4"))));
            foreach (var result in baseline.Results)
            {
                PrintCaseResult(result);
            }
        }

        public void OnIterationStart(int iterationNumber, int maxIterations,
            List<HistoryEntry> history, int candidateCount)
        {
            Console.WriteLine(Dim("\n" + new string('-', 60)));
            Console.WriteLine(Bold(string.Format("  iteration {0}/{1}", iterationNumber, maxIterations)));

            if (history.Count >= 3 &&
                history.Skip(history.Count - 3).All(e => e.Status == "discard"))
            {
                Console.WriteLine(Yellow("  strategy change recommended"));
            }
            else
            {
                Console.WriteLine(Dim(string.Format("  generating {0} candidates...", candidateCount)));
            }
        }

        public void OnCandidateSuggestions(int iterationNumber, List<ImproverSuggestion> suggestions)
        {
            foreach (var suggestion in suggestions)
            {
                string firstLine = suggestion.Reasoning.Split('\n')[0];
                Console.WriteLine(
                    Cyan(string.Format("  [{0}/{1}]", suggestion.CandidateLabel, suggestion.Operation))
                    + " " + Dim(firstLine));
            }
            Console.WriteLine(Dim("  evaluating candidates..."));
        }

        public void OnImproverError(IterationResult iteration, Exception error)
        {
            Console.WriteLine(Red("  improver failed: " + error.Message));
        }

        public void OnIterationEvaluated(IterationResult iteration)
        {
            if (iteration.Candidates != null && iteration.Candidates.Count > 0)
            {
                var parts = new List<string>();
                foreach (var candidate in iteration.Candidates)
                {
                    string marker = candidate.Status == "keep" ? "*" : "";
                    parts.Add(string.Format("{0}:{1:F4}{2}",
                        candidate.CandidateLabel, candidate.Score, marker));
                }
                Console.WriteLine(Dim("  candidates: " + string.Join("  ", parts)));
            }

            string deltaStr = iteration.Delta >= 0
                ? Green("+" + iteration.Delta.ToString("F4"))
                : Red(iteration.Delta.ToString("F4"));

            Console.WriteLine(string.Format("  {0} {1} {2} {3}",
                Bar(iteration.Score),
                Bold(iteration.Score.ToString("F4")),
                deltaStr,
                Dim(string.Format("+/-{0:F4} floor:{1:F4}",
                    iteration.Result.Spread, iteration.NoiseFloor))));

            if (iteration.Delta > 0 && iteration.Status != "keep")
            {
                Console.WriteLine(Yellow("  improvement within noise range - treating as no change"));
            }

            foreach (var result in iteration.Result.Results)
            {
                PrintCaseResult(result);
            }

            if (!string.IsNullOrEmpty(iteration.SectionDeltaSummary))
            {
                Console.WriteLine(Dim("  section deltas: " + iteration.SectionDeltaSummary));
            }

            Console.WriteLine(Dim("  diff:"));
            var lines = iteration.Diff.Split('\n');
            int limit = Math.Min(lines.Length, 12);
            for (int i = 0; i < limit; i++)
            {
                string line = lines[i];
                string colored;
                if (line.StartsWith("+")) colored = Green(line);
                else if (line.StartsWith("-")) colored = Red(line);
                else colored = Dim(line);
                Console.WriteLine("    " + colored);
            }
            if (lines.Length > 12)
            {
                Console.WriteLine(Dim(string.Format("    ... {0} more lines", lines.Length - 12)));
            }

            Console.WriteLine(iteration.Status == "keep"
                ? Green("  -> keep")
                : Yellow("  -> discard"));
        }

        public void OnComplete(OptimizeRun run)
        {
            Console.WriteLine(Dim("\n" + new string('=', 60)));
            Console.WriteLine(Bold("  done\n"));
            Console.WriteLine(string.Format("  baseline: {0:F4}", run.Baseline.Avg));

            string change = run.BestScore > run.Baseline.Avg
                ? Green("+" + (run.BestScore - run.Baseline.Avg).ToString("F4"))
                : Dim("no change");

            Console.WriteLine(string.Format("  final:    {0} {1}",
                Bold(run.BestScore.ToString("F4")), change));
        }

        public void PrintVerifyResult(LoadedProject project, string promptPath, SingleRunResult result)
        {
            Console.WriteLine(Bold("\nautoprompt - verify"));
            Console.WriteLine(Dim("  project: " + project.Name));
            Console.WriteLine(Dim("  tests: " + project.VerifyCases.Count));
            Console.WriteLine(Dim("  execution: " + FormatModelProfile(project.Models.Execution)));
            Console.WriteLine(Dim("  judge: " + FormatModelProfile(project.Models.Judge)));
            Console.WriteLine(Dim("  prompt: " + promptPath + "\n"));
            Console.WriteLine(Dim(new string('-', 60)));
            Console.WriteLine(string.Format("  {0} {1}\n", Bar(result.Avg), Bold(result.Avg.ToString("F4"))));

            foreach (var caseResult in result.Results)
            {
                PrintCaseResult(caseResult);
            }

            Console.WriteLine(Dim("\n" + new string('=', 60)));
            Console.WriteLine(Bold(string.Format("  score: {0:F4}\n", result.Avg)));
        }
    }
}
