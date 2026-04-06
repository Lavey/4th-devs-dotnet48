using System;
using Newtonsoft.Json;

namespace FourthDevs.AgentGraph.Models
{
    public interface IEntity
    {
        string Id { get; }
    }

    // ── Sessions ──────────────────────────────────────────────────────────

    public class Session : IEntity
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("goal")] public string Goal { get; set; }
        [JsonProperty("status")] public string Status { get; set; } // active, paused, done
        [JsonProperty("usage")] public TokenUsage Usage { get; set; }
        [JsonProperty("created_at")] public string CreatedAt { get; set; }
        [JsonProperty("updated_at")] public string UpdatedAt { get; set; }
    }

    // ── Actors ────────────────────────────────────────────────────────────

    public class Actor : IEntity
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("session_id")] public string SessionId { get; set; }
        [JsonProperty("type")] public string Type { get; set; } // user, agent
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("status")] public string Status { get; set; } // active, idle
        [JsonProperty("capabilities")] public ActorCapabilities Capabilities { get; set; }
    }

    public class ActorCapabilities
    {
        [JsonProperty("tools")] public string[] Tools { get; set; }
        [JsonProperty("instructions")] public string Instructions { get; set; }
        [JsonProperty("webSearch")] public bool WebSearch { get; set; }
    }

    // ── Tasks ─────────────────────────────────────────────────────────────

    public class AgentTask : IEntity
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("session_id")] public string SessionId { get; set; }
        [JsonProperty("parent_task_id")] public string ParentTaskId { get; set; }
        [JsonProperty("owner_actor_id")] public string OwnerActorId { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("status")] public string Status { get; set; } // todo, in_progress, waiting, blocked, done
        [JsonProperty("memory")] public MemoryState Memory { get; set; }
        [JsonProperty("recovery")] public TaskRecoveryState Recovery { get; set; }
        [JsonProperty("priority")] public int Priority { get; set; }
        [JsonProperty("created_at")] public string CreatedAt { get; set; }
    }

    public class TaskRecoveryState
    {
        [JsonProperty("autoRetry")] public bool AutoRetry { get; set; }
        [JsonProperty("attempts")] public int Attempts { get; set; }
        [JsonProperty("lastFailureKind")] public string LastFailureKind { get; set; }
        [JsonProperty("lastFailureMessage")] public string LastFailureMessage { get; set; }
        [JsonProperty("lastFailureAt")] public string LastFailureAt { get; set; }
        [JsonProperty("nextRetryAt")] public string NextRetryAt { get; set; }
    }

    public class MemoryState
    {
        [JsonProperty("observations")] public string Observations { get; set; } = "";
        [JsonProperty("lastObservedSeq")] public int LastObservedSeq { get; set; }
        [JsonProperty("observationTokens")] public int ObservationTokens { get; set; }
        [JsonProperty("generation")] public int Generation { get; set; }
    }

    // ── Items ─────────────────────────────────────────────────────────────

    public class Item : IEntity
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("session_id")] public string SessionId { get; set; }
        [JsonProperty("task_id")] public string TaskId { get; set; }
        [JsonProperty("actor_id")] public string ActorId { get; set; }
        [JsonProperty("type")] public string Type { get; set; } // message, decision, invocation, result
        [JsonProperty("content")] public Newtonsoft.Json.Linq.JObject Content { get; set; }
        [JsonProperty("sequence")] public int Sequence { get; set; }
        [JsonProperty("created_at")] public string CreatedAt { get; set; }
    }

    // ── Artifacts ─────────────────────────────────────────────────────────

    public class Artifact : IEntity
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("session_id")] public string SessionId { get; set; }
        [JsonProperty("task_id")] public string TaskId { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; } // file, plan, diff, image
        [JsonProperty("path")] public string Path { get; set; }
        [JsonProperty("version")] public int Version { get; set; }
        [JsonProperty("metadata")] public Newtonsoft.Json.Linq.JObject Metadata { get; set; }
        [JsonProperty("created_at")] public string CreatedAt { get; set; }
    }

    // ── Token Usage ───────────────────────────────────────────────────────

    public class TokenUsage
    {
        [JsonProperty("inputTokens")] public int InputTokens { get; set; }
        [JsonProperty("outputTokens")] public int OutputTokens { get; set; }
        [JsonProperty("totalTokens")] public int TotalTokens { get; set; }
        [JsonProperty("cachedTokens")] public int CachedTokens { get; set; }

        public static TokenUsage Empty() => new TokenUsage();

        public static TokenUsage Add(TokenUsage a, TokenUsage b)
        {
            if (a == null) return b ?? Empty();
            if (b == null) return a;
            return new TokenUsage
            {
                InputTokens = a.InputTokens + b.InputTokens,
                OutputTokens = a.OutputTokens + b.OutputTokens,
                TotalTokens = a.TotalTokens + b.TotalTokens,
                CachedTokens = a.CachedTokens + b.CachedTokens,
            };
        }
    }

    // ── Relations ─────────────────────────────────────────────────────────

    public class Relation : IEntity
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("session_id")] public string SessionId { get; set; }
        [JsonProperty("from_kind")] public string FromKind { get; set; }
        [JsonProperty("from_id")] public string FromId { get; set; }
        [JsonProperty("relation_type")] public string RelationType { get; set; }
        [JsonProperty("to_kind")] public string ToKind { get; set; }
        [JsonProperty("to_id")] public string ToId { get; set; }
        [JsonProperty("created_at")] public string CreatedAt { get; set; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    public static class DomainHelpers
    {
        public static string NewId() => Guid.NewGuid().ToString();
        public static string Now() => DateTime.UtcNow.ToString("o");
    }
}
