using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Wonderlands.Core;
using FourthDevs.Wonderlands.Models;

namespace FourthDevs.Wonderlands.Scheduling
{
    public sealed class ReadinessEngine
    {
        private readonly Runtime _rt;

        public ReadinessEngine(Runtime rt) { _rt = rt; }

        public async Task<bool> AreDependenciesMet(Job job)
        {
            var deps = await _rt.Relations.Find(r =>
                r.FromKind == "job" && r.FromId == job.Id && r.RelationType == "depends_on");
            foreach (var dep in deps)
            {
                var depJob = await _rt.Jobs.GetById(dep.ToId);
                if (depJob == null || depJob.Status != "done") return false;
            }
            return true;
        }

        public async Task<bool> HasUnfinishedChildren(Job job)
        {
            var children = await _rt.Jobs.Find(j => j.ParentJobId == job.Id);
            return children.Any(j => j.Status != "done" && j.Status != "blocked");
        }

        public async Task<List<Job>> ListDueDecisions(string sessionId)
        {
            var candidates = await _rt.Jobs.Find(j =>
                j.SessionId == sessionId && (j.Status == "pending" || j.Status == "waiting" || j.Status == "blocked"));

            var ready = new List<Job>();
            foreach (var job in candidates)
            {
                if (job.Status == "pending")
                {
                    if (!await AreDependenciesMet(job)) continue;
                    await _rt.Jobs.Update(job.Id, j => j.Status = "ready");
                    ready.Add(job);
                    continue;
                }
                if (job.Status == "waiting")
                {
                    if (await AreDependenciesMet(job) && !await HasUnfinishedChildren(job))
                    {
                        await _rt.Jobs.Update(job.Id, j => j.Status = "ready");
                        ready.Add(job);
                    }
                    continue;
                }
                if (job.Status == "blocked")
                {
                    var latestRun = await GetLatestRun(job.Id);
                    if (latestRun != null && Recovery.ShouldAutoRetryRun(latestRun))
                    {
                        await _rt.Jobs.Update(job.Id, j => j.Status = "ready");
                        ready.Add(job);
                    }
                }
            }
            return ready.OrderBy(j => j.Priority).ToList();
        }

        public async Task UnblockParents(Job completedJob)
        {
            if (string.IsNullOrEmpty(completedJob.ParentJobId)) return;
            var parent = await _rt.Jobs.GetById(completedJob.ParentJobId);
            if (parent == null || parent.Status != "waiting") return;
            if (await HasUnfinishedChildren(parent)) return;
            if (!await AreDependenciesMet(parent)) return;
            await _rt.Jobs.Update(parent.Id, j => j.Status = "ready");
        }

        public async Task<List<Artifact>> GetArtifactsProducedByJob(string jobId)
        {
            var rels = await _rt.Relations.Find(r =>
                r.FromKind == "job" && r.FromId == jobId
                && r.RelationType == "produces" && r.ToKind == "artifact");
            var artifacts = new List<Artifact>();
            foreach (var r in rels)
            {
                var a = await _rt.Artifacts.GetById(r.ToId);
                if (a != null) artifacts.Add(a);
            }
            return LatestArtifacts(artifacts);
        }

        public async Task<List<Artifact>> GetDependencyArtifacts(Job job)
        {
            var deps = await _rt.Relations.Find(r =>
                r.FromKind == "job" && r.FromId == job.Id && r.RelationType == "depends_on");
            var all = new List<Artifact>();
            foreach (var dep in deps)
            {
                var arts = await GetArtifactsProducedByJob(dep.ToId);
                all.AddRange(arts);
            }
            return LatestArtifacts(all);
        }

        public Task<List<Item>> GetRunItems(string runId)
        {
            return _rt.Items.Find(i => i.RunId == runId);
        }

        public Task<List<Job>> GetSessionJobs(string sessionId)
        {
            return _rt.Jobs.Find(j => j.SessionId == sessionId);
        }

        public async Task<Run> GetLatestRun(string jobId)
        {
            var runs = await _rt.Runs.Find(r => r.JobId == jobId);
            return runs.OrderByDescending(r => r.CreatedAt).FirstOrDefault();
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
