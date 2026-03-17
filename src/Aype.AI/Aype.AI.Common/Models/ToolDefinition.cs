using Newtonsoft.Json;

namespace Aype.AI.Common.Models
{
    // -------------------------------------------------------------------------
    // ToolDefinition — schema for a function tool exposed to the model.
    // Kept separate because it is referenced by both ApiClient and Tools projects.
    // -------------------------------------------------------------------------

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
}
