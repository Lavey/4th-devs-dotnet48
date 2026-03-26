using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ContextAgent.Memory
{
    internal class MemoryConfig
    {
        public const int ObservationThresholdTokens = 400;
        public const int ReflectionThresholdTokens = 400;
        public const int ReflectionTargetTokens = 200;
        public const string ObserverModel = "gpt-4.1-mini";
        public const string ReflectorModel = "gpt-4.1-mini";
    }

    internal class MemoryState
    {
        public string ActiveObservations { get; set; } = string.Empty;
        public int LastObservedIndex { get; set; } = 0;
        public int ObservationTokenCount { get; set; } = 0;
        public int GenerationCount { get; set; } = 0;
        public int ObserverLogSeq { get; set; } = 0;
        public int ReflectorLogSeq { get; set; } = 0;
        public bool ObserverRanThisRequest { get; set; } = false;
        public int LastReflectionOutputTokens { get; set; } = 0;
    }

    internal class MemoryContext
    {
        public string SystemPrompt { get; set; }
        public List<JObject> Messages { get; set; }
    }

    internal class Session
    {
        public string Id { get; set; }
        public List<JObject> Messages { get; set; } = new List<JObject>();
        public MemoryState Memory { get; set; } = new MemoryState();
    }
}
