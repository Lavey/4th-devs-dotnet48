using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.Lesson07_Chunking.Strategies
{
    /// <summary>
    /// A single chunk produced by any strategy, with attached metadata.
    /// </summary>
    internal class Chunk
    {
        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("metadata")]
        public Dictionary<string, object> Metadata { get; set; }
            = new Dictionary<string, object>();
    }
}
