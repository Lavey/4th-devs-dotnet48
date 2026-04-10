using System;
using System.Threading.Tasks;
using FourthDevs.Wonderlands.Core;
using FourthDevs.Wonderlands.Models;

namespace FourthDevs.Wonderlands.Scheduling
{
    public static class WorkerLoop
    {
        private const int MaxRounds = 20;

        public static async Task ProcessSession(string sessionId, Runtime rt)
        {
            var engine = new ReadinessEngine(rt);
            int round = 0;

            while (round < MaxRounds)
            {
                round++;
                var readyJobs = await engine.ListDueDecisions(sessionId);
                if (readyJobs.Count == 0) break;

                Log.Round(round, readyJobs.Count);

                foreach (var job in readyJobs)
                {
                    try { await ProcessOneJob(job, rt, engine); }
                    catch (Exception ex) { Log.JobError("?", ex.Message); }
                }
            }
        }

        private static async Task ProcessOneJob(Job job, Runtime rt, ReadinessEngine engine)
        {
            await rt.Jobs.Update(job.Id, j => j.Status = "running");

            var agentName = job.AgentName ?? "orchestrator";
            Log.Actor(agentName, job.Title);

            var run = await rt.Runs.Add(new Run
            {
                Id = DomainHelpers.NewId(),
                SessionId = job.SessionId,
                JobId = job.Id,
                RootRunId = null,
                AgentName = agentName,
                Status = "running",
                TurnCount = 0,
                Memory = new MemoryState(),
                CreatedAt = DomainHelpers.Now(),
                UpdatedAt = DomainHelpers.Now(),
            });

            // If this is a child job, link root run
            if (!string.IsNullOrEmpty(job.ParentJobId))
            {
                var parentRun = await engine.GetLatestRun(job.ParentJobId);
                if (parentRun != null)
                    await rt.Runs.Update(run.Id, r => r.RootRunId = parentRun.RootRunId ?? parentRun.Id);
            }

            try
            {
                var result = await RunExecution.DriveRun(job, run, rt, engine);

                await AccumulateSessionUsage(job.SessionId, result.Usage, rt);

                if (result.Status == "completed")
                {
                    await rt.Runs.Update(run.Id, r => { r.Status = "completed"; r.UpdatedAt = DomainHelpers.Now(); });
                    await rt.Jobs.Update(job.Id, j => j.Status = "done");
                    await engine.UnblockParents(job);
                    Log.JobDone(agentName, result.Message);
                    return;
                }

                bool shouldSuspend = result.Status == "suspended"
                    || (result.Status == "blocked" && await engine.HasUnfinishedChildren(job));

                if (shouldSuspend)
                {
                    await rt.Runs.Update(run.Id, r => { r.Status = "suspended"; r.UpdatedAt = DomainHelpers.Now(); });
                    await rt.Jobs.Update(job.Id, j => j.Status = "waiting");
                    Log.JobWaiting(agentName, result.Message);
                    return;
                }

                await rt.Runs.Update(run.Id, r =>
                {
                    r.Status = "failed";
                    r.Recovery = new RunRecoveryState
                    {
                        AutoRetry = false,
                        Attempts = 0,
                        LastFailureKind = "explicit_block",
                        LastFailureMessage = result.Message,
                        LastFailureAt = DomainHelpers.Now(),
                    };
                    r.UpdatedAt = DomainHelpers.Now();
                });
                await rt.Jobs.Update(job.Id, j => j.Status = "blocked");
                Log.JobBlocked(agentName, result.Message);
            }
            catch (RecoverableRunError ex)
            {
                var prevRuns = await rt.Runs.Find(r => r.JobId == job.Id);
                var attempts = prevRuns.Count;
                bool scheduled = attempts <= Recovery.MaxAutoRetryAttempts;
                string nextRetryAt = scheduled ? DateTime.UtcNow.AddMilliseconds(ex.RetryAfterMs).ToString("o") : null;

                await rt.Runs.Update(run.Id, r =>
                {
                    r.Status = "failed";
                    r.Recovery = new RunRecoveryState
                    {
                        AutoRetry = scheduled,
                        Attempts = attempts,
                        LastFailureKind = "llm_transient",
                        LastFailureMessage = ex.Message,
                        LastFailureAt = DomainHelpers.Now(),
                        NextRetryAt = nextRetryAt,
                    };
                    r.UpdatedAt = DomainHelpers.Now();
                });
                await rt.Jobs.Update(job.Id, j => j.Status = "blocked");

                var retryMsg = scheduled && nextRetryAt != null
                    ? string.Format("{0}. Auto-retry {1}/{2} scheduled.", ex.Message, attempts, Recovery.MaxAutoRetryAttempts)
                    : string.Format("{0}. Auto-retry limit reached after {1} attempts.", ex.Message, attempts);
                Log.JobBlocked(agentName, retryMsg);
            }
            catch (Exception ex)
            {
                await rt.Runs.Update(run.Id, r =>
                {
                    r.Status = "failed";
                    r.Recovery = new RunRecoveryState
                    {
                        AutoRetry = false,
                        Attempts = 0,
                        LastFailureKind = "runtime_error",
                        LastFailureMessage = ex.Message,
                        LastFailureAt = DomainHelpers.Now(),
                    };
                    r.UpdatedAt = DomainHelpers.Now();
                });
                await rt.Jobs.Update(job.Id, j => j.Status = "blocked");
                Log.JobError(agentName, ex.Message);
            }
        }

        private static async Task AccumulateSessionUsage(string sessionId, TokenUsage usage, Runtime rt)
        {
            if (usage == null || usage.TotalTokens == 0) return;
            var session = await rt.Sessions.GetById(sessionId);
            if (session == null) return;
            await rt.Sessions.Update(sessionId, s => s.Usage = TokenUsage.Add(s.Usage ?? TokenUsage.Empty(), usage));
        }
    }
}
