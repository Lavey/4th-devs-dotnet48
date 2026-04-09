using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.MultiAgentApi.Models
{
    // -------------------------------------------------------------------------
    // Session, Thread, and Message models
    // -------------------------------------------------------------------------

    internal class WorkSession
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("tenant_id")]
        public string TenantId { get; set; }

        [JsonProperty("workspace_id")]
        public string WorkspaceId { get; set; }

        [JsonProperty("agent_id")]
        public string AgentId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("created_by")]
        public string CreatedBy { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }

        [JsonProperty("threads")]
        public List<SessionThread> Threads { get; set; }
    }

    internal class SessionThread
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("session_id")]
        public string SessionId { get; set; }

        [JsonProperty("agent_id")]
        public string AgentId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }

        [JsonProperty("messages")]
        public List<SessionMessage> Messages { get; set; }
    }

    internal class SessionMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("thread_id")]
        public string ThreadId { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("metadata")]
        public string Metadata { get; set; }

        [JsonProperty("created_by")]
        public string CreatedBy { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }
}
