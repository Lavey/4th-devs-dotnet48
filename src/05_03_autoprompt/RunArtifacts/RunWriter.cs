using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FourthDevs.AutoPrompt.Config;
using FourthDevs.AutoPrompt.Llm;
using FourthDevs.AutoPrompt.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AutoPrompt.RunArtifacts
{
    public static class RunWriter
    {
        public static string WriteOptimizeRun(LoadedProject project, OptimizeRun run)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss");
            string runDir = Path.Combine(Defaults.RUNS_DIR, project.Name, timestamp);
            string diffsDir = Path.Combine(runDir, "diffs");

            Directory.CreateDirectory(diffsDir);

            // Write results.tsv
            var lines = new List<string>();
            lines.Add("iteration\tscore\tstatus\tcandidate\toperation\tsection_changes\tdescription");
            lines.Add(string.Format(
                "0\t{0:F4}\tbaseline\tbaseline\tbaseline\t-\tinitial prompt (+/-{1:F4})",
                run.Baseline.Avg, run.Baseline.Spread));

            foreach (var iteration in run.Iterations)
            {
                lines.Add(string.Format("{0}\t{1:F4}\t{2}\t{3}\t{4}\t{5}\t{6}",
                    iteration.Iteration,
                    iteration.Score,
                    iteration.Status,
                    iteration.CandidateLabel ?? "n/a",
                    iteration.Operation,
                    string.IsNullOrEmpty(iteration.SectionDeltaSummary) ? "-" : iteration.SectionDeltaSummary,
                    iteration.DiffSummary));
            }

            File.WriteAllText(
                Path.Combine(runDir, "results.tsv"),
                string.Join("\n", lines) + "\n");

            // Write run.json
            var runMeta = new JObject
            {
                ["project"] = project.Name,
                ["evalRuns"] = run.EvalRuns,
                ["maxIterations"] = run.MaxIterations,
                ["baselineScore"] = run.Baseline.Avg,
                ["bestScore"] = run.BestScore
            };

            // Add models info
            var modelsObj = new JObject();
            modelsObj["execution"] = JObject.FromObject(new
            {
                model = run.Models.Execution.Model,
                reasoning = run.Models.Execution.Reasoning != null
                    ? (object)new { effort = run.Models.Execution.Reasoning.Effort }
                    : null
            });
            modelsObj["judge"] = JObject.FromObject(new
            {
                model = run.Models.Judge.Model,
                reasoning = run.Models.Judge.Reasoning != null
                    ? (object)new { effort = run.Models.Judge.Reasoning.Effort }
                    : null
            });
            modelsObj["improver"] = JObject.FromObject(new
            {
                model = run.Models.Improver.Model,
                reasoning = run.Models.Improver.Reasoning != null
                    ? (object)new { effort = run.Models.Improver.Reasoning.Effort }
                    : null
            });
            runMeta["models"] = modelsObj;

            File.WriteAllText(
                Path.Combine(runDir, "run.json"),
                runMeta.ToString(Formatting.Indented) + "\n");

            // Write prompts
            File.WriteAllText(Path.Combine(runDir, "prompt.initial.md"), project.InitialPrompt);
            File.WriteAllText(Path.Combine(runDir, "prompt.best.md"), run.BestPrompt);

            // Write diffs
            foreach (var iteration in run.Iterations)
            {
                WriteDiffLog(diffsDir, iteration);
            }

            // Write traces
            var traces = TraceCollector.Collect();
            if (traces.Count > 0)
            {
                string tracesDir = Path.Combine(runDir, "traces");
                Directory.CreateDirectory(tracesDir);

                var byStage = new Dictionary<string, List<TraceEntry>>();
                foreach (var trace in traces)
                {
                    string stageKey = trace.Stage.Replace("/", "__");
                    if (!byStage.ContainsKey(stageKey))
                    {
                        byStage[stageKey] = new List<TraceEntry>();
                    }
                    byStage[stageKey].Add(trace);
                }

                foreach (var kvp in byStage)
                {
                    File.WriteAllText(
                        Path.Combine(tracesDir, kvp.Key + ".json"),
                        JsonConvert.SerializeObject(kvp.Value, Formatting.Indented) + "\n");
                }
            }

            return runDir;
        }

        private static void WriteDiffLog(string diffsDir, IterationResult iteration)
        {
            string header = string.Format(
                "# Iteration {0} - {1} - score: {2:F4}\n",
                iteration.Iteration, iteration.Status, iteration.Score);

            var metaParts = new List<string>();
            metaParts.Add(string.Format("Candidate: {0}", iteration.CandidateLabel ?? "n/a"));
            metaParts.Add(string.Format("Operation: {0}", iteration.Operation));
            metaParts.Add(string.Format("Reasoning: {0}", iteration.Reasoning));

            if (!string.IsNullOrEmpty(iteration.SectionSummary))
                metaParts.Add(string.Format("Section scores: {0}", iteration.SectionSummary));
            if (!string.IsNullOrEmpty(iteration.SectionDeltaSummary))
                metaParts.Add(string.Format("Section deltas: {0}", iteration.SectionDeltaSummary));

            metaParts.Add("");

            if (iteration.Candidates != null && iteration.Candidates.Count > 0)
            {
                metaParts.Add("Candidates:");
                foreach (var candidate in iteration.Candidates)
                {
                    string line = string.Format("- {0}: {1:F4} ({2}) {3}",
                        candidate.CandidateLabel,
                        candidate.Score,
                        candidate.Status,
                        candidate.SectionDeltaSummary ?? "").Trim();
                    metaParts.Add(line);
                }
                metaParts.Add("");
            }

            string meta = string.Join("\n", metaParts);
            string fileName = string.Format("{0}_{1}.diff",
                iteration.Iteration.ToString().PadLeft(3, '0'),
                iteration.Status);

            File.WriteAllText(
                Path.Combine(diffsDir, fileName),
                header + meta + "\n" + (iteration.Diff ?? "") + "\n");
        }
    }
}
