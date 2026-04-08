using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.AutoPrompt.Llm;
using FourthDevs.AutoPrompt.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AutoPrompt.Core
{
    public class RunEvaluation
    {
        private readonly LlmClient _llm;
        private readonly ScoreBatch _scorer;

        public RunEvaluation(LlmClient llm)
        {
            _llm = llm;
            _scorer = new ScoreBatch(llm);
        }

        private static string BuildUserMessage(TestCase testCase)
        {
            if (testCase.Context == null)
                return testCase.Input;

            return string.Format(
                "## Context\n\n```json\n{0}\n```\n\n## Input\n\n{1}",
                testCase.Context.ToString(Formatting.Indented),
                testCase.Input);
        }

        public async Task<SingleRunResult> RunSingleAsync(
            string prompt,
            List<TestCase> testCases,
            ExtractionSchema extractionSchema,
            EvaluationConfig evaluation,
            ResolvedModels models)
        {
            var extractions = new List<CaseResult>();

            foreach (var testCase in testCases)
            {
                try
                {
                    string raw = await _llm.CompleteAsync(
                        prompt,
                        BuildUserMessage(testCase),
                        model: models.Execution.Model,
                        reasoning: models.Execution.Reasoning,
                        jsonSchema: extractionSchema,
                        stage: "extraction/case_" + testCase.Id);

                    extractions.Add(new CaseResult
                    {
                        Id = testCase.Id,
                        Actual = JObject.Parse(raw),
                        Expected = testCase.Expected,
                        Error = null
                    });
                }
                catch (Exception ex)
                {
                    extractions.Add(new CaseResult
                    {
                        Id = testCase.Id,
                        Actual = null,
                        Expected = testCase.Expected,
                        Error = ex.Message
                    });
                }
            }

            var succeeded = extractions.Where(e => e.Actual != null).ToList();
            var failed = extractions.Where(e => e.Actual == null).ToList();

            var judged = succeeded.Count > 0
                ? await _scorer.ScoreAsync(succeeded, evaluation, models)
                : new List<CaseResult>();

            var failedResults = failed.Select(e => new CaseResult
            {
                Id = e.Id,
                Score = 0,
                Breakdown = null,
                Actual = null,
                Expected = e.Expected,
                Error = e.Error
            });

            var results = judged.Concat(failedResults)
                .OrderBy(r => r.Id, StringComparer.Ordinal)
                .ToList();

            double average = results.Count > 0
                ? results.Sum(r => r.Score) / results.Count
                : 0;

            return new SingleRunResult
            {
                Avg = Math.Round(average * 10000) / 10000,
                Results = results
            };
        }

        public async Task<EvalResult> RunAsync(
            string prompt,
            List<TestCase> testCases,
            ExtractionSchema extractionSchema,
            EvaluationConfig evaluation,
            ResolvedModels models,
            int runs)
        {
            var allRuns = new List<SingleRunResult>();
            for (int i = 0; i < runs; i++)
            {
                var run = await RunSingleAsync(prompt, testCases, extractionSchema, evaluation, models);
                allRuns.Add(run);
            }

            double avgScore = Math.Round(
                allRuns.Sum(r => r.Avg) / runs * 10000) / 10000;

            var sorted = allRuns.OrderBy(r => r.Avg).ToList();
            var median = sorted[sorted.Count / 2];
            double spread = sorted[sorted.Count - 1].Avg - sorted[0].Avg;

            return new EvalResult
            {
                Avg = avgScore,
                Results = median.Results,
                Spread = spread,
                Runs = allRuns
            };
        }
    }
}
