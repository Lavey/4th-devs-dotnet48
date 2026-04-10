using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Wonderlands.Agents;
using FourthDevs.Wonderlands.Core;
using FourthDevs.Wonderlands.Models;
using FourthDevs.Wonderlands.Scheduling;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Wonderlands
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var userMessage = args.Length > 0
                    ? string.Join(" ", args)
                    : "Write a comprehensive blog post about TypeScript 5.0 features";

                var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".data");
                if (Directory.Exists(dataDir))
                    Directory.Delete(dataDir, true);

                var rt = await Runtime.Create(dataDir);

                Log.Header("Wonderlands Multi-Agent Runtime \u2014 " + Ai.AiClient.DescribeLlm());

                // ── Create session ────────────────────────────────────
                var session = await rt.Sessions.Add(new Session
                {
                    Id = DomainHelpers.NewId(),
                    Title = userMessage,
                    Goal = userMessage,
                    Status = "active",
                    CreatedAt = DomainHelpers.Now(),
                    UpdatedAt = DomainHelpers.Now(),
                });

                // ── Create root job (orchestrator) ────────────────────
                var orchestratorDef = AgentRegistry.Get("orchestrator");
                var rootJob = await rt.Jobs.Add(new Job
                {
                    Id = DomainHelpers.NewId(),
                    SessionId = session.Id,
                    Kind = "root",
                    Title = "Handle user request",
                    Status = "pending",
                    AgentName = "orchestrator",
                    Priority = 1,
                    CreatedAt = DomainHelpers.Now(),
                });

                // Add user message as first item for the root job
                await RuntimeHelpers.AddItem(rt, session.Id, "message",
                    new JObject { ["role"] = "user", ["text"] = userMessage },
                    rootJob.Id, null);

                Log.Info("Session: \"" + session.Title + "\"");
                Log.Info("Root job: " + rootJob.Title + " (agent=" + rootJob.AgentName + ")");

                // ── Process ───────────────────────────────────────────
                Log.Header("Processing");
                await WorkerLoop.ProcessSession(session.Id, rt);

                // Update session status
                var allJobs = await rt.Jobs.Find(j => j.SessionId == session.Id);
                bool allDone = allJobs.Count > 0 && allJobs.All(j => j.Status == "done");
                await rt.Sessions.Update(session.Id, s =>
                {
                    s.Status = allDone ? "done" : "paused";
                    s.UpdatedAt = DomainHelpers.Now();
                });

                // ── Summary ───────────────────────────────────────────
                Log.Header("Summary");

                var jobs = await rt.Jobs.All();
                var runs = await rt.Runs.All();
                var items = await rt.Items.All();
                var artifacts = await rt.Artifacts.All();

                Log.Info("Session status: " + (allDone ? "done" : "paused"));
                Log.Info("Jobs: " + jobs.Count + " (done=" + jobs.Count(j => j.Status == "done") + ", blocked=" + jobs.Count(j => j.Status == "blocked") + ")");
                Log.Info("Runs: " + runs.Count);
                Log.Info("Items: " + items.Count);
                Log.Info("Artifacts: " + artifacts.Count);

                if (session.Usage != null)
                {
                    Log.Info(string.Format("Total tokens: {0} (in={1}, out={2}, cached={3})",
                        session.Usage.TotalTokens, session.Usage.InputTokens,
                        session.Usage.OutputTokens, session.Usage.CachedTokens));
                }

                if (artifacts.Count > 0)
                {
                    Log.Header("Artifacts");
                    foreach (var a in artifacts.OrderBy(x => x.Path))
                    {
                        var chars = "";
                        if (a.Metadata != null && a.Metadata["chars"] != null)
                            chars = " (" + a.Metadata["chars"] + " chars)";
                        Log.Info("  " + a.Path + " v" + a.Version + chars);
                    }
                }

                Log.Success("Done.");
            }
            catch (Exception ex)
            {
                Log.Error("Fatal: " + ex.Message);
                Console.Error.WriteLine(ex);
            }
        }
    }
}
