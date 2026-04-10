using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Wonderlands.Agents;
using FourthDevs.Wonderlands.Core;
using FourthDevs.Wonderlands.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Wonderlands.Scheduling
{
    public static class ContextAssembler
    {
        private static string Truncate(string text, int max = 120)
        {
            if (text == null) return "";
            return text.Length <= max ? text : text.Substring(0, max - 1) + "\u2026";
        }

        private static string SafeJson(object value, string fallback = "")
        {
            try { return JsonConvert.SerializeObject(value) ?? fallback; }
            catch { return fallback; }
        }

        private static string AsText(JToken value, string fallback = "")
        {
            if (value == null) return fallback;
            if (value.Type == JTokenType.String) return value.ToString();
            return SafeJson(value, fallback);
        }

        public static async Task<string> BuildRunPromptPrefix(Job job, Run run, Runtime rt)
        {
            var session = await rt.Sessions.GetById(job.SessionId);
            if (session == null) throw new Exception("Session not found: " + job.SessionId);

            var agentNamesStr = string.Join(", ", AgentRegistry.AgentNames);

            var sb = new StringBuilder();
            sb.AppendLine("You are continuing a job inside a Wonderlands multi-agent system.");
            sb.AppendLine("Older sealed execution history is available only through run memory. Raw items are appended separately and include only the unobserved tail.");
            sb.AppendLine();
            sb.AppendLine("Session title: " + session.Title);
            if (!string.IsNullOrEmpty(session.Goal)) sb.AppendLine("Session goal: " + session.Goal);
            sb.AppendLine();
            sb.AppendLine("Current agent: " + (run.AgentName ?? job.AgentName));
            sb.AppendLine("Current job id: " + job.Id);
            sb.AppendLine("Current job title: " + job.Title);
            sb.AppendLine("Current job priority: " + job.Priority);
            if (!string.IsNullOrEmpty(job.ParentJobId)) sb.AppendLine("Parent job id: " + job.ParentJobId);
            sb.AppendLine("Current run id: " + run.Id);
            sb.AppendLine("Turn count: " + run.TurnCount);
            sb.AppendLine();
            sb.AppendLine("Agent registry (use these names with delegate_to_agent): " + agentNamesStr);
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- ALWAYS use tools to change state. Plain text alone does nothing.");
            sb.AppendLine("- To delegate work, call delegate_to_agent with an agent name from the registry.");
            sb.AppendLine("- You may delegate multiple child jobs in one turn when they can run independently.");
            sb.AppendLine("- Use dependsOnJobIds when one delegated job must wait for another.");
            sb.AppendLine("- If you need source material, use read_artifact.");
            sb.AppendLine("- If you produce a document or notes, use write_artifact.");
            sb.AppendLine("- You MUST call complete_task when the current job is finished.");
            sb.AppendLine("- Do not call block_task just because child jobs are in flight; the scheduler suspends your run automatically and resumes you later.");
            sb.AppendLine("- If you cannot progress and there is no child work in flight, call block_task.");
            return sb.ToString();
        }

        public static async Task<JArray> BuildRunInput(Job job, Run run, Runtime rt, ReadinessEngine engine, string promptPrefix)
        {
            var runItems = await engine.GetRunItems(run.Id);
            int lastObservedSeq = run.Memory != null ? run.Memory.LastObservedSeq : 0;
            var activeItems = runItems.Where(i => i.Sequence > lastObservedSeq).OrderBy(i => i.Sequence).ToList();
            var observations = run.Memory != null ? (run.Memory.Observations ?? "").Trim() : "";
            var snapshot = await BuildJobSnapshot(job, rt, engine);

            var input = new JArray();
            input.Add(new JObject { ["type"] = "message", ["role"] = "user", ["content"] = promptPrefix });

            if (!string.IsNullOrEmpty(observations))
            {
                input.Add(new JObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = "Run memory (sealed older work from this run thread):\n\n" + observations
                });
            }

            input.Add(new JObject { ["type"] = "message", ["role"] = "user", ["content"] = snapshot });

            foreach (var item in activeItems)
            {
                var converted = ToInputItem(item);
                if (converted != null) input.Add(converted);
            }

            return input;
        }

        private static JObject ToInputItem(Item item)
        {
            switch (item.Type)
            {
                case "message":
                {
                    var role = item.Content != null && item.Content["role"] != null ? item.Content["role"].ToString() : "user";
                    string text;
                    if (role == "delegator")
                    {
                        var fromAgent = item.Content != null && item.Content["fromAgent"] != null ? item.Content["fromAgent"].ToString() : null;
                        var body = AsText(item.Content != null ? item.Content["text"] : null);
                        text = (!string.IsNullOrEmpty(fromAgent)
                            ? "Delegated instructions from " + fromAgent + ":\n\n"
                            : "Delegated instructions:\n\n") + body;
                    }
                    else
                    {
                        text = AsText(item.Content != null ? item.Content["text"] : null);
                    }
                    if (string.IsNullOrWhiteSpace(text)) return null;
                    return new JObject { ["type"] = "message", ["role"] = "user", ["content"] = text };
                }
                case "decision":
                {
                    var text = AsText(item.Content != null ? item.Content["text"] : null);
                    if (string.IsNullOrWhiteSpace(text)) return null;
                    return new JObject { ["type"] = "message", ["role"] = "assistant", ["content"] = text };
                }
                case "invocation":
                {
                    var callId = AsText(item.Content != null ? item.Content["callId"] : null);
                    var tool = AsText(item.Content != null ? item.Content["tool"] : null);
                    if (string.IsNullOrEmpty(callId) || string.IsNullOrEmpty(tool)) return null;
                    return new JObject
                    {
                        ["type"] = "function_call",
                        ["call_id"] = callId,
                        ["name"] = tool,
                        ["arguments"] = SafeJson(item.Content != null ? (object)item.Content["input"] ?? new JObject() : new JObject(), "{}"),
                        ["status"] = "completed"
                    };
                }
                case "result":
                {
                    var callId = AsText(item.Content != null ? item.Content["callId"] : null);
                    if (string.IsNullOrEmpty(callId)) return null;
                    return new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = callId,
                        ["output"] = AsText(item.Content != null ? item.Content["output"] : null),
                        ["status"] = "completed"
                    };
                }
                default: return null;
            }
        }

        private static async Task<string> BuildJobSnapshot(Job job, Runtime rt, ReadinessEngine engine)
        {
            var allJobs = await engine.GetSessionJobs(job.SessionId);
            var depArtifacts = await engine.GetDependencyArtifacts(job);
            var ownArtifacts = await engine.GetArtifactsProducedByJob(job.Id);

            var children = await rt.Jobs.Find(j => j.ParentJobId == job.Id);
            var childArtifacts = new List<Artifact>();
            foreach (var c in children)
            {
                var arts = await engine.GetArtifactsProducedByJob(c.Id);
                childArtifacts.AddRange(arts);
            }

            var sb = new StringBuilder();
            sb.AppendLine("## Current session jobs");
            foreach (var j in allJobs.OrderBy(j => j.Priority))
            {
                sb.AppendLine("- " + j.Id.Substring(0, 8) + " | " + j.Status + " | agent=" + (j.AgentName ?? "?") + " | " + j.Title);
            }

            if (depArtifacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Dependency artifacts (produced by jobs this job depends on)");
                foreach (var a in depArtifacts)
                    sb.AppendLine("- " + a.Path + " (id=" + a.Id.Substring(0, 8) + ", v" + a.Version + ")");
            }

            if (ownArtifacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Artifacts produced by this job");
                foreach (var a in ownArtifacts)
                    sb.AppendLine("- " + a.Path + " (id=" + a.Id.Substring(0, 8) + ", v" + a.Version + ")");
            }

            if (childArtifacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Artifacts produced by child jobs");
                foreach (var a in childArtifacts)
                    sb.AppendLine("- " + a.Path + " (id=" + a.Id.Substring(0, 8) + ", v" + a.Version + ")");
            }

            return sb.ToString();
        }
    }
}
