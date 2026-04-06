using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Core;
using FourthDevs.AgentGraph.Models;

namespace FourthDevs.AgentGraph.Scheduler
{
    public sealed class GraphQueries
    {
        private readonly Runtime _rt;

        public GraphQueries(Runtime rt) { _rt = rt; }

        public async Task<bool> AreDependenciesMet(AgentTask task)
        {
            var deps = await _rt.Relations.Find(r =>
                r.FromKind == "task" && r.FromId == task.Id && r.RelationType == "depends_on");
            foreach (var dep in deps)
            {
                var depTask = await _rt.Tasks.GetById(dep.ToId);
                if (depTask == null || depTask.Status != "done") return false;
            }
            return true;
        }

        public async Task<bool> HasUnfinishedChildren(AgentTask task)
        {
            var children = await _rt.Tasks.Find(t => t.ParentTaskId == task.Id);
            return children.Any(t => t.Status != "done" && t.Status != "blocked");
        }

        public async Task<List<AgentTask>> FindReadyTasks(string sessionId)
        {
            var candidates = await _rt.Tasks.Find(t =>
                t.SessionId == sessionId && (t.Status == "todo" || t.Status == "waiting" || t.Status == "blocked"));

            var ready = new List<AgentTask>();
            foreach (var task in candidates)
            {
                if (task.Status == "todo")
                {
                    if (!await AreDependenciesMet(task)) continue;
                    ready.Add(task);
                    continue;
                }
                if (task.Status == "waiting")
                {
                    if (await AreDependenciesMet(task) && !await HasUnfinishedChildren(task))
                    {
                        await _rt.Tasks.Update(task.Id, t => t.Status = "todo");
                        ready.Add(task);
                    }
                    continue;
                }
                if (task.Status == "blocked" && Recovery.ShouldAutoRetryTask(task))
                {
                    await _rt.Tasks.Update(task.Id, t => t.Status = "todo");
                    ready.Add(task);
                }
            }
            return ready.OrderBy(t => t.Priority).ToList();
        }

        public async Task UnblockParents(AgentTask completedTask)
        {
            if (string.IsNullOrEmpty(completedTask.ParentTaskId)) return;
            var parent = await _rt.Tasks.GetById(completedTask.ParentTaskId);
            if (parent == null || parent.Status != "waiting") return;
            if (await HasUnfinishedChildren(parent)) return;
            if (!await AreDependenciesMet(parent)) return;
            await _rt.Tasks.Update(parent.Id, t => t.Status = "todo");
        }

        public async Task<Actor> FindAssignedActor(AgentTask task)
        {
            var rels = await _rt.Relations.Find(r =>
                r.FromKind == "task" && r.FromId == task.Id && r.RelationType == "assigned_to");
            if (rels.Count == 0) return null;
            return await _rt.Actors.GetById(rels[0].ToId);
        }

        public async Task<List<AgentTask>> GetDependencyTasks(AgentTask task)
        {
            var rels = await _rt.Relations.Find(r =>
                r.FromKind == "task" && r.FromId == task.Id && r.RelationType == "depends_on");
            var tasks = new List<AgentTask>();
            foreach (var r in rels)
            {
                var t = await _rt.Tasks.GetById(r.ToId);
                if (t != null) tasks.Add(t);
            }
            return tasks;
        }

        public async Task<List<Artifact>> GetArtifactsProducedByTask(string taskId)
        {
            var rels = await _rt.Relations.Find(r =>
                r.FromKind == "task" && r.FromId == taskId
                && r.RelationType == "produces" && r.ToKind == "artifact");
            var artifacts = new List<Artifact>();
            foreach (var r in rels)
            {
                var a = await _rt.Artifacts.GetById(r.ToId);
                if (a != null) artifacts.Add(a);
            }
            return LatestArtifacts(artifacts);
        }

        public async Task<List<Artifact>> GetDependencyArtifacts(AgentTask task)
        {
            var depTasks = await GetDependencyTasks(task);
            var all = new List<Artifact>();
            foreach (var t in depTasks)
            {
                var arts = await GetArtifactsProducedByTask(t.Id);
                all.AddRange(arts);
            }
            return LatestArtifacts(all);
        }

        public Task<List<Item>> GetTaskItems(string taskId)
        {
            return _rt.Items.Find(i => i.TaskId == taskId);
        }

        public Task<List<Actor>> GetSessionActors(string sessionId)
        {
            return _rt.Actors.Find(a => a.SessionId == sessionId);
        }

        public Task<List<AgentTask>> GetSessionTasks(string sessionId)
        {
            return _rt.Tasks.Find(t => t.SessionId == sessionId);
        }

        private static List<Artifact> LatestArtifacts(List<Artifact> artifacts)
        {
            var byPath = new Dictionary<string, Artifact>();
            foreach (var a in artifacts.OrderByDescending(x => x.Version).ThenByDescending(x => x.CreatedAt))
            {
                if (!byPath.ContainsKey(a.Path)) byPath[a.Path] = a;
            }
            return byPath.Values.OrderBy(a => a.Path).ToList();
        }
    }
}
