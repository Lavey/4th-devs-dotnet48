using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourthDevs.AutoPrompt.Llm;
using FourthDevs.AutoPrompt.Models;
using FourthDevs.AutoPrompt.Prompts;
using Newtonsoft.Json;

namespace FourthDevs.AutoPrompt.Core
{
    public class ImprovePrompt
    {
        private readonly LlmClient _llm;

        public ImprovePrompt(LlmClient llm)
        {
            _llm = llm;
        }

        public static string DetectStuck(List<HistoryEntry> history)
        {
            if (history.Count < 3) return "";

            var recent = history.Skip(history.Count - 3).ToList();
            bool allDiscarded = recent.All(e => e.Status == "discard");
            bool sameOperation = recent.All(e => e.Operation == recent[0].Operation);

            if (allDiscarded && sameOperation)
            {
                var sb = new StringBuilder();
                sb.AppendLine("## STRATEGY CHANGE REQUIRED");
                sb.AppendLine();
                sb.AppendLine(string.Format(
                    "The last {0} attempts were all {1} and all discarded.",
                    recent.Count, recent[0].Operation));
                sb.AppendLine(string.Format("Forbidden this iteration: {0}", recent[0].Operation));
                sb.AppendLine();
                sb.AppendLine("Strong alternatives:");
                sb.AppendLine("- REMOVE: free attention by deleting a redundant rule.");
                sb.AppendLine("- MERGE: combine overlapping rules into one sharper rule.");
                sb.AppendLine("- REORDER: move the most important rule to the top or bottom.");
                sb.AppendLine();
                return sb.ToString();
            }

            if (allDiscarded)
            {
                var ops = string.Join(", ", recent.Select(e => e.Operation));
                var sb = new StringBuilder();
                sb.AppendLine("## Caution: 3 consecutive discards");
                sb.AppendLine();
                sb.AppendLine(string.Format("Recent operations: {0}", ops));
                sb.AppendLine("Consider a different error type or a simplification move like REMOVE or MERGE.");
                sb.AppendLine();
                return sb.ToString();
            }

            return "";
        }

        public static ImproverSuggestion ParseImproverResponse(string raw)
        {
            const string separator = "---PROMPT---";
            int separatorIndex = raw.IndexOf(separator, StringComparison.Ordinal);

            if (separatorIndex == -1)
            {
                return new ImproverSuggestion
                {
                    Reasoning = "(no reasoning provided)",
                    Operation = "UNKNOWN",
                    Prompt = raw.Trim()
                };
            }

            string meta = raw.Substring(0, separatorIndex);
            string prompt = raw.Substring(separatorIndex + separator.Length).Trim();

            var reasoningMatch = Regex.Match(meta, @"REASONING:\s*([\s\S]*?)(?=\nOPERATION:|\n---)", RegexOptions.IgnoreCase);
            var operationMatch = Regex.Match(meta, @"OPERATION:\s*(\w+)", RegexOptions.IgnoreCase);

            return new ImproverSuggestion
            {
                Reasoning = reasoningMatch.Success ? reasoningMatch.Groups[1].Value.Trim() : "(no reasoning)",
                Operation = operationMatch.Success ? operationMatch.Groups[1].Value.Trim().ToUpperInvariant() : "UNKNOWN",
                Prompt = prompt
            };
        }

        private static string FormatBreakdown(Dictionary<string, SectionBreakdown> breakdown)
        {
            if (breakdown == null) return "";

            var sb = new StringBuilder();
            foreach (var kvp in breakdown)
            {
                sb.AppendLine(string.Format("  {0}: {1:F2}", kvp.Key, kvp.Value.Score));
                if (kvp.Value.Issues != null)
                {
                    foreach (var issue in kvp.Value.Issues)
                    {
                        sb.AppendLine(string.Format("    {0}", issue));
                    }
                }
            }
            return sb.ToString().TrimEnd();
        }

        public static string BuildImproverMessage(
            string prompt,
            EvalResult evalResult,
            List<TestCase> testCases,
            List<HistoryEntry> history,
            ExtractionSchema extractionSchema,
            EvaluationConfig evaluation,
            string candidateHint)
        {
            var sb = new StringBuilder();

            // Output schema section
            sb.AppendLine("## Output schema (fixed, enforced by project config - the prompt cannot change this)");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(extractionSchema.Schema.ToString(Formatting.Indented));
            sb.AppendLine("```");
            sb.AppendLine();

            // Evaluation policy section
            sb.AppendLine("## Evaluation policy (fixed, enforced by project config)");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine(EvaluationPolicy.Format(evaluation));
            sb.AppendLine("```");
            sb.AppendLine();

            // Current prompt
            sb.AppendLine("## Current prompt");
            sb.AppendLine();
            sb.AppendLine(prompt);
            sb.AppendLine();

            // Candidate strategy
            if (!string.IsNullOrEmpty(candidateHint))
            {
                sb.AppendLine("## Candidate strategy for this attempt");
                sb.AppendLine();
                sb.AppendLine(candidateHint);
                sb.AppendLine();
            }

            // History
            if (history.Count > 0)
            {
                sb.AppendLine("## Previous attempts (do NOT repeat failed approaches)");
                sb.AppendLine();
                foreach (var entry in history)
                {
                    string desc = !string.IsNullOrEmpty(entry.Reasoning)
                        ? entry.Reasoning
                        : entry.Diff;
                    sb.AppendLine(string.Format("- **{0}** [{1}] (score: {2}) - {3}",
                        entry.Status,
                        string.IsNullOrEmpty(entry.Operation) ? "?" : entry.Operation,
                        entry.Score,
                        desc));
                    if (!string.IsNullOrEmpty(entry.CandidateLabel))
                        sb.AppendLine(string.Format("  candidate: {0}", entry.CandidateLabel));
                    if (!string.IsNullOrEmpty(entry.SectionSummary))
                        sb.AppendLine(string.Format("  sections: {0}", entry.SectionSummary));
                    if (!string.IsNullOrEmpty(entry.SectionDeltaSummary))
                        sb.AppendLine(string.Format("  deltas: {0}", entry.SectionDeltaSummary));
                }
                sb.AppendLine();
            }

            // Stuck warning
            string stuckWarning = DetectStuck(history);
            if (!string.IsNullOrEmpty(stuckWarning))
            {
                sb.Append(stuckWarning);
            }

            // Eval results
            sb.AppendLine(string.Format("## Eval results (avg: {0})", evalResult.Avg));
            sb.AppendLine();

            bool firstCase = true;
            foreach (var result in evalResult.Results)
            {
                if (!firstCase) sb.AppendLine().AppendLine();
                firstCase = false;

                var testCase = testCases.FirstOrDefault(tc => tc.Id == result.Id);
                string inputPreview = testCase != null
                    ? (testCase.Input.Length > 800 ? testCase.Input.Substring(0, 800) : testCase.Input)
                    : "(unavailable)";

                if (!string.IsNullOrEmpty(result.Error))
                {
                    sb.AppendLine(string.Format("### Case {0} - score: {1} - ERROR", result.Id, result.Score));
                    sb.AppendLine();
                    sb.AppendLine("**Input (truncated):**");
                    sb.AppendLine(inputPreview);
                    sb.AppendLine();
                    sb.AppendLine(string.Format("**Error:** {0}", result.Error));
                }
                else
                {
                    sb.AppendLine(string.Format("### Case {0} - score: {1}", result.Id, result.Score));
                    sb.AppendLine();
                    sb.AppendLine("**Score breakdown:**");
                    sb.AppendLine("```");
                    sb.AppendLine(FormatBreakdown(result.Breakdown));
                    sb.AppendLine("```");
                    sb.AppendLine();
                    sb.AppendLine("**Input (truncated):**");
                    sb.AppendLine(inputPreview);
                    sb.AppendLine();
                    sb.AppendLine("**Actual output:**");
                    sb.AppendLine("```json");
                    sb.AppendLine(result.Actual != null ? result.Actual.ToString(Formatting.Indented) : "null");
                    sb.AppendLine("```");
                    sb.AppendLine();
                    sb.AppendLine("**Expected output:**");
                    sb.AppendLine("```json");
                    sb.AppendLine(result.Expected != null ? result.Expected.ToString(Formatting.Indented) : "null");
                    sb.AppendLine("```");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Audit schema coverage and evaluation coverage first, then diagnose the dominant error type, then return the full improved prompt.");

            return sb.ToString();
        }

        public async Task<ImproverSuggestion> SuggestImprovementAsync(
            string prompt,
            EvalResult evalResult,
            List<TestCase> testCases,
            List<HistoryEntry> history,
            ExtractionSchema extractionSchema,
            EvaluationConfig evaluation,
            ResolvedModels models,
            string candidateHint,
            double? temperature = null)
        {
            string userMessage = BuildImproverMessage(
                prompt, evalResult, testCases, history,
                extractionSchema, evaluation, candidateHint);

            string stageLabel = "default";
            if (!string.IsNullOrEmpty(candidateHint))
            {
                int colonIdx = candidateHint.IndexOf(':');
                stageLabel = (colonIdx >= 0 ? candidateHint.Substring(0, colonIdx) : candidateHint)
                    .ToLowerInvariant()
                    .Replace(' ', '_');
            }

            string raw = await _llm.CompleteAsync(
                ImproverSystem.PROMPT,
                userMessage,
                model: models.Improver.Model,
                reasoning: models.Improver.Reasoning,
                temperature: temperature,
                stage: "improver/" + stageLabel);

            return ParseImproverResponse(raw);
        }
    }
}
