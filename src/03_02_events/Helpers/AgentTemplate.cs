using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FourthDevs.Events.Config;
using FourthDevs.Events.Models;

namespace FourthDevs.Events.Helpers
{
    /// <summary>
    /// Loads agent templates from .agent.md files in the workspace/agents/ directory.
    /// </summary>
    internal static class AgentTemplateHelper
    {
        public static AgentTemplate LoadFromWorkspace(string agentName)
        {
            string agentsDir = EnvConfig.AgentsPath;
            string filePath = Path.Combine(agentsDir, agentName + ".agent.md");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Agent template not found: " + filePath);

            string content = File.ReadAllText(filePath, Encoding.UTF8);
            return ParseAgentTemplate(content, agentName);
        }

        public static List<AgentTemplate> LoadAll()
        {
            string agentsDir = EnvConfig.AgentsPath;
            var templates = new List<AgentTemplate>();

            if (!Directory.Exists(agentsDir)) return templates;

            foreach (string file in Directory.GetFiles(agentsDir, "*.agent.md"))
            {
                string name = Path.GetFileName(file).Replace(".agent.md", "");
                try
                {
                    string content = File.ReadAllText(file, Encoding.UTF8);
                    templates.Add(ParseAgentTemplate(content, name));
                }
                catch (Exception ex)
                {
                    Core.Logger.Warn("template", "Failed to load " + name + ": " + ex.Message);
                }
            }

            return templates;
        }

        private static AgentTemplate ParseAgentTemplate(string content, string fallbackName)
        {
            var template = new AgentTemplate { Name = fallbackName, Model = "gpt-4.1" };
            var frontmatter = FrontmatterParser.Parse(content);

            if (frontmatter.Fields.ContainsKey("name"))
                template.Name = frontmatter.Fields["name"];
            if (frontmatter.Fields.ContainsKey("model"))
                template.Model = frontmatter.Fields["model"];
            if (frontmatter.Fields.ContainsKey("tools"))
                template.Tools = FrontmatterParser.ParseList(frontmatter.Fields["tools"]);
            if (frontmatter.Fields.ContainsKey("capabilities"))
                template.Capabilities = FrontmatterParser.ParseList(frontmatter.Fields["capabilities"]);

            template.SystemPrompt = frontmatter.Body.Trim();
            return template;
        }
    }
}
