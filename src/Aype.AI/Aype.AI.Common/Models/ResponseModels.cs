using System.Collections.Generic;
using Aype.AI.Common.Models.Enums;
using Newtonsoft.Json;

namespace Aype.AI.Common.Models
{
    // -------------------------------------------------------------------------
    // Responses API request / response models
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

        [JsonProperty("instructions", NullValueHandling = NullValueHandling.Ignore)]
        public string Instructions { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public TextOptions Text { get; set; }

        [JsonProperty("reasoning", NullValueHandling = NullValueHandling.Ignore)]
        public ReasoningOptions Reasoning { get; set; }
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
        public OutputItemType Type { get; set; }

        [JsonProperty("role", NullValueHandling = NullValueHandling.Ignore)]
        public string Role { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("call_id", NullValueHandling = NullValueHandling.Ignore)]
        public string CallId { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("arguments", NullValueHandling = NullValueHandling.Ignore)]
        public string Arguments { get; set; }

        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
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
