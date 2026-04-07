using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Models
{
    // -------------------------------------------------------------------------
    // SSE stream event types sent from the server to the dashboard.
    // Each event carries a sequential number and ISO-8601 timestamp.
    // -------------------------------------------------------------------------

    public abstract class BaseStreamEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("messageId")]
        public string MessageId { get; set; }

        [JsonProperty("seq")]
        public int Seq { get; set; }

        [JsonProperty("at")]
        public string At { get; set; }
    }

    public sealed class AssistantMessageStartEvent : BaseStreamEvent
    {
        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        public AssistantMessageStartEvent() { Type = "assistant_message_start"; }
    }

    public sealed class TextDeltaEvent : BaseStreamEvent
    {
        [JsonProperty("textDelta")]
        public string TextDelta { get; set; }

        public TextDeltaEvent() { Type = "text_delta"; }
    }

    public sealed class ThinkingStartEvent : BaseStreamEvent
    {
        [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
        public string Label { get; set; }

        public ThinkingStartEvent() { Type = "thinking_start"; }
    }

    public sealed class ThinkingDeltaEvent : BaseStreamEvent
    {
        [JsonProperty("textDelta")]
        public string TextDelta { get; set; }

        public ThinkingDeltaEvent() { Type = "thinking_delta"; }
    }

    public sealed class ThinkingEndEvent : BaseStreamEvent
    {
        public ThinkingEndEvent() { Type = "thinking_end"; }
    }

    public sealed class ToolCallEvent : BaseStreamEvent
    {
        [JsonProperty("toolCallId")]
        public string ToolCallId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("args")]
        public JObject Args { get; set; }

        public ToolCallEvent() { Type = "tool_call"; }
    }

    public sealed class ToolResultEvent : BaseStreamEvent
    {
        [JsonProperty("toolCallId")]
        public string ToolCallId { get; set; }

        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("output")]
        public object Output { get; set; }

        public ToolResultEvent() { Type = "tool_result"; }
    }

    public sealed class ArtifactEvent : BaseStreamEvent
    {
        [JsonProperty("artifactId")]
        public string ArtifactId { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("path", NullValueHandling = NullValueHandling.Ignore)]
        public string Path { get; set; }

        [JsonProperty("preview")]
        public string Preview { get; set; }

        public ArtifactEvent() { Type = "artifact"; }
    }

    public sealed class ErrorEvent : BaseStreamEvent
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        public ErrorEvent() { Type = "error"; }
    }

    public sealed class CompleteEvent : BaseStreamEvent
    {
        [JsonProperty("finishReason")]
        public string FinishReason { get; set; }

        public CompleteEvent() { Type = "complete"; }
    }
}
