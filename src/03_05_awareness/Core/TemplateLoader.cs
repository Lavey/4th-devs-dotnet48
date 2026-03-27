using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FourthDevs.Awareness.Models;

namespace FourthDevs.Awareness.Core
{
    internal static class TemplateLoader
    {
        public static AgentTemplate Load(string filePath)
        {
            string content = File.ReadAllText(filePath);
            var template = new AgentTemplate();

            var match = Regex.Match(content, @"^---\r?\n(.*?)\r?\n---\r?\n?(.*)", RegexOptions.Singleline);
            if (!match.Success)
            {
                template.SystemPrompt = content.Trim();
                return template;
            }

            string frontmatter = match.Groups[1].Value;
            string body = match.Groups[2].Value.Trim();
            template.SystemPrompt = body;

            var nameMatch = Regex.Match(frontmatter, @"^name:\s*(.+)$", RegexOptions.Multiline);
            if (nameMatch.Success) template.Name = nameMatch.Groups[1].Value.Trim();

            var modelMatch = Regex.Match(frontmatter, @"^model:\s*(.+)$", RegexOptions.Multiline);
            if (modelMatch.Success) template.Model = modelMatch.Groups[1].Value.Trim();

            var toolsSection = Regex.Match(frontmatter, @"^tools:\s*\r?\n((?:\s+-\s*.+\r?\n?)+)", RegexOptions.Multiline);
            if (toolsSection.Success)
            {
                string toolsBlock = toolsSection.Groups[1].Value;
                var toolMatches = Regex.Matches(toolsBlock, @"^\s+-\s*(.+)$", RegexOptions.Multiline);
                template.Tools = new List<string>();
                foreach (Match m in toolMatches)
                    template.Tools.Add(m.Groups[1].Value.Trim());
            }

            return template;
        }
    }
}
