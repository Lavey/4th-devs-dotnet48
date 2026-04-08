using Newtonsoft.Json;

namespace FourthDevs.AutoPrompt.Models
{
    public class TraceEntry
    {
        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("stage")]
        public string Stage { get; set; }

        [JsonProperty("request")]
        public TraceRequest Request { get; set; }

        [JsonProperty("response")]
        public TraceResponse Response { get; set; }

        [JsonProperty("durationMs")]
        public long DurationMs { get; set; }
    }

    public class TraceRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("instructions")]
        public string Instructions { get; set; }

        [JsonProperty("input")]
        public string Input { get; set; }

        [JsonProperty("schema", NullValueHandling = NullValueHandling.Ignore)]
        public string Schema { get; set; }
    }

    public class TraceResponse
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("usage", NullValueHandling = NullValueHandling.Ignore)]
        public object Usage { get; set; }
    }
}
