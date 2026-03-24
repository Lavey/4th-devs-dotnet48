using System;
using FourthDevs.Events.Core;
using FourthDevs.Events.Workflows;

namespace FourthDevs.Events.Autonomy
{
    /// <summary>
    /// Resolves the workflow to use: reads goal contract and resolves to a workflow definition.
    /// </summary>
    internal static class AutonomyRuntime
    {
        public static AutonomyResolution Resolve(string workflowId)
        {
            try
            {
                var goal = Contract.ReadGoalContract();
                if (goal != null)
                {
                    Logger.Info("autonomy", "Goal contract loaded: " + goal.Objective);

                    var workflow = WorkflowRegistry.ResolveWorkflow(workflowId);

                    return new AutonomyResolution
                    {
                        Mode = "autonomous",
                        Workflow = workflow,
                        Goal = goal,
                        Context = new AutonomyContext
                        {
                            Goal = goal,
                            PlanVersion = 1,
                            RemainingReplanBudget = goal.ReplanBudget
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Error("autonomy", "Failed to read goal: " + ex.Message);
            }

            var staticWorkflow = WorkflowRegistry.ResolveWorkflow(workflowId);
            return new AutonomyResolution
            {
                Mode = "static",
                Workflow = staticWorkflow
            };
        }
    }
}
