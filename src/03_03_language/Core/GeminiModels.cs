using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Language.Core
{
    // Models for the Gemini Interactions API
    // POST https://generativelanguage.googleapis.com/v1beta/interactions

    [JsonObject]
    public class GeminiInteractionRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("input")]
        public object Input { get; set; }

        [JsonProperty("system_instruction", NullValueHandling = NullValueHandling.Ignore)]
        public string SystemInstruction { get; set; }

        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public List<GeminiFunctionToolDef> Tools { get; set; }

        [JsonProperty("generation_config", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> GenerationConfig { get; set; }

        [JsonProperty("response_format", NullValueHandling = NullValueHandling.Ignore)]
        public object ResponseFormat { get; set; }

        [JsonProperty("response_modalities", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> ResponseModalities { get; set; }

        [JsonProperty("previous_interaction_id", NullValueHandling = NullValueHandling.Ignore)]
        public string PreviousInteractionId { get; set; }
    }

    public class GeminiFunctionToolDef
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("parameters")]
        public object Parameters { get; set; }
    }

    public class GeminiInteraction
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("outputs")]
        public List<JObject> Outputs { get; set; }

        [JsonProperty("usage")]
        public JObject Usage { get; set; }
    }

    public class GeminiTextInput
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "text";

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class GeminiAudioInput
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "audio";

        [JsonProperty("mime_type")]
        public string MimeType { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }
    }

    public class GeminiFunctionResult
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "function_result";

        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("is_error", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsError { get; set; }
    }
}
