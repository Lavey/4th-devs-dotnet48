using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FourthDevs.VideoGeneration.Models
{
    /// <summary>
    /// Represents a single tool definition for the OpenAI Responses API.
    /// </summary>
    internal class VideoGenToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject Parameters { get; set; }
        public Func<JObject, Task<object>> Handler { get; set; }
    }
}
