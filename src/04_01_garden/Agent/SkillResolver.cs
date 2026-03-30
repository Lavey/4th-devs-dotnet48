using System.Collections.Generic;
using System.Text.RegularExpressions;
using FourthDevs.Garden.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Garden.Agent
{
    /// <summary>
    /// Parses /skill-name invocations from user messages and builds
    /// metadata context for the agent.
    /// Port of 04_01_garden/src/agent/skill.ts.
    /// </summary>
    internal static class SkillResolver
    {
        private static readonly Regex SkillPattern =
            new Regex(@"^\/([a-z0-9][a-z0-9-]*)(?:\s+([\s\S]*))?$", RegexOptions.IgnoreCase);

        internal sealed class ResolvedSkillContext
        {
            public List<string> ToolNames { get; set; }
            public string UserMessage { get; set; }
        }

        public static ResolvedSkillContext Resolve(
            string userMessage,
            List<SkillTemplate> skills,
            List<string> defaultToolNames)
        {
            string trimmed = userMessage.Trim();
            Match match = SkillPattern.Match(trimmed);

            SkillTemplate invokedSkill = null;
            string skillName = null;
            string arguments = string.Empty;

            if (match.Success)
            {
                skillName = match.Groups[1].Value.ToLowerInvariant();
                arguments = match.Groups[2].Success ? match.Groups[2].Value.Trim() : string.Empty;
                invokedSkill = FindSkillByName(skills, skillName);
            }

            List<string> toolNames;
            if (invokedSkill != null && invokedSkill.AllowedTools.Count > 0)
                toolNames = invokedSkill.AllowedTools;
            else
                toolNames = defaultToolNames;

            if (invokedSkill == null)
            {
                return new ResolvedSkillContext
                {
                    ToolNames = toolNames,
                    UserMessage = userMessage
                };
            }

            var metadata = new JObject
            {
                ["active_skill"] = new JObject
                {
                    ["name"] = invokedSkill.Name,
                    ["arguments"] = string.IsNullOrEmpty(arguments) ? (JToken)JValue.CreateNull() : arguments,
                    ["allowed_tools"] = new JArray(invokedSkill.AllowedTools.ToArray()),
                    ["runtime_scripts"] = new JArray(invokedSkill.RuntimeScripts.ToArray()),
                    ["argument_hint"] = string.IsNullOrEmpty(invokedSkill.ArgumentHint)
                        ? (JToken)JValue.CreateNull()
                        : invokedSkill.ArgumentHint,
                }
            };

            string metadataBlock = "<metadata>\n" +
                                   metadata.ToString(Formatting.Indented) +
                                   "\n</metadata>";

            string messageWithMetadata = metadataBlock + "\n<user_request>\n" +
                                         userMessage + "\n</user_request>";

            return new ResolvedSkillContext
            {
                ToolNames = toolNames,
                UserMessage = messageWithMetadata
            };
        }

        private static SkillTemplate FindSkillByName(List<SkillTemplate> skills, string name)
        {
            foreach (SkillTemplate skill in skills)
            {
                if (skill.Name != null && skill.Name.ToLowerInvariant() == name)
                    return skill;
            }
            return null;
        }
    }
}
