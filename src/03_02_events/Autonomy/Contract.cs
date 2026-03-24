using System;
using System.IO;
using FourthDevs.Events.Config;
using FourthDevs.Events.Helpers;

namespace FourthDevs.Events.Autonomy
{
    /// <summary>
    /// Reads goal contract from workspace/goal.md with YAML frontmatter.
    /// </summary>
    internal static class Contract
    {
        public static GoalContract ReadGoalContract(string goalPath = null)
        {
            string path = goalPath ?? EnvConfig.GoalPath;
            if (!File.Exists(path)) return null;

            string content = File.ReadAllText(path);
            var parsed = FrontmatterParser.Parse(content);
            var fields = parsed.Fields;

            string objective;
            fields.TryGetValue("objective", out objective);
            if (string.IsNullOrWhiteSpace(objective))
                objective = parsed.Body.Trim();

            if (string.IsNullOrWhiteSpace(objective))
                return null;

            string id;
            fields.TryGetValue("id", out id);
            if (string.IsNullOrWhiteSpace(id))
                id = "goal-" + DateTime.UtcNow.ToString("yyyy-MM-dd");

            var contract = new GoalContract
            {
                Id = id,
                Objective = objective,
                Context = parsed.Body.Trim()
            };

            string mustHave;
            if (fields.TryGetValue("must_have", out mustHave))
                contract.MustHave = FrontmatterParser.ParseList(mustHave);

            string forbidden;
            if (fields.TryGetValue("forbidden", out forbidden))
                contract.Forbidden = FrontmatterParser.ParseList(forbidden);

            string stepBudget;
            if (fields.TryGetValue("step_budget_rounds", out stepBudget))
                contract.StepBudgetRounds = EnvConfig.ParsePositiveInt(stepBudget, 12);

            string replanBudget;
            if (fields.TryGetValue("replan_budget", out replanBudget))
                contract.ReplanBudget = EnvConfig.ParsePositiveInt(replanBudget, 2);

            string maxTasks;
            if (fields.TryGetValue("max_total_tasks", out maxTasks))
                contract.MaxTotalTasks = EnvConfig.ParsePositiveInt(maxTasks, 16);

            return contract;
        }
    }
}
