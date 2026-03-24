using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FourthDevs.Events.Config;
using FourthDevs.Events.Helpers;
using FourthDevs.Events.Models;

namespace FourthDevs.Events.Features
{
    /// <summary>
    /// Task management: CRUD operations on markdown task files with YAML frontmatter.
    /// </summary>
    internal static class TaskManager
    {
        private static readonly Dictionary<string, int> PriorityOrder = new Dictionary<string, int>
        {
            { TaskPriority.Critical, 0 },
            { TaskPriority.High, 1 },
            { TaskPriority.Medium, 2 },
            { TaskPriority.Low, 3 }
        };

        public static string TasksDir { get { return EnvConfig.TasksPath; } }

        public static List<TaskRecord> ListTasks()
        {
            string dir = TasksDir;
            if (!Directory.Exists(dir)) return new List<TaskRecord>();

            var tasks = new List<TaskRecord>();
            foreach (string file in Directory.GetFiles(dir, "*.md"))
            {
                var task = ReadTask(file);
                if (task != null) tasks.Add(task);
            }

            tasks.Sort((a, b) => string.Compare(a.Slug, b.Slug, StringComparison.OrdinalIgnoreCase));
            return tasks;
        }

        public static TaskRecord ReadTask(string path)
        {
            if (!File.Exists(path)) return null;
            string content = File.ReadAllText(path);
            var parsed = FrontmatterParser.Parse(content);
            var fm = FrontmatterParser.ToTaskFrontmatter(parsed.Fields);
            string slug = Path.GetFileNameWithoutExtension(path);
            return new TaskRecord
            {
                Path = path,
                Slug = slug,
                Frontmatter = fm,
                Body = parsed.Body.Trim()
            };
        }

        public static TaskRecord CreateTask(CreateTaskInput input, string workflowId)
        {
            Directory.CreateDirectory(TasksDir);
            string now = DateTime.UtcNow.ToString("o");
            string slug = PathHelper.Slugify(input.Id ?? input.Title ?? "task");
            string filename = slug + ".md";
            string path = System.IO.Path.Combine(TasksDir, filename);

            var fm = new TaskFrontmatter
            {
                Id = input.Id ?? slug,
                Title = input.Title ?? slug,
                Status = TaskStatus.Open,
                Priority = input.Priority ?? TaskPriority.Medium,
                AssignedTo = input.Agent,
                DependsOn = input.DependsOn ?? new List<string>(),
                Capabilities = input.Capabilities ?? new List<string>(),
                CreatedAt = now,
                UpdatedAt = now,
                OutputFile = input.OutputFile,
                OutputType = input.OutputType,
                Phase = input.Phase,
                Order = input.Order,
                WorkflowId = workflowId,
                Agent = input.Agent,
                MaxAttempts = 3
            };

            string content = FrontmatterParser.SerializeTask(fm, input.Body ?? "");
            File.WriteAllText(path, content);

            return new TaskRecord
            {
                Path = path,
                Slug = slug,
                Frontmatter = fm,
                Body = input.Body ?? ""
            };
        }

        public static TaskRecord FindTaskById(string taskId)
        {
            var tasks = ListTasks();
            return tasks.FirstOrDefault(t =>
                string.Equals(t.Frontmatter.Id, taskId, StringComparison.OrdinalIgnoreCase));
        }

        public static TaskRecord ClaimNextTask(string owner, string runId, List<string> capabilities)
        {
            var tasks = ListTasks();
            var statusMap = new Dictionary<string, string>();
            foreach (var t in tasks)
                statusMap[t.Frontmatter.Id ?? t.Slug] = t.Frontmatter.Status;

            var candidates = tasks
                .Where(t => t.Frontmatter.Status == TaskStatus.Open)
                .Where(t => HasNoPendingDeps(t, statusMap))
                .Where(t => IsEligible(t, owner, capabilities))
                .OrderBy(t => GetPriorityScore(t.Frontmatter.Priority))
                .ThenBy(t => t.Frontmatter.CreatedAt ?? "")
                .ToList();

            foreach (var c in candidates)
            {
                var fresh = ReadTask(c.Path);
                if (fresh == null || fresh.Frontmatter.Status != TaskStatus.Open) continue;

                fresh.Frontmatter.Status = TaskStatus.InProgress;
                fresh.Frontmatter.AssignedTo = owner;
                fresh.Frontmatter.RunId = runId;
                fresh.Frontmatter.StartedAt = DateTime.UtcNow.ToString("o");
                fresh.Frontmatter.UpdatedAt = DateTime.UtcNow.ToString("o");
                fresh.Frontmatter.BlockedReason = null;
                WriteTask(fresh);
                return fresh;
            }

            return null;
        }

        public static void MarkTaskCompleted(TaskRecord task, string note)
        {
            string now = DateTime.UtcNow.ToString("o");
            task.Frontmatter.Status = TaskStatus.Done;
            task.Frontmatter.CompletedAt = now;
            task.Frontmatter.UpdatedAt = now;
            task.Frontmatter.WaitId = null;
            task.Frontmatter.WaitQuestion = null;
            task.Frontmatter.BlockedReason = null;
            task.Frontmatter.ErrorMessage = null;
            WriteTask(task);
        }

        public static void MarkTaskBlocked(TaskRecord task, string reason)
        {
            task.Frontmatter.Status = TaskStatus.Blocked;
            task.Frontmatter.BlockedReason = reason;
            task.Frontmatter.Attempts++;
            task.Frontmatter.UpdatedAt = DateTime.UtcNow.ToString("o");
            task.Frontmatter.AssignedTo = null;
            task.Frontmatter.RunId = null;
            WriteTask(task);
        }

        public static void MarkTaskWaitingHuman(TaskRecord task, string waitId, string question)
        {
            task.Frontmatter.Status = TaskStatus.WaitingHuman;
            task.Frontmatter.WaitId = waitId;
            task.Frontmatter.WaitQuestion = question;
            task.Frontmatter.UpdatedAt = DateTime.UtcNow.ToString("o");
            WriteTask(task);
        }

        public static void ReopenTaskWithAnswer(TaskRecord task, string answer)
        {
            task.Frontmatter.Status = TaskStatus.Open;
            task.Frontmatter.WaitAnswer = answer;
            task.Frontmatter.WaitId = null;
            task.Frontmatter.WaitQuestion = null;
            task.Frontmatter.BlockedReason = null;
            task.Frontmatter.AssignedTo = null;
            task.Frontmatter.RunId = null;
            task.Frontmatter.UpdatedAt = DateTime.UtcNow.ToString("o");
            WriteTask(task);
        }

        public static List<DependencyChange> ReconcileDependencyStates()
        {
            var tasks = ListTasks();
            var statusMap = new Dictionary<string, string>();
            foreach (var t in tasks)
                statusMap[t.Frontmatter.Id ?? t.Slug] = t.Frontmatter.Status;

            var changes = new List<DependencyChange>();

            foreach (var task in tasks)
            {
                if (task.Frontmatter.Status == TaskStatus.Done ||
                    task.Frontmatter.Status == TaskStatus.WaitingHuman)
                    continue;

                if (task.Frontmatter.DependsOn == null || task.Frontmatter.DependsOn.Count == 0)
                {
                    // Retry blocked (non-dep) tasks
                    if (task.Frontmatter.Status == TaskStatus.Blocked &&
                        task.Frontmatter.BlockedReason != "dependencies" &&
                        task.Frontmatter.Attempts < task.Frontmatter.MaxAttempts)
                    {
                        task.Frontmatter.Status = TaskStatus.Open;
                        task.Frontmatter.BlockedReason = null;
                        task.Frontmatter.UpdatedAt = DateTime.UtcNow.ToString("o");
                        WriteTask(task);
                        changes.Add(new DependencyChange { Task = task, Became = "unblocked", PendingDeps = new List<string>() });
                    }
                    continue;
                }

                var pending = PendingDeps(task, statusMap);

                if (pending.Count > 0 && task.Frontmatter.Status == TaskStatus.Open)
                {
                    task.Frontmatter.Status = TaskStatus.Blocked;
                    task.Frontmatter.BlockedReason = "dependencies";
                    task.Frontmatter.UpdatedAt = DateTime.UtcNow.ToString("o");
                    WriteTask(task);
                    changes.Add(new DependencyChange { Task = task, Became = "blocked", PendingDeps = pending });
                }
                else if (pending.Count == 0 && task.Frontmatter.Status == TaskStatus.Blocked &&
                         task.Frontmatter.BlockedReason == "dependencies")
                {
                    task.Frontmatter.Status = TaskStatus.Open;
                    task.Frontmatter.BlockedReason = null;
                    task.Frontmatter.UpdatedAt = DateTime.UtcNow.ToString("o");
                    WriteTask(task);
                    changes.Add(new DependencyChange { Task = task, Became = "unblocked", PendingDeps = new List<string>() });
                }
                else if (task.Frontmatter.Status == TaskStatus.Blocked &&
                         task.Frontmatter.BlockedReason != "dependencies" &&
                         task.Frontmatter.Attempts < task.Frontmatter.MaxAttempts)
                {
                    task.Frontmatter.Status = TaskStatus.Open;
                    task.Frontmatter.BlockedReason = null;
                    task.Frontmatter.UpdatedAt = DateTime.UtcNow.ToString("o");
                    WriteTask(task);
                    changes.Add(new DependencyChange { Task = task, Became = "unblocked", PendingDeps = new List<string>() });
                }
            }

            return changes;
        }

        public static bool AllTasksCompleted()
        {
            var tasks = ListTasks();
            return tasks.Count > 0 && tasks.All(t => t.Frontmatter.Status == TaskStatus.Done);
        }

        public static Dictionary<string, int> CountByStatus()
        {
            var counts = new Dictionary<string, int>
            {
                { TaskStatus.Open, 0 },
                { TaskStatus.InProgress, 0 },
                { TaskStatus.Blocked, 0 },
                { TaskStatus.WaitingHuman, 0 },
                { TaskStatus.Done, 0 }
            };

            foreach (var t in ListTasks())
            {
                string s = t.Frontmatter.Status ?? TaskStatus.Open;
                if (counts.ContainsKey(s)) counts[s]++;
            }
            return counts;
        }

        public static List<TaskRecord> ListWaitingHuman()
        {
            return ListTasks().Where(t => t.Frontmatter.Status == TaskStatus.WaitingHuman).ToList();
        }

        private static void WriteTask(TaskRecord task)
        {
            string content = FrontmatterParser.SerializeTask(task.Frontmatter, task.Body);
            File.WriteAllText(task.Path, content);
        }

        private static List<string> PendingDeps(TaskRecord task, Dictionary<string, string> statusMap)
        {
            var pending = new List<string>();
            foreach (string dep in task.Frontmatter.DependsOn)
            {
                string s;
                if (!statusMap.TryGetValue(dep, out s) || s != TaskStatus.Done)
                    pending.Add(dep);
            }
            return pending;
        }

        private static bool HasNoPendingDeps(TaskRecord task, Dictionary<string, string> statusMap)
        {
            return PendingDeps(task, statusMap).Count == 0;
        }

        private static bool IsEligible(TaskRecord task, string owner, List<string> capabilities)
        {
            if (task.Frontmatter.Capabilities != null && task.Frontmatter.Capabilities.Count > 0)
            {
                var capSet = new HashSet<string>(capabilities ?? new List<string>());
                return task.Frontmatter.Capabilities.All(c => capSet.Contains(c));
            }
            return task.Frontmatter.Agent == owner;
        }

        private static int GetPriorityScore(string priority)
        {
            int score;
            if (priority != null && PriorityOrder.TryGetValue(priority, out score))
                return score;
            return 2;
        }
    }

    public class DependencyChange
    {
        public TaskRecord Task { get; set; }
        public string Became { get; set; }
        public List<string> PendingDeps { get; set; }
    }
}
