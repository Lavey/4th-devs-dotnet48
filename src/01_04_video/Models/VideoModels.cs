using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Video.Models
{
    /// <summary>
    /// Represents a single tool definition for the OpenAI Responses API.
    /// </summary>
    internal class VideoToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject Parameters { get; set; }
        public Func<JObject, Task<object>> Handler { get; set; }
    }

    /// <summary>
    /// Result returned from a single agent run.
    /// </summary>
    internal class AgentRunResult
    {
        public string Text { get; set; } = string.Empty;
        public int Turns { get; set; }
    }
}
