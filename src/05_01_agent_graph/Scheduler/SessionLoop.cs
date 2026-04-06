using System;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Core;
using FourthDevs.AgentGraph.Models;

namespace FourthDevs.AgentGraph.Scheduler
{
    public static class SessionLoop
    {
        private const int MaxRounds = 20;

        public static async Task ProcessSession(string sessionId, Runtime rt)
        {
            var graph = new GraphQueries(rt);
            int round = 0;

            // Recover stale in_progress tasks
            var stale = await rt.Tasks.Find(t => t.SessionId == sessionId && t.Status == "in_progress");
            foreach (var task in stale)
            {
                await rt.Tasks.Update(task.Id, t => t.Status = "todo");
                Log.Warn("Recovered stale task \"" + task.Title + "\" (in_progress → todo)");
            }

            while (round < MaxRounds)
            {
                round++;
                var ready = await graph.FindReadyTasks(sessionId);
                if (ready.Count == 0) break;

                Log.Round(round, ready.Count);

                foreach (var task in ready)
                {
                    try { await ProcessOneTask(task, rt); }
                    catch (Exception ex) { Log.TaskError("?", ex.Message); }
                }
            }
        }

        private static async Task ProcessOneTask(AgentTask task, Runtime rt)
        {
            var graph = new GraphQueries(rt);
            await rt.Tasks.Update(task.Id, t => t.Status = "in_progress");

            var actor = await graph.FindAssignedActor(task);
            if (actor == null)
            {
                await rt.Tasks.Update(task.Id, t =>
                {
                    t.Status = "blocked";
                    t.Recovery = new TaskRecoveryState
                    {
                        AutoRetry = false,
                        Attempts = task.Recovery != null ? task.Recovery.Attempts : 0,
                        LastFailureKind = "runtime_error",
                        LastFailureMessage = "No assigned actor",
                        LastFailureAt = DomainHelpers.Now(),
                    };
                });
                Log.Warn("No assigned actor for \"" + task.Title + "\"");
                return;
            }

            Log.Actor(actor.Name, task.Title);

            try
            {
                var result = await ActorRunner.RunActorTask(task, actor, rt, graph);

                // Accumulate usage onto session
                await AccumulateSessionUsage(task.SessionId, result.Usage, rt);

                if (result.Status == "completed")
                {
                    await rt.Tasks.Update(task.Id, t => { t.Status = "done"; t.Recovery = null; });
                    await graph.UnblockParents(task);
                    Log.TaskDone(actor.Name, result.Message);
                    await UpdateActorStatus(actor, rt);
                    return;
                }

                bool shouldWait = result.Status == "waiting"
                    || (result.Status == "blocked" && await graph.HasUnfinishedChildren(task));

                if (shouldWait)
                {
                    await rt.Tasks.Update(task.Id, t => { t.Status = "waiting"; t.Recovery = null; });
                    Log.TaskWaiting(actor.Name, result.Message);
                    return;
                }

                await rt.Tasks.Update(task.Id, t =>
                {
                    t.Status = "blocked";
                    t.Recovery = new TaskRecoveryState
                    {
                        AutoRetry = false,
                        Attempts = task.Recovery != null ? task.Recovery.Attempts : 0,
                        LastFailureKind = "explicit_block",
                        LastFailureMessage = result.Message,
                        LastFailureAt = DomainHelpers.Now(),
                    };
                });
                Log.TaskBlocked(actor.Name, result.Message);
                await UpdateActorStatus(actor, rt);
            }
            catch (RecoverableActorError ex)
            {
                var attempts = (task.Recovery != null ? task.Recovery.Attempts : 0) + 1;
                bool scheduled = attempts <= Recovery.MaxAutoRetryAttempts;
                string nextRetryAt = scheduled ? DateTime.UtcNow.AddMilliseconds(ex.RetryAfterMs).ToString("o") : null;

                await rt.Tasks.Update(task.Id, t =>
                {
                    t.Status = "blocked";
                    t.Recovery = new TaskRecoveryState
                    {
                        AutoRetry = scheduled,
                        Attempts = attempts,
                        LastFailureKind = "llm_transient",
                        LastFailureMessage = ex.Message,
                        LastFailureAt = DomainHelpers.Now(),
                        NextRetryAt = nextRetryAt,
                    };
                });
                var retryMsg = scheduled && nextRetryAt != null
                    ? string.Format("{0}. Auto-retry {1}/{2} scheduled for {3}.", ex.Message, attempts, Recovery.MaxAutoRetryAttempts, nextRetryAt)
                    : string.Format("{0}. Auto-retry limit reached after {1} attempts.", ex.Message, attempts);
                Log.TaskBlocked(actor.Name, retryMsg);
            }
            catch (Exception ex)
            {
                await rt.Tasks.Update(task.Id, t =>
                {
                    t.Status = "blocked";
                    t.Recovery = new TaskRecoveryState
                    {
                        AutoRetry = false,
                        Attempts = task.Recovery != null ? task.Recovery.Attempts : 0,
                        LastFailureKind = "runtime_error",
                        LastFailureMessage = ex.Message,
                        LastFailureAt = DomainHelpers.Now(),
                    };
                });
                Log.TaskError(actor.Name, ex.Message);
            }
        }

        private static async Task UpdateActorStatus(Actor actor, Runtime rt)
        {
            var ownedTasks = await rt.Tasks.Find(t => t.SessionId == actor.SessionId && t.OwnerActorId == actor.Id);
            bool hasActive = ownedTasks.Any(t => t.Status != "done" && t.Status != "blocked");
            string next = hasActive ? "active" : "idle";
            if (actor.Status != next) await rt.Actors.Update(actor.Id, a => a.Status = next);
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
