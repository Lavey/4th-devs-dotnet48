using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Awareness.Models
{
    public class Message
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class AgentTemplate
    {
        public string Name { get; set; }
        public string Model { get; set; } = "gpt-4.1";
        public string SystemPrompt { get; set; }
        public List<string> Tools { get; set; } = new List<string>();
    }

    public class Session
    {
        public string Id { get; set; }
        public List<Message> Messages { get; set; } = new List<Message>();
        public int Turns { get; set; }
        public string LastResponseId { get; set; }
        public string ScoutLastResponseId { get; set; }
    }

    public class ChatLogEntry
    {
        public string At { get; set; }
        public string SessionId { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class WeatherSnapshot
    {
        public string Location { get; set; }
        public string Summary { get; set; }
        public double? TemperatureC { get; set; }
        public string ObservedAt { get; set; }
        public string Source { get; set; }
    }

    public class AgentResponse
    {
        public string Text { get; set; } = string.Empty;
        public bool UsedTool { get; set; }
    }

    public class LocalToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject Parameters { get; set; }
        public Func<JObject, Task<object>> Handler { get; set; }
    }
}
