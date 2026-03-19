using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FourthDevs.Ops.Agent
{
    /// <summary>
    /// Loads an agent template from a <c>*.agent.md</c> file.
    /// The file uses a simple YAML-like frontmatter block delimited by <c>---</c> lines,
    /// followed by the system prompt as plain Markdown.
    /// </summary>
    internal static class AgentLoader
    {
        private static readonly string AgentsDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace", "agents");

        /// <summary>
        /// Reads and parses <c>workspace/agents/{name}.agent.md</c>.
        /// </summary>
        public static AgentTemplate Load(string name)
        {
            string filePath = Path.Combine(AgentsDir, name + ".agent.md");
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Agent file not found: {filePath}", filePath);

            string raw = File.ReadAllText(filePath, Encoding.UTF8);
            return Parse(name, raw);
        }

        // ----------------------------------------------------------------
        // Parser
        // ----------------------------------------------------------------

        private static AgentTemplate Parse(string defaultName, string raw)
        {
            // Split on lines
            string[] lines = raw.Replace("\r\n", "\n").Split('\n');

            var template = new AgentTemplate { Name = defaultName };

            // Find first and second "---" delimiters
            int start = -1;
            int end   = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimEnd() == "---")
                {
                    if (start < 0) { start = i; continue; }
                    end = i;
                    break;
                }
            }

            if (start < 0 || end < 0)
            {
                // No frontmatter – treat entire file as system prompt
                template.SystemPrompt = raw.Trim();
                return template;
            }

            // Parse frontmatter
            string currentKey = null;
            bool inList = false;
            for (int i = start + 1; i < end; i++)
            {
                string line = lines[i];

                // List item
                if (line.StartsWith("  - ") || line.StartsWith("- "))
                {
                    string item = line.TrimStart().TrimStart('-').Trim();
                    if (inList && currentKey != null)
                        ApplyListItem(template, currentKey, item);
                    continue;
                }

                // Key: value
                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    currentKey = line.Substring(0, colon).Trim();
                    string value = line.Substring(colon + 1).Trim();
                    inList = string.IsNullOrEmpty(value);
                    if (!inList)
                        ApplyScalar(template, currentKey, value);
                }
            }

            // System prompt = everything after closing ---
            var sb = new StringBuilder();
            for (int i = end + 1; i < lines.Length; i++)
            {
                sb.AppendLine(lines[i]);
            }
            template.SystemPrompt = sb.ToString().Trim();

            return template;
        }

        private static void ApplyScalar(AgentTemplate t, string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "name":  t.Name  = value; break;
                case "model": t.Model = value; break;
            }
        }

        private static void ApplyListItem(AgentTemplate t, string key, string item)
        {
            if (key.ToLowerInvariant() == "tools")
                t.Tools.Add(item);
        }
    }
}
