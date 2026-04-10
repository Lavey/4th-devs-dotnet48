using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Wonderlands.Agents;
using FourthDevs.Wonderlands.Ai;
using FourthDevs.Wonderlands.Core;
using FourthDevs.Wonderlands.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Wonderlands.Tools
{
    public static class ToolRegistry
    {
        public static readonly Dictionary<string, ToolDefinition> Definitions = BuildDefinitions();

        private static Dictionary<string, ToolDefinition> BuildDefinitions()
        {
            var agentNamesStr = string.Join(", ", AgentRegistry.AgentNames);

            return new Dictionary<string, ToolDefinition>
            {
                ["delegate_to_agent"] = new ToolDefinition
                {
                    Name = "delegate_to_agent",
                    Description = "Delegate a child job to a specialist agent. The child job gets its own run. Available agents: " + agentNamesStr + ". Use dependsOnJobIds when one child must wait for another.",
                    Parameters = JObject.Parse(@"{
                        ""type"":""object"",
                        ""properties"":{
                            ""agentName"":{""type"":""string"",""description"":""Target agent name from the registry""},
                            ""title"":{""type"":""string"",""description"":""Short job title""},
                            ""instructions"":{""type"":""string"",""description"":""Detailed instructions for the delegated job""},
                            ""priority"":{""type"":""integer"",""description"":""Lower runs first. Default 1""},
                            ""dependsOnJobIds"":{""type"":""array"",""description"":""Job ids that must finish before this job can run"",""items"":{""type"":""string""}}
                        },
                        ""required"":[""agentName"",""title"",""instructions""],
                        ""additionalProperties"":false
                    }")
                },
                ["complete_task"] = new ToolDefinition
                {
                    Name = "complete_task",
                    Description = "Mark the current job finished once the work is truly done.",
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
                    Description = "Block the current job when you cannot make progress.",
                    Parameters = JObject.Parse(@"{
                        ""type"":""object"",
                        ""properties"":{
                            ""reason"":{""type"":""string"",""description"":""Why the job is blocked""}
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
                    Description = "Write or update an artifact file for the current job.",
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
            };
        }

        private static readonly Dictionary<string, ToolHandler> Handlers = new Dictionary<string, ToolHandler>
        {
            ["delegate_to_agent"] = HandleDelegateToAgent,
            ["complete_task"] = HandleCompleteTask,
            ["block_task"] = HandleBlockTask,
            ["read_artifact"] = HandleReadArtifact,
            ["write_artifact"] = HandleWriteArtifact,
        };

        public static async Task<ToolExecutionOutcome> ExecuteToolCall(ToolCall call, ToolContext ctx)
        {
            ToolHandler handler;
            if (!Handlers.TryGetValue(call.Name, out handler))
                throw new Exception("Unknown tool: " + call.Name);
            return await handler(ctx);
        }

        public static AgentToolConfig GetAgentConfig(string agentName)
        {
            var def = AgentRegistry.Get(agentName);
            if (def == null)
                return new AgentToolConfig
                {
                    Instructions = "Use the available tools to finish the task.",
                    Tools = new[] { "complete_task", "block_task" },
                    WebSearch = false,
                };

            return new AgentToolConfig
            {
                Instructions = def.Instructions,
                Tools = def.Tools ?? new[] { "complete_task", "block_task" },
                WebSearch = def.WebSearch,
            };
        }

        // ── Handler implementations ──────────────────────────────────────

        private static async Task<ToolExecutionOutcome> HandleDelegateToAgent(ToolContext ctx)
        {
            var agentName = Args.GetString(ctx.Call.Arguments, "agentName");
            var title = Args.GetString(ctx.Call.Arguments, "title");
            var instructions = Args.GetString(ctx.Call.Arguments, "instructions");
            var priority = Args.GetPositiveInteger(ctx.Call.Arguments, "priority", 1);
            var dependsOnJobIds = Args.GetStringArray(ctx.Call.Arguments, "dependsOnJobIds");

            var def = AgentRegistry.Get(agentName);
            if (def == null)
                throw new Exception("Unknown agent: " + agentName + ". Available: " + string.Join(", ", AgentRegistry.AgentNames));

            var childJob = await ctx.Rt.Jobs.Add(new Job
            {
                Id = DomainHelpers.NewId(),
                SessionId = ctx.Job.SessionId,
                ParentJobId = ctx.Job.Id,
                Kind = "delegated",
                Title = title,
                Status = "pending",
                AgentName = agentName,
                Priority = priority,
                CreatedAt = DomainHelpers.Now(),
            });

            await RuntimeHelpers.AddRelation(ctx.Rt, ctx.Job.SessionId,
                "job", ctx.Job.Id, "parent_of", "job", childJob.Id);

            foreach (var depId in dependsOnJobIds)
            {
                await RuntimeHelpers.AddRelation(ctx.Rt, ctx.Job.SessionId,
                    "job", childJob.Id, "depends_on", "job", depId);
            }

            await RuntimeHelpers.AddItem(ctx.Rt, ctx.Job.SessionId, "message",
                new JObject
                {
                    ["role"] = "delegator",
                    ["fromAgent"] = ctx.Job.AgentName,
                    ["text"] = instructions,
                },
                childJob.Id, null);

            Log.Delegate(ctx.Job.AgentName, agentName, title);

            return new ToolExecutionOutcome
            {
                Status = "continue",
                Output = JsonConvert.SerializeObject(new { jobId = childJob.Id, agent = agentName, title }),
                Message = "Delegated to " + agentName + ": " + title,
            };
        }

        private static Task<ToolExecutionOutcome> HandleCompleteTask(ToolContext ctx)
        {
            var summary = Args.GetString(ctx.Call.Arguments, "summary");
            return Task.FromResult(new ToolExecutionOutcome
            {
                Status = "completed",
                Output = JsonConvert.SerializeObject(new { summary }),
                Message = summary,
            });
        }

        private static Task<ToolExecutionOutcome> HandleBlockTask(ToolContext ctx)
        {
            var reason = Args.GetString(ctx.Call.Arguments, "reason");
            return Task.FromResult(new ToolExecutionOutcome
            {
                Status = "blocked",
                Output = JsonConvert.SerializeObject(new { reason }),
                Message = reason,
            });
        }

        private static async Task<ToolExecutionOutcome> HandleReadArtifact(ToolContext ctx)
        {
            var artifactId = Args.GetOptionalString(ctx.Call.Arguments, "artifactId");
            var path = Args.GetOptionalString(ctx.Call.Arguments, "path");

            Artifact artifact = null;
            if (!string.IsNullOrEmpty(artifactId))
                artifact = await ctx.Rt.Artifacts.GetById(artifactId);
            else if (!string.IsNullOrEmpty(path))
                artifact = await ArtifactShared.GetLatestArtifactByPath(ctx.Job.SessionId, ArtifactShared.NormalizeArtifactPath(path), ctx.Rt);

            if (artifact == null)
                throw new Exception("Artifact not found");

            string content;
            try { content = ArtifactShared.ReadArtifactContent(ctx.Rt, artifact.Path); }
            catch { throw new Exception("Artifact file missing: " + artifact.Path); }

            if (content.Length > ArtifactShared.MaxReadArtifactChars)
                content = content.Substring(0, ArtifactShared.MaxReadArtifactChars) + "\n… (truncated)";

            return new ToolExecutionOutcome
            {
                Status = "continue",
                Output = JsonConvert.SerializeObject(new { id = artifact.Id, path = artifact.Path, content }),
                Message = "Read artifact " + artifact.Path,
            };
        }

        private static async Task<ToolExecutionOutcome> HandleWriteArtifact(ToolContext ctx)
        {
            var rawPath = Args.GetString(ctx.Call.Arguments, "path");
            var content = Args.GetString(ctx.Call.Arguments, "content");
            var kind = Args.GetOptionalString(ctx.Call.Arguments, "kind") ?? "file";
            var artifactPath = ArtifactShared.NormalizeArtifactPath(rawPath);

            ArtifactShared.WriteArtifactContent(ctx.Rt, artifactPath, content);
            var artifact = await ArtifactShared.UpsertArtifact(ctx.Rt, ctx.Job, artifactPath, kind, content);

            return new ToolExecutionOutcome
            {
                Status = "continue",
                Output = JsonConvert.SerializeObject(new { id = artifact.Id, path = artifactPath, version = artifact.Version, chars = content.Length }),
                Message = "Wrote " + artifactPath + " (" + content.Length + " chars)",
            };
        }
    }
}
