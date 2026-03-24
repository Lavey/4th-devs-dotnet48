using System.Collections.Generic;

namespace FourthDevs.Events.Autonomy
{
    public class GoalContract
    {
        public string Id { get; set; }
        public string Objective { get; set; }
        public string Context { get; set; }
        public List<string> MustHave { get; set; } = new List<string>();
        public List<string> Forbidden { get; set; } = new List<string>();
        public int StepBudgetRounds { get; set; } = 12;
        public int ReplanBudget { get; set; } = 2;
        public int MaxTotalTasks { get; set; } = 16;
    }

    public class AutonomyContext
    {
        public GoalContract Goal { get; set; }
        public int PlanVersion { get; set; } = 1;
        public int RemainingReplanBudget { get; set; }
    }

    public class AutonomyResolution
    {
        public string Mode { get; set; }
        public Workflows.WorkflowDefinition Workflow { get; set; }
        public GoalContract Goal { get; set; }
        public AutonomyContext Context { get; set; }
        public string NoGoMessage { get; set; }
    }
}
