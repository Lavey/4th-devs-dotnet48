using System.Collections.Generic;

namespace FourthDevs.Wonderlands.Agents
{
    public sealed class AgentDefinition
    {
        public string Name { get; set; }
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
                Tools = new[] { "delegate_to_agent", "write_artifact", "read_artifact", "complete_task", "block_task" },
                MaxSteps = 15,
                Instructions = string.Join("\n", new[]
                {
                    "You are the orchestrator for this session.",
                    "First, assess the user request. If it is simple (a greeting, a short question, a trivial ask), call complete_task directly with the answer as the summary. Do NOT delegate simple requests.",
                    "For complex tasks that require research, writing, or multi-step work: delegate concrete child jobs to specialist agents.",
                    "If multiple child jobs can run independently, delegate them in the same turn.",
                    "If one job depends on another, pass dependsOnJobIds so the readiness engine enforces the dependency.",
                    "After you have delegated the child work needed for now, simply stop. Do NOT call block_task just to wait for children; the scheduler will suspend your run automatically and resume you when children complete.",
                    "",
                    "When you are resumed after child jobs complete:",
                    "- Check the snapshot for completed child jobs and their artifacts.",
                    "- If more work is needed, delegate the next batch.",
                    "- If the original goal is satisfied, call complete_task with a summary.",
                    "",
                    "Reuse existing agent names when they fit the job.",
                    "Use block_task only for a real blocker that child jobs cannot resolve.",
                })
            },
            ["researcher"] = new AgentDefinition
            {
                Name = "researcher",
                Tools = new[] { "write_artifact", "complete_task", "block_task" },
                WebSearch = true,
                MaxSteps = 8,
                Instructions = "You are the researcher with live web search access. Use web search to find current, accurate information. Include concrete facts, sources, code examples, and practical implications. Cite URLs where possible. Write well-organized markdown research notes using write_artifact, then call complete_task."
            },
            ["writer"] = new AgentDefinition
            {
                Name = "writer",
                Tools = new[] { "read_artifact", "write_artifact", "complete_task", "block_task" },
                MaxSteps = 8,
                Instructions = "You are the writer. Before drafting, read the dependency artifacts to gather evidence using read_artifact. Then write a polished markdown article using write_artifact and call complete_task."
            },
            ["email_writer"] = new AgentDefinition
            {
                Name = "email_writer",
                Tools = new[] { "read_artifact", "write_artifact", "complete_task", "block_task" },
                MaxSteps = 6,
                Instructions = "You are an email writer. Read dependency artifacts first using read_artifact to gather the material. Then compose a professional, well-structured email using write_artifact. Call complete_task when done."
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
