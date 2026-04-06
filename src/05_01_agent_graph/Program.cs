using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Agents;
using FourthDevs.AgentGraph.Core;
using FourthDevs.AgentGraph.Models;
using FourthDevs.AgentGraph.Scheduler;
using FourthDevs.AgentGraph.Server;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentGraph
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

                // Clean data directory
                var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".data");
                if (Directory.Exists(dataDir))
                    Directory.Delete(dataDir, true);

                var rt = await Runtime.Create(dataDir);

                // Start dashboard server
                using (var server = new DashboardServer(rt))
                {
                    server.Start();
                    OpenBrowser(server.Url);
                    await Task.Delay(800);

                    Log.Header("Multi-Agent Core Schema — " + Ai.AiClient.DescribeLlm());

                    // ── Start session ─────────────────────────────────────
                    var session = await rt.Sessions.Add(new Session
                    {
                        Id = DomainHelpers.NewId(),
                        Title = userMessage,
                        Status = "active",
                        CreatedAt = DomainHelpers.Now(),
                        UpdatedAt = DomainHelpers.Now(),
                    });

                    var user = await rt.Actors.Add(new Actor
                    {
                        Id = DomainHelpers.NewId(),
                        SessionId = session.Id,
                        Type = "user",
                        Name = "alice",
                        Status = "active",
                    });

                    Actor orchestrator = null;
                    foreach (var name in AgentRegistry.BootstrapAgents)
                    {
                        var def = AgentRegistry.Get(name);
                        if (def == null) continue;
                        var actor = await rt.Actors.Add(new Actor
                        {
                            Id = DomainHelpers.NewId(),
                            SessionId = session.Id,
                            Type = def.Type,
                            Name = def.Name,
                            Status = "active",
                            Capabilities = new ActorCapabilities
                            {
                                Tools = def.Tools,
                                Instructions = def.Instructions,
                            },
                        });
                        if (def.Name == "orchestrator") orchestrator = actor;
                    }

                    var rootTask = await rt.Tasks.Add(new AgentTask
                    {
                        Id = DomainHelpers.NewId(),
                        SessionId = session.Id,
                        OwnerActorId = orchestrator.Id,
                        Title = "Handle user request",
                        Status = "todo",
                        Priority = 1,
                        CreatedAt = DomainHelpers.Now(),
                    });

                    await RuntimeHelpers.AddItem(rt, session.Id, "message",
                        new JObject { ["role"] = "user", ["text"] = userMessage },
                        rootTask.Id, user.Id);

                    await RuntimeHelpers.AddRelation(rt, session.Id,
                        "task", rootTask.Id, "assigned_to", "actor", orchestrator.Id);

                    Log.Info("Session: \"" + session.Title + "\"");
                    Log.Info("Orchestrator: " + orchestrator.Name);

                    // ── Process ───────────────────────────────────────────
                    Log.Header("Processing");
                    await SessionLoop.ProcessSession(session.Id, rt);

                    // Update session status
                    var allTasks = await rt.Tasks.Find(t => t.SessionId == session.Id);
                    bool allDone = allTasks.Count > 0 && allTasks.All(t => t.Status == "done");
                    await rt.Sessions.Update(session.Id, s =>
                    {
                        s.Status = allDone ? "done" : "paused";
                        s.UpdatedAt = DomainHelpers.Now();
                    });

                    // ── Summary ───────────────────────────────────────────
                    Log.Header("Graph Summary");
                    var sessions = await rt.Sessions.All();
                    var actors = await rt.Actors.All();
                    var tasks = await rt.Tasks.All();
                    var items = await rt.Items.All();
                    var artifacts = await rt.Artifacts.All();
                    var relations = await rt.Relations.All();

                    Log.Summary("sessions", sessions.Count);
                    Log.Summary("actors", actors.Count);
                    Log.Summary("tasks", tasks.Count);
                    Log.Summary("items", items.Count);
                    Log.Summary("artifacts", artifacts.Count);
                    Log.Summary("relations", relations.Count);

                    var usage = sessions.FirstOrDefault()?.Usage;
                    if (usage != null)
                    {
                        var cacheRate = usage.InputTokens > 0
                            ? (int)Math.Round(100.0 * usage.CachedTokens / usage.InputTokens) : 0;
                        Log.Summary("tokens (in/out/cached)", usage.InputTokens + " / " + usage.OutputTokens + " / " + usage.CachedTokens);
                        Log.Summary("cache hit rate", cacheRate + "%");
                    }

                    Log.Header("Relations");
                    foreach (var rel in relations)
                    {
                        var from = await ResolveLabel(rel.FromKind, rel.FromId, rt);
                        var to = await ResolveLabel(rel.ToKind, rel.ToId, rt);
                        Console.WriteLine("  [" + rel.FromKind + "] " + from + " ──" + rel.RelationType + "──▸ [" + rel.ToKind + "] " + to);
                    }

                    Log.Header("Task Tree");
                    var roots = tasks.Where(t => string.IsNullOrEmpty(t.ParentTaskId)).ToList();
                    foreach (var t in roots)
                    {
                        Console.WriteLine("  " + StatusIcon(t.Status) + " " + t.Title);
                        var children = tasks.Where(c => c.ParentTaskId == t.Id).ToList();
                        foreach (var c in children)
                            Console.WriteLine("    " + StatusIcon(c.Status) + " " + c.Title);
                    }

                    Log.Header("Artifacts");
                    foreach (var a in artifacts)
                    {
                        var chars = a.Metadata != null && a.Metadata["chars"] != null
                            ? a.Metadata["chars"].ToString() : "?";
                        Console.WriteLine("  [" + a.Kind.PadRight(4) + "] " + a.Path + "  v" + a.Version + "  (" + chars + " chars)");
                    }

                    Log.Success("All data persisted to .data/");
                    Log.Done();

                    Log.Info("Dashboard still live at " + server.Url + " — Ctrl+C to stop");

                    // Keep running until Ctrl+C
                    var exitEvent = new System.Threading.ManualResetEventSlim(false);
                    Console.CancelKeyPress += (s, e) => { e.Cancel = true; exitEvent.Set(); };
                    exitEvent.Wait();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Environment.ExitCode = 1;
            }
        }

        private static string StatusIcon(string status)
        {
            switch (status)
            {
                case "todo": return "○";
                case "in_progress": return "◐";
                case "waiting": return "⏸";
                case "blocked": return "◆";
                case "done": return "●";
                default: return "?";
            }
        }

        private static string Truncate(string s, int max)
        {
            if (s == null) return "";
            return s.Length > max ? s.Substring(0, max) + "…" : s;
        }

        private static async Task<string> ResolveLabel(string kind, string id, Runtime rt)
        {
            switch (kind)
            {
                case "actor":
                    var a = await rt.Actors.GetById(id);
                    return a != null ? a.Name : id.Substring(0, Math.Min(8, id.Length));
                case "task":
                    var t = await rt.Tasks.GetById(id);
                    return t != null ? Truncate(t.Title, 44) : id.Substring(0, Math.Min(8, id.Length));
                case "artifact":
                    var art = await rt.Artifacts.GetById(id);
                    return art != null ? art.Path : id.Substring(0, Math.Min(8, id.Length));
                default:
                    return id.Substring(0, Math.Min(8, id.Length));
            }
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { /* ignore */ }
        }
    }
}
