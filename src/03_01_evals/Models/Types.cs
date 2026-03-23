using System.Collections.Generic;
using FourthDevs.Common.Models;
using Newtonsoft.Json;

namespace FourthDevs.Evals.Models
{
    // -------------------------------------------------------------------------
    // Domain types for the Responses API-based evals agent.
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
        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }
    }

    internal sealed class CompletionResult
    {
        public string Text { get; set; }
        public List<ToolCall> ToolCalls { get; set; } = new List<ToolCall>();
        public List<OutputItem> Output { get; set; } = new List<OutputItem>();
        public Usage Usage { get; set; }
    }

    /// <summary>
    /// Session stores a heterogeneous list of messages: InputMessage, OutputItem,
    /// and anonymous tool-result objects (function_call / function_call_output).
    /// </summary>
    internal sealed class Session
    {
        public string Id { get; set; }
        public List<object> Messages { get; set; } = new List<object>();
    }

    internal sealed class AgentRunResult
    {
        public string Response { get; set; }
        public int Turns { get; set; }
        public Usage Usage { get; set; }
        public List<string> ToolsUsed { get; set; } = new List<string>();
        public int ToolCallCount { get; set; }
    }
}
