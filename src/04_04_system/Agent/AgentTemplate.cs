using System.Collections.Generic;

namespace FourthDevs.AgentSystem.Agent
{
    /// <summary>
    /// Parsed agent template loaded from a <c>*.md</c> file in workspace/system/agents/.
    /// Uses YAML frontmatter with title, model, tools, description and Markdown body
    /// as the system prompt.
    /// </summary>
    internal sealed class AgentTemplate
    {
        public string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4.1-mini";
        public List<string> Tools { get; set; } = new List<string>();
        public string SystemPrompt { get; set; } = string.Empty;
    }
}
