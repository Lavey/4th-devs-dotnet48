using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Agents;
using FourthDevs.AgentGraph.Core;
using FourthDevs.AgentGraph.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentGraph.Scheduler
{
    public static class ContextBuilder
    {
        private static string Truncate(string text, int max = 120)
        {
            if (text == null) return "";
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
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

        public static async Task<string> BuildTaskPromptPrefix(AgentTask task, Actor actor, Runtime rt)
        {
            var session = await rt.Sessions.GetById(task.SessionId);
            if (session == null) throw new Exception("Session not found: " + task.SessionId);

            var agentNamesStr = string.Join(", ", AgentRegistry.AgentNames);

            var sb = new StringBuilder();
            sb.AppendLine("You are continuing a task inside a blackboard-style multi-agent system.");
            sb.AppendLine("Older sealed execution history is available only through task memory. Raw task items are appended separately and include only the unobserved tail.");
            sb.AppendLine();
            sb.AppendLine("Session title: " + session.Title);
            if (!string.IsNullOrEmpty(session.Goal)) sb.AppendLine("Session goal: " + session.Goal);
            sb.AppendLine();
            sb.AppendLine("Current actor: " + actor.Name + " (" + actor.Type + ")");
            sb.AppendLine("Current task id: " + task.Id);
            sb.AppendLine("Current task title: " + task.Title);
            sb.AppendLine("Current task priority: " + task.Priority);
            if (!string.IsNullOrEmpty(task.ParentTaskId)) sb.AppendLine("Parent task id: " + task.ParentTaskId);
            sb.AppendLine();
            sb.AppendLine("Agent registry (use these exact names with create_actor to get predefined tools and instructions): " + agentNamesStr);
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- ALWAYS use tools to change state. Plain text alone does nothing — the scheduler will block your task if you respond without tool calls.");
            sb.AppendLine("- To create a new specialist, call create_actor with a name from the agent registry, then delegate_task.");
            sb.AppendLine("- If you need another actor to work, use delegate_task with an existing actor name.");
            sb.AppendLine("- You may delegate multiple child tasks in one turn when they can run independently.");
            sb.AppendLine("- Use dependsOnTaskIds when one delegated task must wait for another.");
            sb.AppendLine("- If you need source material, use read_artifact.");
            sb.AppendLine("- If you produce a document or notes, use write_artifact.");
            sb.AppendLine("- You MUST call complete_task when the current task is finished. It is the only way to mark a task done.");
            sb.AppendLine("- Do not call block_task just because child tasks are in flight; the scheduler waits automatically and resumes you later.");
            sb.AppendLine("- If you cannot progress and there is no child work in flight, call block_task.");
            return sb.ToString();
        }

        public static async Task<JArray> BuildTaskRunInput(AgentTask task, Runtime rt, GraphQueries graph, string promptPrefix)
        {
            var currentTask = await rt.Tasks.GetById(task.Id);
            if (currentTask == null) throw new Exception("Task not found: " + task.Id);

            var taskItems = await graph.GetTaskItems(currentTask.Id);
            int lastObservedSeq = currentTask.Memory != null ? currentTask.Memory.LastObservedSeq : 0;
            var activeItems = taskItems.Where(i => i.Sequence > lastObservedSeq).OrderBy(i => i.Sequence).ToList();
            var observations = currentTask.Memory != null ? (currentTask.Memory.Observations ?? "").Trim() : "";
            var snapshot = await BuildTaskSnapshot(currentTask, rt, graph);

            var input = new JArray();
            input.Add(new JObject { ["type"] = "message", ["role"] = "user", ["content"] = promptPrefix });

            if (!string.IsNullOrEmpty(observations))
            {
                input.Add(new JObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = "Task memory (sealed older work from this task thread):\n\n" + observations
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
                        var fromActor = item.Content != null && item.Content["fromActor"] != null ? item.Content["fromActor"].ToString() : null;
                        var body = AsText(item.Content != null ? item.Content["text"] : null);
                        text = (!string.IsNullOrEmpty(fromActor)
                            ? "Delegated instructions from " + fromActor + ":\n\n"
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

        private static string FormatTaskLine(AgentTask task, Dictionary<string, string> ownerNames)
        {
            string owner = "?";
            if (!string.IsNullOrEmpty(task.OwnerActorId) && ownerNames.ContainsKey(task.OwnerActorId))
                owner = ownerNames[task.OwnerActorId];

            var parts = new List<string> { "- " + task.Id, task.Status, "owner=" + owner };

            if (task.Recovery != null)
            {
                if (task.Recovery.AutoRetry && !string.IsNullOrEmpty(task.Recovery.NextRetryAt))
                    parts.Add("retry_at=" + task.Recovery.NextRetryAt);
                else if (!string.IsNullOrEmpty(task.Recovery.LastFailureMessage))
                    parts.Add("note=" + Truncate(task.Recovery.LastFailureMessage));
            }

            parts.Add(task.Title);
            return string.Join(" | ", parts);
        }

        private static async Task<string> BuildTaskSnapshot(AgentTask task, Runtime rt, GraphQueries graph)
        {
            var actors = await graph.GetSessionActors(task.SessionId);
            var tasks = await graph.GetSessionTasks(task.SessionId);
            var depTasks = await graph.GetDependencyTasks(task);
            var depArtifacts = await graph.GetDependencyArtifacts(task);
            var ownArtifacts = await graph.GetArtifactsProducedByTask(task.Id);

            // Get child task artifacts
            var children = await rt.Tasks.Find(t => t.ParentTaskId == task.Id);
            var childArtifacts = new List<Artifact>();
            foreach (var c in children)
            {
                var arts = await graph.GetArtifactsProducedByTask(c.Id);
                childArtifacts.AddRange(arts);
            }

            var ownerNames = actors.ToDictionary(a => a.Id, a => a.Name);

            var sb = new StringBuilder();
            sb.AppendLine("Current task snapshot:");
            sb.AppendLine();
            sb.AppendLine("Available actors:");
            foreach (var a in actors)
            {
                var toolNames = a.Capabilities != null && a.Capabilities.Tools != null ? string.Join(", ", a.Capabilities.Tools) : "none";
                sb.AppendLine("- " + a.Name + " (" + a.Type + ") tools=" + toolNames);
            }
            sb.AppendLine();
            sb.AppendLine("Current session tasks:");
            foreach (var t in tasks.OrderBy(t2 => t2.Priority))
                sb.AppendLine(FormatTaskLine(t, ownerNames));

            sb.AppendLine();
            sb.AppendLine(depTasks.Count > 0 ? "Dependency tasks:" : "Dependency tasks: none");
            foreach (var t in depTasks) sb.AppendLine("- " + t.Id + " | " + t.Status + " | " + t.Title);

            sb.AppendLine();
            sb.AppendLine(depArtifacts.Count > 0 ? "Dependency artifacts available to read:" : "Dependency artifacts available to read: none");
            foreach (var a in depArtifacts) sb.AppendLine("- " + a.Id + " | " + a.Path + " | kind=" + a.Kind + " | version=" + a.Version);

            sb.AppendLine();
            sb.AppendLine(childArtifacts.Count > 0 ? "Artifacts produced by child tasks:" : "Artifacts produced by child tasks: none");
            foreach (var a in childArtifacts) sb.AppendLine("- " + a.Id + " | " + a.Path + " | kind=" + a.Kind + " | version=" + a.Version);

            sb.AppendLine();
            sb.AppendLine(ownArtifacts.Count > 0 ? "Artifacts already produced by this task:" : "Artifacts already produced by this task: none");
            foreach (var a in ownArtifacts) sb.AppendLine("- " + a.Id + " | " + a.Path + " | kind=" + a.Kind + " | version=" + a.Version);

            return sb.ToString();
        }
    }
}
