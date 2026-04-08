using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.AutoPrompt.Config;
using FourthDevs.AutoPrompt.Llm;
using FourthDevs.AutoPrompt.Models;

namespace FourthDevs.AutoPrompt.Core
{
    public class OptimizeProject
    {
        private static readonly CandidateStrategy[] CANDIDATE_STRATEGIES = new[]
        {
            new CandidateStrategy
            {
                Label = "balanced",
                Hint = "Balanced attempt: choose the highest-impact single change without any special bias."
            },
            new CandidateStrategy
            {
                Label = "coverage",
                Hint = "Coverage attempt: prefer ADD or REWORD when a scored schema/evaluation concern lacks a clear rule."
            },
            new CandidateStrategy
            {
                Label = "simplify",
                Hint = "Simplification attempt: prefer REMOVE or MERGE when rules overlap or can be compressed without losing coverage."
            },
            new CandidateStrategy
            {
                Label = "boundary",
                Hint = "Boundary attempt: prefer a rule that sharpens what counts as a task, decision, person, or project."
            },
            new CandidateStrategy
            {
                Label = "salience",
                Hint = "Salience attempt: prefer REORDER or REWORD to make the most important rule easier for the model to follow."
            }
        };

        private readonly LlmClient _llm;
        private readonly ImprovePrompt _improver;
        private readonly RunEvaluation _evaluator;

        public OptimizeProject(LlmClient llm)
        {
            _llm = llm;
            _improver = new ImprovePrompt(llm);
            _evaluator = new RunEvaluation(llm);
        }

        private static List<CandidateStrategy> BuildCandidateStrategies(int count)
        {
            var strategies = new List<CandidateStrategy>();
            for (int i = 0; i < count; i++)
            {
                var baseStrategy = CANDIDATE_STRATEGIES[i % CANDIDATE_STRATEGIES.Length];
                if (i < CANDIDATE_STRATEGIES.Length)
                {
                    strategies.Add(baseStrategy);
                }
                else
                {
                    strategies.Add(new CandidateStrategy
                    {
                        Label = string.Format("{0}-{1}", baseStrategy.Label, i + 1),
                        Hint = baseStrategy.Hint
                    });
                }
            }
            return strategies;
        }

        private static Dictionary<string, double> AverageSectionScores(EvalResult evalResult)
        {
            var totals = new Dictionary<string, double>();
            var counts = new Dictionary<string, int>();

            foreach (var result in evalResult.Results)
            {
                if (result.Breakdown == null) continue;

                foreach (var kvp in result.Breakdown)
                {
                    if (!totals.ContainsKey(kvp.Key))
                    {
                        totals[kvp.Key] = 0;
                        counts[kvp.Key] = 0;
                    }
                    totals[kvp.Key] += kvp.Value.Score;
                    counts[kvp.Key] += 1;
                }
            }

            var result2 = new Dictionary<string, double>();
            foreach (var key in totals.Keys)
            {
                result2[key] = Math.Round(totals[key] / counts[key] * 10000) / 10000;
            }
            return result2;
        }

        private static Dictionary<string, double> ComputeSectionDeltas(
            Dictionary<string, double> sectionScores,
            Dictionary<string, double> referenceSectionScores)
        {
            var keys = new HashSet<string>();
            if (sectionScores != null)
            {
                foreach (var k in sectionScores.Keys) keys.Add(k);
            }
            if (referenceSectionScores != null)
            {
                foreach (var k in referenceSectionScores.Keys) keys.Add(k);
            }

            var result = new Dictionary<string, double>();
            foreach (var key in keys)
            {
                double current = 0;
                double reference = 0;
                if (sectionScores != null && sectionScores.ContainsKey(key))
                    current = sectionScores[key];
                if (referenceSectionScores != null && referenceSectionScores.ContainsKey(key))
                    reference = referenceSectionScores[key];
                result[key] = Math.Round((current - reference) * 10000) / 10000;
            }
            return result;
        }

        private static string FormatSectionSummary(Dictionary<string, double> sectionScores)
        {
            var parts = new List<string>();
            foreach (var kvp in sectionScores)
            {
                parts.Add(string.Format("{0}:{1:F2}", kvp.Key, kvp.Value));
            }
            return string.Join(" ", parts);
        }

        private static string FormatSectionDeltaSummary(Dictionary<string, double> sectionDeltas)
        {
            var filtered = sectionDeltas
                .Where(kvp => Math.Abs(kvp.Value) > 0.0001)
                .OrderByDescending(kvp => Math.Abs(kvp.Value))
                .ToList();

            var parts = new List<string>();
            foreach (var kvp in filtered)
            {
                string sign = kvp.Value >= 0 ? "+" : "";
                parts.Add(string.Format("{0}:{1}{2:F2}", kvp.Key, sign, kvp.Value));
            }
            return string.Join(" ", parts);
        }

        private static HistoryEntry ToHistoryEntry(IterationResult iteration)
        {
            string reasoning = iteration.Reasoning ?? "";
            reasoning = reasoning.Replace("\n", " ");
            if (reasoning.Length > 200) reasoning = reasoning.Substring(0, 200);

            string diff = iteration.Diff ?? "";
            var diffLines = diff.Split(new[] { '\n' }, StringSplitOptions.None);
            string diffPreview = string.Join(" | ",
                diffLines.Length > 3
                    ? diffLines.Take(3).ToArray()
                    : diffLines);

            return new HistoryEntry
            {
                Status = iteration.Status,
                Score = iteration.Score.ToString("F4"),
                Operation = iteration.Operation,
                Reasoning = reasoning,
                CandidateLabel = iteration.CandidateLabel,
                SectionSummary = iteration.SectionSummary,
                SectionDeltaSummary = iteration.SectionDeltaSummary,
                Diff = diffPreview
            };
        }

        private static int CompareCandidateIterations(IterationResult left, IterationResult right)
        {
            if (left.Status == "keep" && right.Status != "keep") return -1;
            if (right.Status == "keep" && left.Status != "keep") return 1;
            if (right.Score != left.Score) return right.Score.CompareTo(left.Score);
            if (right.Delta != left.Delta) return right.Delta.CompareTo(left.Delta);
            int leftLen = left.Prompt != null ? left.Prompt.Length : 0;
            int rightLen = right.Prompt != null ? right.Prompt.Length : 0;
            return leftLen.CompareTo(rightLen);
        }

        public async Task<OptimizeRun> RunAsync(
            LoadedProject project,
            int maxIterations = Defaults.MAX_ITERATIONS,
            int evalRuns = Defaults.EVAL_RUNS,
            Cli.ConsoleReporter reporter = null)
        {
            int candidateCount = (project.Optimization != null && project.Optimization.Candidates.HasValue)
                ? project.Optimization.Candidates.Value
                : Defaults.CANDIDATE_COUNT;

            if (reporter != null)
                reporter.OnStart(project, maxIterations, evalRuns, candidateCount);

            string currentPrompt = project.InitialPrompt;
            string bestPrompt = currentPrompt;
            double bestScore = 0;

            var baseline = await _evaluator.RunAsync(
                currentPrompt,
                project.TestCases,
                project.ExtractionSchema,
                project.Evaluation,
                project.Models,
                evalRuns);

            bestScore = baseline.Avg;
            if (reporter != null) reporter.OnBaseline(baseline);

            var lastEval = baseline;
            var bestSectionScores = AverageSectionScores(baseline);
            var iterations = new List<IterationResult>();

            for (int iterationNumber = 1; iterationNumber <= maxIterations; iterationNumber++)
            {
                var history = iterations
                    .Skip(Math.Max(0, iterations.Count - 5))
                    .Select(ToHistoryEntry)
                    .ToList();

                var candidateStrategies = BuildCandidateStrategies(candidateCount);
                if (reporter != null)
                    reporter.OnIterationStart(iterationNumber, maxIterations, history, candidateCount);

                // Generate suggestions
                List<ImproverSuggestion> suggestions;
                try
                {
                    suggestions = new List<ImproverSuggestion>();
                    foreach (var strategy in candidateStrategies)
                    {
                        try
                        {
                            var suggestion = await _improver.SuggestImprovementAsync(
                                currentPrompt,
                                lastEval,
                                project.TestCases,
                                history,
                                project.ExtractionSchema,
                                project.Evaluation,
                                project.Models,
                                strategy.Hint);

                            suggestion.CandidateLabel = strategy.Label;
                            suggestion.CandidateHint = strategy.Hint;
                            suggestions.Add(suggestion);
                        }
                        catch
                        {
                            // Skip failed candidate generation
                        }
                    }

                    if (suggestions.Count == 0)
                    {
                        throw new Exception("all candidate generations failed");
                    }
                }
                catch (Exception error)
                {
                    var crashIteration = new IterationResult
                    {
                        Iteration = iterationNumber,
                        Score = 0,
                        Status = "crash",
                        Operation = "ERROR",
                        Reasoning = error.Message,
                        CandidateLabel = "improver",
                        SectionSummary = "",
                        SectionDeltaSummary = "",
                        Diff = "(improver failed before generating a prompt)",
                        DiffSummary = "(none)",
                        Result = null,
                        Candidates = new List<CandidateSummary>()
                    };
                    iterations.Add(crashIteration);
                    if (reporter != null) reporter.OnImproverError(crashIteration, error);
                    continue;
                }

                if (reporter != null) reporter.OnCandidateSuggestions(iterationNumber, suggestions);

                // Evaluate candidates
                var candidateIterations = new List<IterationResult>();
                foreach (var suggestion in suggestions)
                {
                    try
                    {
                        var candidateResult = await _evaluator.RunAsync(
                            suggestion.Prompt,
                            project.TestCases,
                            project.ExtractionSchema,
                            project.Evaluation,
                            project.Models,
                            evalRuns);

                        double delta = candidateResult.Avg - bestScore;
                        double noiseFloor = Math.Max(candidateResult.Spread, lastEval.Spread) / 2;
                        bool improved = delta > noiseFloor;
                        string diff = PromptDiff.ComputeDiff(currentPrompt, suggestion.Prompt);
                        var sectionScores = AverageSectionScores(candidateResult);
                        var sectionDeltas = ComputeSectionDeltas(sectionScores, bestSectionScores);

                        candidateIterations.Add(new IterationResult
                        {
                            Iteration = iterationNumber,
                            Score = candidateResult.Avg,
                            Status = improved ? "keep" : "discard",
                            Operation = suggestion.Operation,
                            Reasoning = suggestion.Reasoning,
                            CandidateLabel = suggestion.CandidateLabel,
                            Prompt = suggestion.Prompt,
                            Diff = diff,
                            DiffSummary = PromptDiff.SummarizeDiff(currentPrompt, suggestion.Prompt),
                            Result = candidateResult,
                            Delta = delta,
                            NoiseFloor = noiseFloor,
                            SectionScores = sectionScores,
                            SectionDeltas = sectionDeltas,
                            SectionSummary = FormatSectionSummary(sectionScores),
                            SectionDeltaSummary = FormatSectionDeltaSummary(sectionDeltas)
                        });
                    }
                    catch
                    {
                        // Skip failed candidate evaluation
                    }
                }

                if (candidateIterations.Count == 0)
                {
                    var crashIteration = new IterationResult
                    {
                        Iteration = iterationNumber,
                        Score = 0,
                        Status = "crash",
                        Operation = "ERROR",
                        Reasoning = "all candidate evaluations failed",
                        CandidateLabel = "evaluation",
                        SectionSummary = "",
                        SectionDeltaSummary = "",
                        Diff = "(evaluation failed before scoring any candidate)",
                        DiffSummary = "(none)",
                        Result = null,
                        Candidates = new List<CandidateSummary>()
                    };
                    iterations.Add(crashIteration);
                    if (reporter != null)
                        reporter.OnImproverError(crashIteration, new Exception(crashIteration.Reasoning));
                    continue;
                }

                candidateIterations.Sort(CompareCandidateIterations);
                var bestCandidate = candidateIterations[0];

                var iteration = new IterationResult
                {
                    Iteration = bestCandidate.Iteration,
                    Score = bestCandidate.Score,
                    Status = bestCandidate.Status,
                    Operation = bestCandidate.Operation,
                    Reasoning = bestCandidate.Reasoning,
                    CandidateLabel = bestCandidate.CandidateLabel,
                    Prompt = bestCandidate.Prompt,
                    Diff = bestCandidate.Diff,
                    DiffSummary = bestCandidate.DiffSummary,
                    Result = bestCandidate.Result,
                    Delta = bestCandidate.Delta,
                    NoiseFloor = bestCandidate.NoiseFloor,
                    SectionScores = bestCandidate.SectionScores,
                    SectionDeltas = bestCandidate.SectionDeltas,
                    SectionSummary = bestCandidate.SectionSummary,
                    SectionDeltaSummary = bestCandidate.SectionDeltaSummary,
                    Candidates = candidateIterations.Select(c => new CandidateSummary
                    {
                        CandidateLabel = c.CandidateLabel,
                        Operation = c.Operation,
                        Status = c.Status,
                        Score = c.Score,
                        Delta = c.Delta,
                        NoiseFloor = c.NoiseFloor,
                        DiffSummary = c.DiffSummary,
                        SectionSummary = c.SectionSummary,
                        SectionDeltaSummary = c.SectionDeltaSummary
                    }).ToList()
                };

                iterations.Add(iteration);
                if (reporter != null) reporter.OnIterationEvaluated(iteration);

                if (iteration.Status == "keep")
                {
                    currentPrompt = iteration.Prompt;
                    bestPrompt = iteration.Prompt;
                    bestScore = iteration.Score;
                    lastEval = iteration.Result;
                    bestSectionScores = iteration.SectionScores;
                }
            }

            var run = new OptimizeRun
            {
                ProjectName = project.Name,
                Baseline = baseline,
                BestPrompt = bestPrompt,
                BestScore = bestScore,
                Models = project.Models,
                Iterations = iterations,
                EvalRuns = evalRuns,
                MaxIterations = maxIterations
            };

            if (reporter != null) reporter.OnComplete(run);
            return run;
        }
    }
}
