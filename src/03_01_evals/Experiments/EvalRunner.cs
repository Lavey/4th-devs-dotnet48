using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourthDevs.Evals.Agent;
using FourthDevs.Evals.Core;
using FourthDevs.Evals.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Evals.Experiments
{
    /// <summary>
    /// Evaluation runner that loads dataset JSON files, runs the agent for each
    /// test case, scores the results, and prints a summary report.
    /// </summary>
    internal static class EvalRunner
    {
        // ------------------------------------------------------------------
        // Tool-use evaluation
        // ------------------------------------------------------------------

        public static async Task RunToolUseEval(Logger logger)
        {
            string path = ResolveDatasetPath("tool-use.synthetic.json");
            JArray dataset = JArray.Parse(File.ReadAllText(path));

            Console.WriteLine();
            Console.WriteLine("=== Tool-Use Evaluation ===");
            Console.WriteLine(string.Format("  Dataset : {0}", path));
            Console.WriteLine(string.Format("  Cases   : {0}", dataset.Count));
            Console.WriteLine();

            var scores = new List<ToolUseScore>();

            foreach (JObject testCase in dataset)
            {
                string id = (string)testCase["id"];
                string message = (string)testCase["message"];
                JObject expect = (JObject)testCase["expect"];

                Console.Write(string.Format("  [{0}] {1} ... ", id, Truncate(message, 50)));

                try
                {
                    var session = new Session { Id = "eval-tu-" + id, Messages = new List<object>() };
                    var result = await AgentRunner.RunAsync(logger, session, message)
                        .ConfigureAwait(false);

                    var score = ScoreToolUse(result, expect);
                    scores.Add(score);

                    Console.WriteLine(string.Format("overall={0:F2}", score.Overall));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message);
                    scores.Add(new ToolUseScore
                    {
                        Id = id,
                        DecisionAccuracy = 0,
                        RequiredToolsAccuracy = 0,
                        ForbiddenToolsAccuracy = 0,
                        CallCountAccuracy = 0,
                        Overall = 0
                    });
                }
            }

            PrintToolUseSummary(scores);
        }

        private static ToolUseScore ScoreToolUse(AgentRunResult result, JObject expect)
        {
            bool shouldUseTools = expect["shouldUseTools"]?.Value<bool>() ?? false;
            var requiredTools = expect["requiredTools"]?.ToObject<List<string>>() ?? new List<string>();
            var forbiddenTools = expect["forbiddenTools"]?.ToObject<List<string>>() ?? new List<string>();
            int? minToolCalls = expect["minToolCalls"]?.Value<int>();
            int? maxToolCalls = expect["maxToolCalls"]?.Value<int>();

            bool agentUsedTools = result.ToolCallCount > 0;

            // decision_accuracy: did the agent correctly decide to use / not use tools?
            double decisionAccuracy = (shouldUseTools == agentUsedTools) ? 1.0 : 0.0;

            // required_tools_accuracy: were all required tools used?
            double requiredToolsAccuracy = 1.0;
            if (requiredTools.Count > 0)
            {
                int found = requiredTools.Count(t => result.ToolsUsed.Contains(t));
                requiredToolsAccuracy = (double)found / requiredTools.Count;
            }

            // forbidden_tools_accuracy: were no forbidden tools used?
            double forbiddenToolsAccuracy = 1.0;
            if (forbiddenTools.Count > 0)
            {
                int violations = forbiddenTools.Count(t => result.ToolsUsed.Contains(t));
                forbiddenToolsAccuracy = violations == 0 ? 1.0 : 0.0;
            }

            // call_count_accuracy: within min/max bounds?
            double callCountAccuracy = 1.0;
            if (minToolCalls.HasValue && result.ToolCallCount < minToolCalls.Value)
                callCountAccuracy = 0.0;
            if (maxToolCalls.HasValue && result.ToolCallCount > maxToolCalls.Value)
                callCountAccuracy = 0.0;

            double overall = (decisionAccuracy + requiredToolsAccuracy +
                              forbiddenToolsAccuracy + callCountAccuracy) / 4.0;

            return new ToolUseScore
            {
                DecisionAccuracy = decisionAccuracy,
                RequiredToolsAccuracy = requiredToolsAccuracy,
                ForbiddenToolsAccuracy = forbiddenToolsAccuracy,
                CallCountAccuracy = callCountAccuracy,
                Overall = overall
            };
        }

        private static void PrintToolUseSummary(List<ToolUseScore> scores)
        {
            if (scores.Count == 0) return;

            double avgDecision = scores.Average(s => s.DecisionAccuracy);
            double avgRequired = scores.Average(s => s.RequiredToolsAccuracy);
            double avgForbidden = scores.Average(s => s.ForbiddenToolsAccuracy);
            double avgCallCount = scores.Average(s => s.CallCountAccuracy);
            double avgOverall = scores.Average(s => s.Overall);

            Console.WriteLine();
            Console.WriteLine("  --- Tool-Use Summary ---");
            Console.WriteLine(string.Format("  decision_accuracy     : {0:F2}", avgDecision));
            Console.WriteLine(string.Format("  required_tools_accuracy: {0:F2}", avgRequired));
            Console.WriteLine(string.Format("  forbidden_tools_accuracy: {0:F2}", avgForbidden));
            Console.WriteLine(string.Format("  call_count_accuracy   : {0:F2}", avgCallCount));
            Console.WriteLine(string.Format("  overall               : {0:F2}", avgOverall));
            Console.WriteLine(string.Format("  cases                 : {0}", scores.Count));
            Console.WriteLine();
        }

        // ------------------------------------------------------------------
        // Response-correctness evaluation
        // ------------------------------------------------------------------

        public static async Task RunResponseCorrectnessEval(Logger logger)
        {
            string path = ResolveDatasetPath("response-correctness.synthetic.json");
            JArray dataset = JArray.Parse(File.ReadAllText(path));

            Console.WriteLine();
            Console.WriteLine("=== Response-Correctness Evaluation ===");
            Console.WriteLine(string.Format("  Dataset : {0}", path));
            Console.WriteLine(string.Format("  Cases   : {0}", dataset.Count));
            Console.WriteLine();

            var scores = new List<ResponseScore>();

            foreach (JObject testCase in dataset)
            {
                string id = (string)testCase["id"];
                string message = (string)testCase["message"];
                JObject expect = (JObject)testCase["expect"];

                Console.Write(string.Format("  [{0}] {1} ... ", id, Truncate(message, 50)));

                try
                {
                    var session = new Session { Id = "eval-rc-" + id, Messages = new List<object>() };
                    var result = await AgentRunner.RunAsync(logger, session, message)
                        .ConfigureAwait(false);

                    double score = ScoreResponseCorrectness(result.Response, expect);
                    scores.Add(new ResponseScore { Id = id, Score = score });

                    Console.WriteLine(string.Format("score={0:F2}", score));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message);
                    scores.Add(new ResponseScore { Id = id, Score = 0 });
                }
            }

            PrintResponseSummary(scores);
        }

        private static double ScoreResponseCorrectness(string response, JObject expect)
        {
            string type = (string)expect["type"];

            switch (type)
            {
                case "exact_number":
                    return ScoreExactNumber(response, expect["value"].Value<double>());

                case "contains_iso_timestamp":
                    return ScoreContainsTimestamp(response);

                case "relevance":
                    return ScoreRelevance(response, (string)expect["topic"]);

                default:
                    return 0.0;
            }
        }

        private static double ScoreExactNumber(string response, double expected)
        {
            // Extract all numbers from the response
            var matches = Regex.Matches(response, @"-?\d+\.?\d*");
            foreach (Match m in matches)
            {
                double parsed;
                if (double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    if (Math.Abs(parsed - expected) < 0.01)
                        return 1.0;
                }
            }
            return 0.0;
        }

        private static double ScoreContainsTimestamp(string response)
        {
            // Check for ISO 8601 timestamp pattern (e.g. 2025-01-15T10:30:00Z or 2025-01-15T10:30:00.000Z)
            bool match = Regex.IsMatch(response,
                @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}");
            return match ? 1.0 : 0.0;
        }

        private static double ScoreRelevance(string response, string topic)
        {
            if (string.IsNullOrWhiteSpace(response))
                return 0.0;

            string lower = response.ToLowerInvariant();

            if (topic != null && topic.ToLowerInvariant().Contains("greeting"))
            {
                // Check for greeting-like words
                string[] greetingWords = { "hello", "hi", "hey", "greetings", "welcome" };
                foreach (string word in greetingWords)
                {
                    if (lower.Contains(word))
                        return 1.0;
                }
                return 0.0;
            }

            // For topic relevance, check if keywords from the topic appear in the response
            if (!string.IsNullOrWhiteSpace(topic))
            {
                string[] keywords = topic.ToLowerInvariant().Split(' ');
                int found = keywords.Count(k => k.Length > 2 && lower.Contains(k));
                return found > 0 ? 1.0 : 0.0;
            }

            return response.Length > 5 ? 0.5 : 0.0;
        }

        private static void PrintResponseSummary(List<ResponseScore> scores)
        {
            if (scores.Count == 0) return;

            double avg = scores.Average(s => s.Score);
            int passed = scores.Count(s => s.Score >= 1.0);

            Console.WriteLine();
            Console.WriteLine("  --- Response-Correctness Summary ---");
            Console.WriteLine(string.Format("  average_score : {0:F2}", avg));
            Console.WriteLine(string.Format("  passed        : {0}/{1}", passed, scores.Count));
            Console.WriteLine(string.Format("  cases         : {0}", scores.Count));
            Console.WriteLine();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static string ResolveDatasetPath(string filename)
        {
            // Try relative to the working directory first
            string basePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Experiments", "Datasets", filename);

            if (File.Exists(basePath))
                return basePath;

            // Fallback: relative to the project directory
            string projectPath = Path.Combine("Experiments", "Datasets", filename);
            if (File.Exists(projectPath))
                return projectPath;

            throw new FileNotFoundException(
                string.Format("Dataset file not found: {0}", filename));
        }

        private static string Truncate(string text, int maxLen)
        {
            if (text == null) return string.Empty;
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
        }

        // ------------------------------------------------------------------
        // Score model classes
        // ------------------------------------------------------------------

        private sealed class ToolUseScore
        {
            public string Id { get; set; }
            public double DecisionAccuracy { get; set; }
            public double RequiredToolsAccuracy { get; set; }
            public double ForbiddenToolsAccuracy { get; set; }
            public double CallCountAccuracy { get; set; }
            public double Overall { get; set; }
        }

        private sealed class ResponseScore
        {
            public string Id { get; set; }
            public double Score { get; set; }
        }
    }
}
