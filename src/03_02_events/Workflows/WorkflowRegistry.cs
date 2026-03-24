using System;
using System.Collections.Generic;

namespace FourthDevs.Events.Workflows
{
    public static class WorkflowRegistry
    {
        public static readonly string DefaultWorkflowId = "report-v1";

        private static readonly Dictionary<string, WorkflowDefinition> Workflows =
            new Dictionary<string, WorkflowDefinition>(StringComparer.OrdinalIgnoreCase);

        static WorkflowRegistry()
        {
            var v1 = ReportV1Workflow.Create();
            var v2 = ReportV2Workflow.Create();
            Workflows[v1.Id] = v1;
            Workflows[v2.Id] = v2;
        }

        public static WorkflowDefinition ResolveWorkflow(string workflowId = null)
        {
            var id = string.IsNullOrWhiteSpace(workflowId) ? DefaultWorkflowId : workflowId.Trim();

            WorkflowDefinition workflow;
            if (Workflows.TryGetValue(id, out workflow))
            {
                return workflow;
            }

            throw new InvalidOperationException(
                $"Unknown workflow \"{id}\". Available: {string.Join(", ", Workflows.Keys)}");
        }

        public static IEnumerable<string> ListWorkflows()
        {
            return Workflows.Keys;
        }
    }
}
