using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.Observability.Models
{
    // -------------------------------------------------------------------------
    // Domain types for the Chat Completions-based observability agent.
    // -------------------------------------------------------------------------

    internal sealed class Usage
    {
        [JsonProperty("input")]
        public int? Input { get; set; }

        [JsonProperty("output")]
        public int? Output { get; set; }

        [JsonProperty("total")]
        public int? Total { get; set; }
    }

    internal sealed class ToolCall
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }
    }

    internal sealed class CompletionResult
    {
        public string Text { get; set; }
        public List<ToolCall> ToolCalls { get; set; }
        public Usage Usage { get; set; }
    }

    // ---- Session / message types -------------------------------------------

    internal sealed class Session
    {
        public string Id { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    internal sealed class ChatMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public string Content { get; set; }

        [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
        public List<ChatToolCall> ToolCalls { get; set; }

        [JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ToolCallId { get; set; }
    }

    internal sealed class ChatToolCall
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        [JsonProperty("function")]
        public ChatFunctionCall Function { get; set; }
    }

    internal sealed class ChatFunctionCall
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }
    }

    // ---- Agent result ------------------------------------------------------

    internal sealed class AgentRunResult
    {
        public string Response { get; set; }
        public int Turns { get; set; }
        public Usage Usage { get; set; }
    }
}
