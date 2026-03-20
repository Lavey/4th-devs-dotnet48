using System;
using System.Collections.Generic;

namespace FourthDevs.Lesson05_Agent.Events
{
    /// <summary>
    /// Event types with rich context for observability.
    /// Mirrors 01_05_agent/src/events/types.ts (i-am-alice/4th-devs).
    /// </summary>

    // ── Context carried by every event ───────────────────────────────

    internal class EventContext
    {
        public string TraceId     { get; set; }
        public long   Timestamp   { get; set; }
        public string SessionId   { get; set; }
        public string AgentId     { get; set; }
        public string RootAgentId { get; set; }
        public string ParentAgentId { get; set; }
        public int    Depth       { get; set; }
        public string BatchId     { get; set; }
    }

    // ── Token usage ──────────────────────────────────────────────────

    internal class TokenUsage
    {
        public int InputTokens  { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens  { get { return InputTokens + OutputTokens; } }
        public int CachedTokens { get; set; }
    }

    // ── Base event ───────────────────────────────────────────────────

    internal abstract class AgentEvent
    {
        public abstract string Type { get; }
        public EventContext Ctx { get; set; }
    }

    // ── Agent lifecycle ──────────────────────────────────────────────

    internal class AgentStartedEvent : AgentEvent
    {
        public override string Type => "agent.started";
        public string Model     { get; set; }
        public string Task      { get; set; }
        public string AgentName { get; set; }
    }

    internal class AgentCompletedEvent : AgentEvent
    {
        public override string Type => "agent.completed";
        public long       DurationMs { get; set; }
        public TokenUsage Usage      { get; set; }
        public string     Result     { get; set; }
    }

    internal class AgentFailedEvent : AgentEvent
    {
        public override string Type => "agent.failed";
        public string Error { get; set; }
    }

    internal class AgentCancelledEvent : AgentEvent
    {
        public override string Type => "agent.cancelled";
    }

    internal class AgentWaitingEvent : AgentEvent
    {
        public override string Type => "agent.waiting";
        public List<WaitingForEntry> WaitingFor { get; set; }
    }

    internal class AgentResumedEvent : AgentEvent
    {
        public override string Type => "agent.resumed";
        public string DeliveredCallId { get; set; }
        public int    Remaining       { get; set; }
    }

    // ── Turn lifecycle ───────────────────────────────────────────────

    internal class TurnStartedEvent : AgentEvent
    {
        public override string Type => "turn.started";
        public int TurnCount { get; set; }
    }

    internal class TurnCompletedEvent : AgentEvent
    {
        public override string Type => "turn.completed";
        public int        TurnCount { get; set; }
        public TokenUsage Usage     { get; set; }
    }

    // ── Generation (LLM call) ────────────────────────────────────────

    internal class GenerationCompletedEvent : AgentEvent
    {
        public override string Type => "generation.completed";
        public string     Model      { get; set; }
        public TokenUsage Usage      { get; set; }
        public long       DurationMs { get; set; }
    }

    // ── Tool execution ───────────────────────────────────────────────

    internal class ToolCalledEvent : AgentEvent
    {
        public override string Type => "tool.called";
        public string CallId    { get; set; }
        public string Name      { get; set; }
        public string Arguments { get; set; }
    }

    internal class ToolCompletedEvent : AgentEvent
    {
        public override string Type => "tool.completed";
        public string CallId     { get; set; }
        public string Name       { get; set; }
        public string Arguments  { get; set; }
        public string Output     { get; set; }
        public long   DurationMs { get; set; }
    }

    internal class ToolFailedEvent : AgentEvent
    {
        public override string Type => "tool.failed";
        public string CallId     { get; set; }
        public string Name       { get; set; }
        public string Arguments  { get; set; }
        public string Error      { get; set; }
        public long   DurationMs { get; set; }
    }

    // ── Helper factory ───────────────────────────────────────────────

    internal static class EventFactory
    {
        private static readonly long Epoch =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        internal static EventContext CreateContext(
            string traceId,
            string sessionId,
            string agentId,
            string rootAgentId,
            int    depth,
            string parentAgentId = null,
            string batchId       = null)
        {
            return new EventContext
            {
                TraceId       = traceId,
                Timestamp     = (DateTime.UtcNow.Ticks - Epoch) / TimeSpan.TicksPerMillisecond,
                SessionId     = sessionId,
                AgentId       = agentId,
                RootAgentId   = rootAgentId,
                ParentAgentId = parentAgentId,
                Depth         = depth,
                BatchId       = batchId
            };
        }
    }
}
