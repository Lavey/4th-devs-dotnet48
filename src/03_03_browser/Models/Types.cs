using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Browser.Models
{
    internal class LocalToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject Parameters { get; set; }
        public Func<JObject, Task<object>> Handler { get; set; }
    }

    internal class AgentRunResult
    {
        public string Text { get; set; } = string.Empty;
        public string ResponseId { get; set; }
        public int Turns { get; set; }
    }
}
