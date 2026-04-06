using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Agents;
using FourthDevs.AgentGraph.Ai;
using FourthDevs.AgentGraph.Core;
using FourthDevs.AgentGraph.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentGraph.Tools
{
    public static class ToolRegistry
    {
        // ── Tool definitions ─────────────────────────────────────────────

        public static readonly Dictionary<string, ToolDefinition> Definitions = BuildDefinitions();

        private static Dictionary<string, ToolDefinition> BuildDefinitions()
        {
            var agentNamesStr = string.Join(", ", AgentRegistry.AgentNames);
            var toolNamesStr = string.Join(", ", ActorToolNames.All);

            return new Dictionary<string, ToolDefinition>
            {
                ["create_actor"] = new ToolDefinition
                {
                    Name = "create_actor",
                    Description = "Create or update a specialist actor in this session. For registry agents (" + agentNamesStr + "), only \"name\" is needed — tools and instructions are predefined. For custom agents, provide all three fields.",
                    Parameters = JObject.Parse(@"{
                        ""type"":""object"",
                        ""properties"":{
                            ""name"":{""type"":""string"",""description"":""Actor name. Use a registry name for predefined config, or a custom name for ad-hoc agents.""},
                            ""instructions"":{""type"":""string"",""description"":""Role instructions (required only for non-registry actors)""},
                            ""tools"":{""type"":""array"",""description"":""Allowed tools (required only for non-registry actors)"",""items"":{""type"":""string"",""enum"":[" + string.Join(",", ActorToolNames.All.Select(n => "\"" + n + "\"")) + @"]}}
                        },
                        ""required"":[""name""],
                        ""additionalProperties"":false
                    }")
                },
                ["delegate_task"] = new ToolDefinition
                {
                    Name = "delegate_task",
                    Description = "Create a child task assigned to an existing actor. Use returned task ids when creating dependency chains.",
                    Parameters = JObject.Parse(@"{
                        ""type"":""object"",
                        ""properties"":{
                            ""actorName"":{""type"":""string"",""description"":""Target actor name, e.g. researcher or writer""},
                            ""title"":{""type"":""string"",""description"":""Short task title""},
                            ""instructions"":{""type"":""string"",""description"":""Detailed instructions for the delegated task""},
                            ""priority"":{""type"":""integer"",""description"":""Lower runs first. Default 1""},
                            ""dependsOnTaskIds"":{""type"":""array"",""description"":""Task ids that must finish before this task can run"",""items"":{""type"":""string""}}
                        },
                        ""required"":[""actorName"",""title"",""instructions""],
                        ""additionalProperties"":false
                    }")
                },
                ["complete_task"] = new ToolDefinition
                {
                    Name = "complete_task",
                    Description = "Mark the current task finished once the work is truly done.",
                    Parameters = JObject.Parse(@"{
                        ""type"":""object"",
                        ""properties"":{
                            ""summary"":{""type"":""string"",""description"":""Short summary of what was completed""}
                        },
                        ""required"":[""summary""],
                        ""additionalProperties"":false
                    }")
                },
                ["block_task"] = new ToolDefinition
                {
                    Name = "block_task",
                    Description = "Block the current task when you cannot make progress.",
                    Parameters = JObject.Parse(@"{
                        ""type"":""object"",
                        ""properties"":{
                            ""reason"":{""type"":""string"",""description"":""Why the task is blocked""}
                        },
                        ""required"":[""reason""],
                        ""additionalProperties"":false
                    }")
                },
                ["read_artifact"] = new ToolDefinition
                {
                    Name = "read_artifact",
                    Description = "Read an existing artifact by artifact id or relative path.",
                    Parameters = JObject.Parse(@"{
                        ""type"":""object"",
                        ""properties"":{
                            ""artifactId"":{""type"":""string"",""description"":""Exact artifact id if you already know it""},
                            ""path"":{""type"":""string"",""description"":""Relative artifact path such as research/typescript-5.md""}
                        },
                        ""additionalProperties"":false
                    }")
                },
                ["write_artifact"] = new ToolDefinition
                {
                    Name = "write_artifact",
                    Description = "Write or update an artifact file for the current task.",
                    Parameters = JObject.Parse(@"{
                        ""type"":""object"",
                        ""properties"":{
                            ""path"":{""type"":""string"",""description"":""Relative artifact path. Never use absolute paths.""},
                            ""content"":{""type"":""string"",""description"":""Full file contents""},
                            ""kind"":{""type"":""string"",""enum"":[""file"",""plan"",""diff"",""image""],""description"":""Artifact kind. Default is file.""}
                        },
                        ""required"":[""path"",""content""],
                        ""additionalProperties"":false
                    }")
                },
                ["send_email"] = new ToolDefinition
                {
                    Name = "send_email",
                    Description = "Compose and send an email. The email is saved as a formatted markdown file in the artifacts. Use {{file:path}} in the body to inline content from an existing artifact.",
                    Parameters = JObject.Parse(@"{
                        ""type"":""object"",
                        ""properties"":{
                            ""to"":{""type"":""string"",""description"":""Comma-separated recipient email addresses""},
                            ""subject"":{""type"":""string"",""description"":""Email subject line""},
                            ""body"":{""type"":""string"",""description"":""Email body in markdown. Use {{file:path}} to include artifact content inline.""},
                            ""cc"":{""type"":""string"",""description"":""Optional CC recipients""}
                        },
                        ""required"":[""to"",""subject"",""body""],
                        ""additionalProperties"":false
                    }")
                },
            };
        }

        // ── Tool handlers ────────────────────────────────────────────────

        private static readonly Dictionary<string, ToolHandler> Handlers = new Dictionary<string, ToolHandler>
        {
            ["create_actor"] = HandleCreateActor,
            ["delegate_task"] = HandleDelegateTask,
            ["complete_task"] = HandleCompleteTask,
            ["block_task"] = HandleBlockTask,
            ["read_artifact"] = HandleReadArtifact,
            ["write_artifact"] = HandleWriteArtifact,
            ["send_email"] = HandleSendEmail,
        };

        public static async Task<ToolExecutionOutcome> ExecuteToolCall(ToolCall call, AgentTask task, Actor actor, Runtime rt)
        {
            ToolHandler handler;
            if (!Handlers.TryGetValue(call.Name, out handler))
                throw new Exception("Unknown tool: " + call.Name);
            return await handler(new ToolContext { Call = call, Task = task, Actor = actor, Rt = rt });
        }

        public static ActorToolConfig GetActorConfig(Actor actor)
        {
            var caps = actor.Capabilities;
            var def = AgentRegistry.Get(actor.Name);

            var tools = caps?.Tools != null && caps.Tools.Length > 0
                ? caps.Tools.Where(ActorToolNames.IsValid).ToArray()
                : (def?.Tools ?? new[] { "complete_task", "block_task" });

            var webSearch = (caps != null && caps.WebSearch) || (def != null && def.WebSearch);

            return new ActorToolConfig
            {
                Instructions = !string.IsNullOrEmpty(caps?.Instructions)
                    ? caps.Instructions
                    : (def?.Instructions ?? "Use the available tools to finish the task."),
                Tools = tools,
                WebSearch = webSearch,
            };
        }

        // ── Handler implementations ──────────────────────────────────────

        private static async Task<ToolExecutionOutcome> HandleCreateActor(ToolContext ctx)
        {
            var name = Args.GetString(ctx.Call.Arguments, "name");
            var registry = AgentRegistry.Get(name);

            var tools = registry?.Tools
                ?? (ctx.Call.Arguments["tools"] != null ? Args.GetToolNameArray(ctx.Call.Arguments, "tools") : null);
            var instructions = registry?.Instructions ?? Args.GetOptionalString(ctx.Call.Arguments, "instructions");
            var webSearch = registry != null && registry.WebSearch;

            if (tools == null || tools.Length == 0)
                throw new Exception("\"" + name + "\" is not a registry agent — provide tools: [" + string.Join(", ", ActorToolNames.All) + "]");
            if (string.IsNullOrEmpty(instructions))
                throw new Exception("\"" + name + "\" is not a registry agent — provide instructions");

            var actors = await ctx.Rt.Actors.Find(a => a.SessionId == ctx.Task.SessionId && a.Name == name);
            var existing = actors.FirstOrDefault();

            if (existing != null)
            {
                if (existing.Type == "user") throw new Exception("Cannot overwrite user actor: " + name);
                var updated = await ctx.Rt.Actors.Update(existing.Id, a =>
                {
                    a.Status = "active";
                    a.Capabilities = new ActorCapabilities { Tools = tools, Instructions = instructions, WebSearch = webSearch };
                });
                Log.Info("Updated actor " + name + " with tools: " + string.Join(", ", tools) + (webSearch ? " +web_search" : ""));
                return new ToolExecutionOutcome
                {
                    Status = "continue",
                    Message = "Updated actor " + updated.Name,
                    Output = JsonConvert.SerializeObject(new { actorId = updated.Id, name = updated.Name, created = false, tools, webSearch })
                };
            }

            var created = await ctx.Rt.Actors.Add(new Actor
            {
                Id = DomainHelpers.NewId(),
                SessionId = ctx.Task.SessionId,
                Type = "agent",
                Name = name,
                Status = "active",
                Capabilities = new ActorCapabilities { Tools = tools, Instructions = instructions, WebSearch = webSearch },
            });
            Log.Info("Created actor " + name + " with tools: " + string.Join(", ", tools) + (webSearch ? " +web_search" : ""));
            return new ToolExecutionOutcome
            {
                Status = "continue",
                Message = "Created actor " + created.Name,
                Output = JsonConvert.SerializeObject(new { actorId = created.Id, name = created.Name, created = true, tools, webSearch })
            };
        }

        private static async Task<ToolExecutionOutcome> HandleDelegateTask(ToolContext ctx)
        {
            var actorName = Args.GetString(ctx.Call.Arguments, "actorName");
            var title = Args.GetString(ctx.Call.Arguments, "title");
            var instructions = Args.GetString(ctx.Call.Arguments, "instructions");
            var priority = Args.GetPositiveInteger(ctx.Call.Arguments, "priority", 1);
            var dependsOnTaskIds = Args.GetStringArray(ctx.Call.Arguments, "dependsOnTaskIds");

            var assignees = await ctx.Rt.Actors.Find(a => a.SessionId == ctx.Task.SessionId && a.Name == actorName);
            var assignee = assignees.FirstOrDefault();
            if (assignee == null) throw new Exception("Unknown actor: " + actorName);

            Log.Delegate(ctx.Actor.Name, assignee.Name, title);

            var childTask = await ctx.Rt.Tasks.Add(new AgentTask
            {
                Id = DomainHelpers.NewId(),
                SessionId = ctx.Task.SessionId,
                ParentTaskId = ctx.Task.Id,
                OwnerActorId = assignee.Id,
                Title = title,
                Status = dependsOnTaskIds.Length > 0 ? "waiting" : "todo",
                Priority = priority,
                CreatedAt = DomainHelpers.Now(),
            });

            await RuntimeHelpers.EnsureRelation(ctx.Rt, ctx.Task.SessionId, "task", childTask.Id, "assigned_to", "actor", assignee.Id);
            foreach (var depId in dependsOnTaskIds)
                await RuntimeHelpers.EnsureRelation(ctx.Rt, ctx.Task.SessionId, "task", childTask.Id, "depends_on", "task", depId);

            await RuntimeHelpers.AddItem(ctx.Rt, ctx.Task.SessionId, "message",
                new JObject { ["role"] = "delegator", ["text"] = instructions, ["fromActor"] = ctx.Actor.Name },
                childTask.Id, ctx.Actor.Id);

            return new ToolExecutionOutcome
            {
                Status = "continue",
                Message = "Delegated \"" + title + "\" to " + assignee.Name + ". The scheduler will resume this task after child work finishes.",
                Output = JsonConvert.SerializeObject(new { taskId = childTask.Id, actorName = assignee.Name, title = childTask.Title, status = childTask.Status, dependsOnTaskIds })
            };
        }

        private static Task<ToolExecutionOutcome> HandleCompleteTask(ToolContext ctx)
        {
            var summary = Args.GetString(ctx.Call.Arguments, "summary");
            return Task.FromResult(new ToolExecutionOutcome
            {
                Status = "completed",
                Message = summary,
                Output = JsonConvert.SerializeObject(new { ok = true, summary })
            });
        }

        private static Task<ToolExecutionOutcome> HandleBlockTask(ToolContext ctx)
        {
            var reason = Args.GetString(ctx.Call.Arguments, "reason");
            return Task.FromResult(new ToolExecutionOutcome
            {
                Status = "blocked",
                Message = reason,
                Output = JsonConvert.SerializeObject(new { ok = false, reason })
            });
        }

        private static async Task<ToolExecutionOutcome> HandleReadArtifact(ToolContext ctx)
        {
            var artifactId = Args.GetOptionalString(ctx.Call.Arguments, "artifactId");
            var requestedPath = Args.GetOptionalString(ctx.Call.Arguments, "path");

            Artifact artifact = null;
            if (artifactId != null)
                artifact = await ctx.Rt.Artifacts.GetById(artifactId);
            else if (requestedPath != null)
                artifact = await ArtifactShared.GetLatestArtifactByPath(ctx.Task.SessionId, ArtifactShared.NormalizeArtifactPath(requestedPath), ctx.Rt);

            if (artifact == null || artifact.SessionId != ctx.Task.SessionId)
            {
                var all = await ctx.Rt.Artifacts.Find(a => a.SessionId == ctx.Task.SessionId);
                var available = string.Join(", ", all.Select(a => a.Path));
                if (string.IsNullOrEmpty(available)) available = "none";
                throw new Exception("Artifact not found. Available artifacts: " + available);
            }

            var content = ArtifactShared.ReadArtifactContent(ctx.Rt, artifact.Path);
            Log.ArtifactLog("read", artifact.Path, content.Length);

            var truncated = content.Length > ArtifactShared.MaxReadArtifactChars;
            var visibleContent = truncated
                ? content.Substring(0, ArtifactShared.MaxReadArtifactChars) + "\n\n[truncated]"
                : content;

            await RuntimeHelpers.EnsureRelation(ctx.Rt, ctx.Task.SessionId, "task", ctx.Task.Id, "uses", "artifact", artifact.Id);

            return new ToolExecutionOutcome
            {
                Status = "continue",
                Message = "Read artifact " + artifact.Path,
                Output = JsonConvert.SerializeObject(new { artifactId = artifact.Id, path = artifact.Path, kind = artifact.Kind, version = artifact.Version, truncated, content = visibleContent })
            };
        }

        private static async Task<ToolExecutionOutcome> HandleWriteArtifact(ToolContext ctx)
        {
            var artifactPath = ArtifactShared.NormalizeArtifactPath(Args.GetString(ctx.Call.Arguments, "path"));
            var content = ArtifactShared.ResolveFilePlaceholders(Args.GetString(ctx.Call.Arguments, "content"), ctx.Rt);
            var kindStr = Args.GetOptionalString(ctx.Call.Arguments, "kind");
            var resolvedKind = (kindStr == "plan" || kindStr == "file" || kindStr == "diff" || kindStr == "image") ? kindStr : "file";

            ArtifactShared.WriteArtifactContent(ctx.Rt, artifactPath, content);
            Log.ArtifactLog("wrote", artifactPath, content.Length);
            var artifact = await ArtifactShared.UpsertArtifact(ctx.Rt, ctx.Task, artifactPath, resolvedKind, content);

            return new ToolExecutionOutcome
            {
                Status = "continue",
                Message = "Wrote artifact " + artifact.Path,
                Output = JsonConvert.SerializeObject(new { artifactId = artifact.Id, path = artifact.Path, kind = artifact.Kind, version = artifact.Version, chars = content.Length })
            };
        }

        private static async Task<ToolExecutionOutcome> HandleSendEmail(ToolContext ctx)
        {
            var to = Args.GetString(ctx.Call.Arguments, "to");
            var subject = Args.GetString(ctx.Call.Arguments, "subject");
            var rawBody = Args.GetString(ctx.Call.Arguments, "body");
            var cc = Args.GetOptionalString(ctx.Call.Arguments, "cc");

            var body = ArtifactShared.ResolveFilePlaceholders(rawBody, ctx.Rt);
            var timestamp = DateTime.UtcNow.ToString("o");
            var slug = System.Text.RegularExpressions.Regex.Replace(subject.ToLowerInvariant(), @"[^a-z0-9]+", "-");
            if (slug.Length > 40) slug = slug.Substring(0, 40);
            var emailPath = ArtifactShared.NormalizeArtifactPath("emails/" + slug + ".md");

            var lines = new List<string> { "---", "to: " + to };
            if (cc != null) lines.Add("cc: " + cc);
            lines.AddRange(new[] { "subject: " + subject, "date: " + timestamp, "status: sent", "---", "", body });
            var content = string.Join("\n", lines);

            ArtifactShared.WriteArtifactContent(ctx.Rt, emailPath, content);
            Log.ArtifactLog("wrote", emailPath, content.Length);
            var artifact = await ArtifactShared.UpsertArtifact(ctx.Rt, ctx.Task, emailPath, "file", content);

            return new ToolExecutionOutcome
            {
                Status = "continue",
                Message = "Email sent to " + to + ": \"" + subject + "\"",
                Output = JsonConvert.SerializeObject(new { artifactId = artifact.Id, path = artifact.Path, to, cc = (object)cc, subject, chars = content.Length })
            };
        }
    }
}
