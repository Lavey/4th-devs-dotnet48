using System;
using System.Collections.Generic;
using System.IO;
using FourthDevs.Events.Config;
using FourthDevs.Events.Core;
using FourthDevs.Events.Features;
using FourthDevs.Events.Helpers;
using FourthDevs.Events.Models;
using FourthDevs.Events.Workflows;

namespace FourthDevs.Events
{
    /// <summary>
    /// Workspace initialization: create directories and seed tasks from workflow.
    /// </summary>
    internal static class Bootstrap
    {
        public static void EnsureWorkspace(WorkflowDefinition workflow)
        {
            string projectDir = EnvConfig.ProjectPath;
            string tasksDir = EnvConfig.TasksPath;

            // Create directory structure
            string[] dirs = new[]
            {
                projectDir,
                tasksDir,
                Path.Combine(projectDir, "notes"),
                Path.Combine(projectDir, "notes", "web"),
                Path.Combine(projectDir, "work"),
                Path.Combine(projectDir, "assets"),
                Path.Combine(projectDir, "report"),
                Path.Combine(projectDir, "deliverables"),
                Path.Combine(projectDir, "system"),
                Path.Combine(projectDir, "system", "events"),
                Path.Combine(projectDir, "system", "memory"),
                Path.Combine(projectDir, "system", "waits"),
            };

            foreach (string d in dirs)
                Directory.CreateDirectory(d);

            // Create project.md if not exists
            string projectMd = Path.Combine(projectDir, "project.md");
            if (!File.Exists(projectMd))
            {
                var fields = new Dictionary<string, string>
                {
                    { "id", workflow.Project.Id },
                    { "title", workflow.Project.Title },
                    { "status", "active" },
                    { "workflow_id", workflow.Id },
                    { "created_at", DateTime.UtcNow.ToString("o") }
                };
                string content = FrontmatterParser.Serialize(fields) + "\n" + workflow.Project.Description;
                File.WriteAllText(projectMd, content);
            }

            // Copy goal.md to project dir if exists
            string goalSrc = EnvConfig.GoalPath;
            string goalDst = Path.Combine(projectDir, "goal.md");
            if (File.Exists(goalSrc) && !File.Exists(goalDst))
            {
                File.Copy(goalSrc, goalDst);
            }

            // Seed tasks from workflow definition
            SeedTasks(workflow);

            Logger.Info("bootstrap", "Workspace initialized for workflow '" + workflow.Id + "' with " +
                        (workflow.Tasks?.Length ?? 0) + " task(s).");
        }

        private static void SeedTasks(WorkflowDefinition workflow)
        {
            if (workflow.Tasks == null) return;

            // Don't re-seed if tasks already exist
            var existing = TaskManager.ListTasks();
            if (existing.Count > 0) return;

            int order = 0;
            foreach (var seed in workflow.Tasks)
            {
                order++;
                var input = new CreateTaskInput
                {
                    Id = seed.Id,
                    Title = seed.Title,
                    Body = seed.Body ?? "",
                    Priority = seed.Priority ?? TaskPriority.Medium,
                    DependsOn = seed.DependsOn != null ? new List<string>(seed.DependsOn) : new List<string>(),
                    Capabilities = seed.RequiredCapabilities != null ? new List<string>(seed.RequiredCapabilities) : new List<string>(),
                    Agent = seed.Owner,
                    OutputFile = seed.OutputPath,
                    Order = order,
                    Phase = "main"
                };

                TaskManager.CreateTask(input, workflow.Id);
            }
        }
    }
}
