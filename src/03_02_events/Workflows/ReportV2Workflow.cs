namespace FourthDevs.Events.Workflows
{
    public static class ReportV2Workflow
    {
        public static WorkflowDefinition Create()
        {
            return new WorkflowDefinition
            {
                Id = "report-v2",
                Project = new WorkflowProjectMetadata
                {
                    Id = "report-v2-project",
                    Title = "Research Report v2",
                    Description = "Generate a styled research report with evidence, planning, writing, editing, and visual design.",
                    DeliverablePath = "deliverables/report.html"
                },
                AgentOrder = new[] { "researcher", "planner", "writer", "editor", "designer" },
                Tasks = new[]
                {
                    new SeedTaskDefinition
                    {
                        Filename = "01-research.md",
                        Id = "task-research",
                        Title = "Gather research and evidence",
                        Owner = "researcher",
                        RequiredCapabilities = new[] { "web_research", "evidence_gathering" },
                        Priority = "high",
                        DependsOn = new string[0],
                        OutputPath = "notes/research.md",
                        Body = "Research the topic defined in goal.md. Use web search to gather current information. Save structured notes with sources."
                    },
                    new SeedTaskDefinition
                    {
                        Filename = "02-plan.md",
                        Id = "task-plan",
                        Title = "Create report outline and structure",
                        Owner = "planner",
                        RequiredCapabilities = new[] { "strategic_planning" },
                        Priority = "high",
                        DependsOn = new[] { "task-research" },
                        OutputPath = "work/outline.md",
                        Body = "Based on research notes, create a detailed outline. Define sections, key arguments, data points to include, and visual elements."
                    },
                    new SeedTaskDefinition
                    {
                        Filename = "03-write.md",
                        Id = "task-write",
                        Title = "Write the report",
                        Owner = "writer",
                        RequiredCapabilities = new[] { "report_writing" },
                        Priority = "high",
                        DependsOn = new[] { "task-plan" },
                        OutputPath = "report/final-report.md",
                        Body = "Write the full report following the outline. Include evidence from research. Use clear, professional prose with markdown formatting."
                    },
                    new SeedTaskDefinition
                    {
                        Filename = "04-edit.md",
                        Id = "task-edit",
                        Title = "Edit and finalize the report",
                        Owner = "editor",
                        RequiredCapabilities = new[] { "copy_editing", "quality_assurance" },
                        Priority = "medium",
                        DependsOn = new[] { "task-write" },
                        OutputPath = "report/final-report.md",
                        Body = "Review and edit the report for clarity, accuracy, and style. Fix any issues and finalize the markdown."
                    },
                    new SeedTaskDefinition
                    {
                        Filename = "05-design.md",
                        Id = "task-design",
                        Title = "Create styled HTML deliverable",
                        Owner = "designer",
                        RequiredCapabilities = new[] { "html_rendering" },
                        Priority = "medium",
                        DependsOn = new[] { "task-edit" },
                        OutputPath = "deliverables/report.html",
                        Body = "Convert the final markdown report to a styled HTML deliverable using render_html tool. Ensure clean presentation."
                    }
                }
            };
        }
    }
}
