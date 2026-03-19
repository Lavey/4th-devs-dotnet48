using System.Collections.Generic;

namespace FourthDevs.Ops.Agent
{
    /// <summary>
    /// Parsed agent template loaded from a <c>*.agent.md</c> file.
    /// </summary>
    internal sealed class AgentTemplate
    {
        public string Name { get; set; }
        public string Model { get; set; } = "gpt-4.1-mini";
        public List<string> Tools { get; set; } = new List<string>();
        public string SystemPrompt { get; set; } = string.Empty;
    }
}
