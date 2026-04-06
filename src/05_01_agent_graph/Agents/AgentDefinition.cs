using System.Collections.Generic;

namespace FourthDevs.AgentGraph.Agents
{
    public sealed class AgentDefinition
    {
        public string Name { get; set; }
        public string Type { get; set; } // "user" or "agent"
        public string[] Tools { get; set; }
        public string Instructions { get; set; }
        public bool WebSearch { get; set; }
        public int? MaxSteps { get; set; }
    }

    public static class AgentRegistry
    {
        private static readonly Dictionary<string, AgentDefinition> Agents = new Dictionary<string, AgentDefinition>
        {
            ["orchestrator"] = new AgentDefinition
            {
                Name = "orchestrator",
                Type = "agent",
                Tools = new[] { "create_actor", "delegate_task", "complete_task", "block_task" },
                MaxSteps = 15,
                Instructions = string.Join("\n", new[]
                {
                    "You are the orchestrator for this session.",
                    "First, assess the user request. If it is simple (a greeting, a short question, a trivial ask), call complete_task directly with the answer as the summary. Do NOT delegate simple requests.",
                    "For complex tasks that require research, writing, or multi-step work: create or reuse specialists, then delegate the concrete child tasks they should perform.",
                    "If multiple child tasks can run independently, you may delegate multiple tasks in the same turn.",
                    "If one child task depends on another, pass dependsOnTaskIds so the scheduler enforces the dependency.",
                    "After you have delegated the child work needed for now, simply stop. Do NOT call block_task just to wait for children; the scheduler will resume you automatically.",
                    "",
                    "When you are resumed after child tasks complete:",
                    "- Check \"Current session tasks\" and \"Artifacts produced by child tasks\" in the snapshot.",
                    "- If more work is needed, delegate the next batch.",
                    "- If the original goal is satisfied, call complete_task with a summary of what was accomplished.",
                    "",
                    "Reuse existing actors when they already fit the job.",
                    "Use block_task only for a real blocker that child tasks cannot resolve.",
                    "A simple research-then-write pipeline is usually sufficient. Do not over-engineer with review rounds unless explicitly asked.",
                })
            },
            ["researcher"] = new AgentDefinition
            {
                Name = "researcher",
                Type = "agent",
                Tools = new[] { "write_artifact", "complete_task", "block_task" },
                WebSearch = true,
                MaxSteps = 8,
                Instructions = "You are the researcher with live web search access. Use web search to find current, accurate information when the topic benefits from up-to-date data. For well-established topics, your own knowledge is sufficient. Include concrete facts, sources, code examples, and practical implications. Cite URLs where possible. Write well-organized markdown research notes using write_artifact, then call complete_task."
            },
            ["writer"] = new AgentDefinition
            {
                Name = "writer",
                Type = "agent",
                Tools = new[] { "read_artifact", "write_artifact", "complete_task", "block_task" },
                MaxSteps = 8,
                Instructions = "You are the writer. Before drafting, read the dependency artifacts to gather evidence using read_artifact. Then write a polished markdown article using write_artifact and call complete_task."
            },
            ["email_writer"] = new AgentDefinition
            {
                Name = "email_writer",
                Type = "agent",
                Tools = new[] { "read_artifact", "send_email", "complete_task", "block_task" },
                MaxSteps = 6,
                Instructions = "You are an email writer. Read dependency artifacts first using read_artifact to gather the material. Then compose a professional, well-structured email using send_email. Use {{file:path}} in the email body to inline artifact content when appropriate instead of rewriting it. Call complete_task when done."
            },
        };

        public static readonly string[] BootstrapAgents = { "orchestrator" };

        public static string[] AgentNames
        {
            get
            {
                var names = new string[Agents.Count];
                Agents.Keys.CopyTo(names, 0);
                return names;
            }
        }

        public static AgentDefinition Get(string name)
        {
            AgentDefinition def;
            Agents.TryGetValue(name, out def);
            return def;
        }
    }
}
