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
    public class ScoreBatch
    {
        private readonly LlmClient _llm;

        public ScoreBatch(LlmClient llm)
        {
            _llm = llm;
        }

        public static ExtractionSchema BuildJudgeSchema(
            List<string> caseIds,
            List<EvaluationSection> sections)
        {
            var properties = new JObject();
            var required = new JArray();

            foreach (var id in caseIds)
            {
                string caseKey = "case_" + id;
                required.Add(caseKey);

                var sectionProps = new JObject();
                var sectionRequired = new JArray();

                foreach (var section in sections)
                {
                    sectionRequired.Add(section.Key);
                    sectionProps[section.Key] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["score"] = new JObject { ["type"] = "number" },
                            ["issues"] = new JObject
                            {
                                ["type"] = "array",
                                ["items"] = new JObject { ["type"] = "string" }
                            }
                        },
                        ["required"] = new JArray("score", "issues")
                    };
                }

                properties[caseKey] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = sectionProps,
                    ["required"] = sectionRequired
                };
            }

            return new ExtractionSchema
            {
                Name = "batch_evaluation",
                Schema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required
                }
            };
        }

        public async Task<List<CaseResult>> ScoreAsync(
            List<CaseResult> cases,
            EvaluationConfig evaluation,
            ResolvedModels models)
        {
            var caseIds = cases.Select(c => c.Id).ToList();

            var caseSections = new List<string>();
            foreach (var testCase in cases)
            {
                caseSections.Add(string.Format(
                    "## Case {0}\n\n**Expected:**\n```json\n{1}\n```\n\n**Actual:**\n```json\n{2}\n```",
                    testCase.Id,
                    testCase.Expected != null ? testCase.Expected.ToString(Formatting.Indented) : "null",
                    testCase.Actual != null ? testCase.Actual.ToString(Formatting.Indented) : "null"));
            }

            string userMessage = string.Join("\n\n---\n\n", caseSections);

            var judgeSchema = BuildJudgeSchema(caseIds, evaluation.Sections);

            string raw = await _llm.CompleteAsync(
                Prompts.JudgeSystem.Build(evaluation),
                userMessage,
                model: models.Judge.Model,
                reasoning: models.Judge.Reasoning,
                jsonSchema: judgeSchema,
                stage: "judge/cases_" + string.Join("+", caseIds));

            var judgment = JObject.Parse(raw);
            var results = new List<CaseResult>();

            foreach (var testCase in cases)
            {
                var caseJudgment = judgment["case_" + testCase.Id] as JObject;
                double total = 0;
                var breakdown = new Dictionary<string, SectionBreakdown>();

                foreach (var section in evaluation.Sections)
                {
                    double score = 0;
                    var issues = new List<string>();

                    if (caseJudgment != null && caseJudgment[section.Key] is JObject sectionJudgment)
                    {
                        var scoreToken = sectionJudgment["score"];
                        if (scoreToken != null)
                        {
                            score = Math.Max(0, Math.Min(1, scoreToken.Value<double>()));
                        }

                        var issuesToken = sectionJudgment["issues"] as JArray;
                        if (issuesToken != null)
                        {
                            foreach (var issue in issuesToken)
                            {
                                issues.Add(issue.Value<string>());
                            }
                        }
                    }

                    total += score * section.Weight;
                    breakdown[section.Key] = new SectionBreakdown
                    {
                        Score = Math.Round(score * 10000) / 10000,
                        Weight = section.Weight,
                        Issues = issues
                    };
                }

                results.Add(new CaseResult
                {
                    Id = testCase.Id,
                    Score = Math.Round(total * 10000) / 10000,
                    Breakdown = breakdown,
                    Actual = testCase.Actual,
                    Expected = testCase.Expected,
                    Error = null
                });
            }

            return results;
        }
    }
}
