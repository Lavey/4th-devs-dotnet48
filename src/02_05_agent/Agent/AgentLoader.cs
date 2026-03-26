using System;
using System.IO;

namespace FourthDevs.ContextAgent.Agent
{
    internal class AgentTemplate
    {
        public string Name { get; set; }
        public string Model { get; set; }
        public string SystemPrompt { get; set; }
    }

    internal static class AgentLoader
    {
        public static AgentTemplate Load(string workspaceRoot, string agentName = "alice")
        {
            string path = Path.Combine(workspaceRoot, "agents", agentName + ".agent.md");
            if (!File.Exists(path))
                throw new FileNotFoundException("Agent template not found: " + path);

            string content = File.ReadAllText(path);

            // Parse frontmatter (--- ... ---)
            string name = agentName;
            string model = "gpt-4.1-mini";
            string systemPrompt = content;

            if (content.StartsWith("---"))
            {
                int end = content.IndexOf("---", 3);
                if (end > 0)
                {
                    string frontmatter = content.Substring(3, end - 3);
                    systemPrompt = content.Substring(end + 3).Trim();

                    foreach (string line in frontmatter.Split('\n'))
                    {
                        string l = line.Trim();
                        if (l.StartsWith("name:"))
                            name = l.Substring(5).Trim();
                        else if (l.StartsWith("model:"))
                            model = l.Substring(6).Trim();
                    }
                }
            }

            return new AgentTemplate
            {
                Name = name,
                Model = model,
                SystemPrompt = systemPrompt
            };
        }
    }
}
