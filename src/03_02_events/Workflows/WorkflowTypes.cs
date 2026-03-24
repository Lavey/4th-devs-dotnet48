using FourthDevs.Events.Models;

namespace FourthDevs.Events.Workflows
{
    public class WorkflowProjectMetadata
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string DeliverablePath { get; set; }
        public string GoalId { get; set; }
        public int? PlanVersion { get; set; }
    }

    public class SeedTaskDefinition
    {
        public string Filename { get; set; }
        public string Id { get; set; }
        public string Title { get; set; }
        public string Owner { get; set; }
        public string[] RequiredCapabilities { get; set; }
        public string Priority { get; set; }
        public string[] DependsOn { get; set; }
        public string OutputPath { get; set; }
        public string Body { get; set; }
        public string GoalId { get; set; }
        public int? PlanVersion { get; set; }
    }

    public class WorkflowDefinition
    {
        public string Id { get; set; }
        public WorkflowProjectMetadata Project { get; set; }
        public string[] AgentOrder { get; set; }
        public SeedTaskDefinition[] Tasks { get; set; }
    }
}
