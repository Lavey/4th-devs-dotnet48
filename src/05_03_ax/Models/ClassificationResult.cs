using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.AxClassifier.Models
{
    public class ClassificationResult
    {
        [JsonProperty("labels")]
        public List<string> Labels { get; set; } = new List<string>();

        [JsonProperty("priority")]
        public string Priority { get; set; }

        [JsonProperty("needsReply")]
        public bool NeedsReply { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }
    }
}
