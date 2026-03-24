namespace FourthDevs.Events.Workflows
{
    public static class ReportV1Workflow
    {
        public static WorkflowDefinition Create()
        {
            return new WorkflowDefinition
            {
                Id = "report-v1",
                Project = new WorkflowProjectMetadata
                {
                    Id = "report-v1-project",
                    Title = "Research Report v1",
                    Description = "Generate a research report with evidence gathering, planning, writing, and editing.",
                    DeliverablePath = "deliverables/report.md"
                },
                AgentOrder = new[] { "researcher", "planner", "writer", "editor" },
                Tasks = new[]
                {
                    new SeedTaskDefinition
                    {
                        Filename = "01-research.md",
                        Id = "task-research",
                        Title = "Gather research and evidence",
                        Owner = "researcher",
                        Priority = "high",
                        DependsOn = new string[0],
                        OutputPath = "notes/research.md",
                        Body = "Research the topic defined in goal.md. Gather evidence, sources, and key findings. Save structured notes."
                    },
                    new SeedTaskDefinition
                    {
                        Filename = "02-plan.md",
                        Id = "task-plan",
                        Title = "Create report outline and structure",
                        Owner = "planner",
                        Priority = "high",
                        DependsOn = new[] { "task-research" },
                        OutputPath = "work/outline.md",
                        Body = "Based on research notes, create a detailed outline for the report. Define sections, key arguments, and structure."
                    },
                    new SeedTaskDefinition
                    {
                        Filename = "03-write.md",
                        Id = "task-write",
                        Title = "Write the report",
                        Owner = "writer",
                        Priority = "high",
                        DependsOn = new[] { "task-plan" },
                        OutputPath = "report/final-report.md",
                        Body = "Write the full report following the outline. Include evidence from research notes. Aim for clear, professional prose."
                    },
                    new SeedTaskDefinition
                    {
                        Filename = "04-edit.md",
                        Id = "task-edit",
                        Title = "Edit and finalize the report",
                        Owner = "editor",
                        Priority = "medium",
                        DependsOn = new[] { "task-write" },
                        OutputPath = "deliverables/report.md",
                        Body = "Review and edit the report for clarity, accuracy, and style. Produce the final deliverable."
                    }
                }
            };
        }
    }
}
