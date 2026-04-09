using Newtonsoft.Json;

namespace FourthDevs.MultiAgentApi.Models
{
    // -------------------------------------------------------------------------
    // Runtime models: Job, Run, Item, ToolExecution, Wait
    // -------------------------------------------------------------------------

    internal class Job
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("tenant_id")]
        public string TenantId { get; set; }

        [JsonProperty("session_id")]
        public string SessionId { get; set; }

        [JsonProperty("thread_id")]
        public string ThreadId { get; set; }

        [JsonProperty("run_id")]
        public string RunId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("payload")]
        public string Payload { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }

        [JsonProperty("completed_at")]
        public string CompletedAt { get; set; }
    }

    internal class Run
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("tenant_id")]
        public string TenantId { get; set; }

        [JsonProperty("session_id")]
        public string SessionId { get; set; }

        [JsonProperty("thread_id")]
        public string ThreadId { get; set; }

        [JsonProperty("agent_id")]
        public string AgentId { get; set; }

        [JsonProperty("revision_id")]
        public string RevisionId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("turn_count")]
        public int TurnCount { get; set; }

        [JsonProperty("max_turns")]
        public int MaxTurns { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }

        [JsonProperty("completed_at")]
        public string CompletedAt { get; set; }
    }

    internal class Item
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("run_id")]
        public string RunId { get; set; }

        [JsonProperty("thread_id")]
        public string ThreadId { get; set; }

        [JsonProperty("sequence")]
        public int Sequence { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("tool_name")]
        public string ToolName { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }

        [JsonProperty("is_error")]
        public bool IsError { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    internal class ToolExecution
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("run_id")]
        public string RunId { get; set; }

        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("tool_name")]
        public string ToolName { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("duration_ms")]
        public long DurationMs { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("completed_at")]
        public string CompletedAt { get; set; }
    }

    internal class Wait
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("run_id")]
        public string RunId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("response")]
        public string Response { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("resolved_at")]
        public string ResolvedAt { get; set; }
    }
}
