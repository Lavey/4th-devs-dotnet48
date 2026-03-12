using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.Common.Models
{
    // -------------------------------------------------------------------------
    // Shared request / response model classes for the OpenAI Responses API
    // https://platform.openai.com/docs/api-reference/responses
    // -------------------------------------------------------------------------

    public class ResponsesRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("input")]
        public List<InputMessage> Input { get; set; } = new List<InputMessage>();

        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public List<ToolDefinition> Tools { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public TextOptions Text { get; set; }

        [JsonProperty("reasoning", NullValueHandling = NullValueHandling.Ignore)]
        public ReasoningOptions Reasoning { get; set; }
    }

    public class InputMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "message";

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public object Content { get; set; }
    }

    public class TextOptions
    {
        [JsonProperty("format")]
        public object Format { get; set; }
    }

    public class ReasoningOptions
    {
        [JsonProperty("effort")]
        public string Effort { get; set; } = "medium";
    }

    // ---- Tool / Function calling ------------------------------------------

    public class ToolDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("parameters")]
        public object Parameters { get; set; }

        [JsonProperty("strict", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Strict { get; set; }
    }

    public class ToolCallInput
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }
    }

    // ---- Response models --------------------------------------------------

    public class ResponsesResponse
    {
        [JsonProperty("output_text")]
        public string OutputText { get; set; }

        [JsonProperty("output")]
        public List<OutputItem> Output { get; set; } = new List<OutputItem>();

        [JsonProperty("usage")]
        public UsageInfo Usage { get; set; }

        [JsonProperty("error")]
        public ApiError Error { get; set; }
    }

    public class OutputItem
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }

        [JsonProperty("content")]
        public List<ContentPart> Content { get; set; }
    }

    public class ContentPart
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class UsageInfo
    {
        [JsonProperty("input_tokens")]
        public int InputTokens { get; set; }

        [JsonProperty("output_tokens")]
        public int OutputTokens { get; set; }

        [JsonProperty("output_tokens_details")]
        public OutputTokenDetails OutputTokensDetails { get; set; }
    }

    public class OutputTokenDetails
    {
        [JsonProperty("reasoning_tokens")]
        public int ReasoningTokens { get; set; }
    }

    public class ApiError
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
