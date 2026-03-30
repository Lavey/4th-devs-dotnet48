using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FourthDevs.Garden.Agent
{
    using FourthDevs.Garden.Models;

    /// <summary>
    /// Loads agent templates, skills, and workflows from vault/system/.
    /// Port of 04_01_garden/src/agent/template.ts.
    /// </summary>
    internal static class TemplateLoader
    {
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string SystemDir = Path.Combine(BaseDir, "vault", "system");
        private static readonly string WorkflowsDir = Path.Combine(SystemDir, "workflows");
        private static readonly string SkillsDir = Path.Combine(SystemDir, "skills");

        private static readonly HashSet<string> SkillScriptExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".ts", ".js", ".mjs", ".cjs", ".mts", ".cts" };

        public static AgentTemplate LoadTemplate(string agent)
        {
            string filePath = Path.Combine(SystemDir, agent + ".agent.md");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Agent file not found: " + filePath, filePath);

            string raw = File.ReadAllText(filePath, Encoding.UTF8);
            var template = ParseFrontmatter(raw);
            if (string.IsNullOrEmpty(template.Name))
                template.Name = agent;

            string workflows = LoadWorkflows();
            LoadedSkills loadedSkills = LoadSkills();
            template.Skills = loadedSkills.Skills;

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(workflows))
            {
                sb.Append(workflows);
                sb.AppendLine();
                sb.AppendLine();
            }
            if (!string.IsNullOrEmpty(loadedSkills.Section))
            {
                sb.Append(loadedSkills.Section);
                sb.AppendLine();
                sb.AppendLine();
            }
            sb.Append(template.Instructions);
            template.Instructions = sb.ToString().Replace("{{date}}", today);

            return template;
        }

        // ----------------------------------------------------------------
        // Frontmatter parser
        // ----------------------------------------------------------------

        private static AgentTemplate ParseFrontmatter(string raw)
        {
            var template = new AgentTemplate();
            string[] lines = raw.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.None);

            int start = -1;
            int end = -1;
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
                template.Instructions = raw.Trim();
                return template;
            }

            string currentKey = null;
            bool inList = false;
            for (int i = start + 1; i < end; i++)
            {
                string line = lines[i];

                if (line.StartsWith("  - ") || line.StartsWith("- "))
                {
                    string item = line.TrimStart().TrimStart('-').Trim();
                    if (inList && currentKey != null)
                        ApplyListItem(template, currentKey, item);
                    continue;
                }

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

            var sb = new StringBuilder();
            for (int i = end + 1; i < lines.Length; i++)
                sb.AppendLine(lines[i]);
            template.Instructions = sb.ToString().Trim();

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

        // ----------------------------------------------------------------
        // Workflows
        // ----------------------------------------------------------------

        private static string LoadWorkflows()
        {
            if (!Directory.Exists(WorkflowsDir))
                return string.Empty;

            string[] files = Directory.GetFiles(WorkflowsDir, "*.md");
            if (files.Length == 0) return string.Empty;

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var sections = new List<string>();

            foreach (string file in files)
            {
                string raw = File.ReadAllText(file, Encoding.UTF8);
                WorkflowMeta meta = ParseWorkflowFrontmatter(raw);
                string name = !string.IsNullOrEmpty(meta.Name)
                    ? meta.Name
                    : Path.GetFileNameWithoutExtension(file);
                string desc = meta.Description ?? string.Empty;
                sections.Add("### " + name + "\n" + desc + "\n\n" + meta.Content.Trim());
            }

            return "\n\n## Workflows\n\nYou MUST follow a workflow when the user's request matches one. " +
                   "Follow every step exactly \u2014 do not skip saving results.\n\n" +
                   string.Join("\n\n", sections);
        }

        private sealed class WorkflowMeta
        {
            public string Name;
            public string Description;
            public string Content = string.Empty;
        }

        private static WorkflowMeta ParseWorkflowFrontmatter(string raw)
        {
            var meta = new WorkflowMeta();
            string[] lines = raw.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.None);

            int start = -1;
            int end = -1;
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
                meta.Content = raw.Trim();
                return meta;
            }

            for (int i = start + 1; i < end; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                string key = lines[i].Substring(0, colon).Trim().ToLowerInvariant();
                string val = lines[i].Substring(colon + 1).Trim();
                if (key == "name") meta.Name = val;
                else if (key == "description") meta.Description = val;
            }

            var sb = new StringBuilder();
            for (int i = end + 1; i < lines.Length; i++)
                sb.AppendLine(lines[i]);
            meta.Content = sb.ToString().Trim();
            return meta;
        }

        // ----------------------------------------------------------------
        // Skills
        // ----------------------------------------------------------------

        private sealed class LoadedSkills
        {
            public string Section = string.Empty;
            public List<SkillTemplate> Skills = new List<SkillTemplate>();
        }

        private static LoadedSkills LoadSkills()
        {
            var result = new LoadedSkills();
            if (!Directory.Exists(SkillsDir))
                return result;

            var skillFiles = new List<string>();
            CollectSkillFiles(SkillsDir, skillFiles);
            if (skillFiles.Count == 0) return result;

            skillFiles.Sort(StringComparer.OrdinalIgnoreCase);

            var skills = new List<SkillTemplate>();
            foreach (string file in skillFiles)
            {
                string raw = File.ReadAllText(file, Encoding.UTF8);
                SkillTemplate skill = ParseSkillFrontmatter(raw, file);
                skill.RuntimeScripts = CollectRuntimeScripts(Path.GetDirectoryName(file), skill);
                skills.Add(skill);
            }

            if (skills.Count == 0) return result;

            var sections = new List<string>();
            foreach (SkillTemplate skill in skills)
                sections.Add(FormatSkill(skill));

            result.Skills = skills;
            result.Section =
                "\n\n## Skills\n\nYou have skills available from vault/system/skills.\n" +
                "Select and apply a skill when its description matches the user's request.\n" +
                "If the user explicitly invokes \"/<skill-name>\", prioritize that skill.\n" +
                "For skills with disable-model-invocation=true, only use them when explicitly invoked.\n" +
                "When a skill is selected, follow its instructions exactly.\n" +
                "If a skill provides runtime scripts, prefer executing them via code_mode \"script_path\" " +
                "instead of rewriting the same logic inline.\n\n" +
                string.Join("\n\n", sections);
            return result;
        }

        private static void CollectSkillFiles(string dir, List<string> files)
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                if (Path.GetFileName(file) == "SKILL.md")
                    files.Add(file);
            }
            foreach (string subDir in Directory.GetDirectories(dir))
                CollectSkillFiles(subDir, files);
        }

        private static SkillTemplate ParseSkillFrontmatter(string raw, string filePath)
        {
            var skill = new SkillTemplate();
            string[] lines = raw.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.None);

            int start = -1;
            int end = -1;
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
                skill.Instructions = raw.Trim();
                skill.Name = Path.GetFileName(Path.GetDirectoryName(filePath));
                return skill;
            }

            string currentKey = null;
            bool inList = false;
            for (int i = start + 1; i < end; i++)
            {
                string line = lines[i];

                if (line.StartsWith("  - ") || line.StartsWith("- "))
                {
                    string item = line.TrimStart().TrimStart('-').Trim();
                    if (inList && currentKey != null)
                        ApplySkillListItem(skill, currentKey, item);
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    currentKey = line.Substring(0, colon).Trim();
                    string value = line.Substring(colon + 1).Trim();
                    inList = string.IsNullOrEmpty(value);
                    if (!inList)
                        ApplySkillScalar(skill, currentKey, value);
                }
            }

            var sb = new StringBuilder();
            for (int i = end + 1; i < lines.Length; i++)
                sb.AppendLine(lines[i]);
            skill.Instructions = sb.ToString().Trim();

            if (string.IsNullOrEmpty(skill.Name))
                skill.Name = Path.GetFileName(Path.GetDirectoryName(filePath));

            // Compute relative path from system dir
            try
            {
                string fullSkill = Path.GetFullPath(filePath);
                string fullSystem = Path.GetFullPath(SystemDir);
                if (fullSkill.StartsWith(fullSystem))
                {
                    skill.RelativePath = fullSkill.Substring(fullSystem.Length)
                        .TrimStart(Path.DirectorySeparatorChar)
                        .Replace('\\', '/');
                }
            }
            catch { /* ignore */ }

            return skill;
        }

        private static void ApplySkillScalar(SkillTemplate s, string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "name":
                    s.Name = value;
                    break;
                case "description":
                    s.Description = value;
                    break;
                case "argument-hint":
                    s.ArgumentHint = value;
                    break;
                case "disable-model-invocation":
                    s.DisableModelInvocation = ParseBool(value, false);
                    break;
                case "user-invocable":
                    s.UserInvocable = ParseBool(value, true);
                    break;
                case "runtime-script":
                    s.RuntimeScripts.Add(value.Trim());
                    break;
                case "allowed-tools":
                    foreach (string item in value.Split(','))
                    {
                        string trimmed = item.Trim();
                        if (trimmed.Length > 0) s.AllowedTools.Add(trimmed);
                    }
                    break;
            }
        }

        private static void ApplySkillListItem(SkillTemplate s, string key, string item)
        {
            switch (key.ToLowerInvariant())
            {
                case "allowed-tools":
                    s.AllowedTools.Add(item);
                    break;
                case "runtime-scripts":
                    s.RuntimeScripts.Add(item);
                    break;
            }
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            string lower = value.ToLowerInvariant();
            if (lower == "true" || lower == "yes") return true;
            if (lower == "false" || lower == "no") return false;
            return fallback;
        }

        private static List<string> CollectRuntimeScripts(string skillDir, SkillTemplate skill)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add declared scripts, resolving paths
            foreach (string declared in skill.RuntimeScripts)
            {
                string resolved = ResolveRuntimeScriptPath(skillDir, declared);
                if (resolved != null)
                    result.Add(resolved);
            }

            // Discover scripts from scripts/ subdirectory
            string scriptsDir = Path.Combine(skillDir, "scripts");
            if (Directory.Exists(scriptsDir))
                DiscoverScripts(scriptsDir, result);

            var sorted = new List<string>(result);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            return sorted;
        }

        private static void DiscoverScripts(string dir, HashSet<string> collected)
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(file);
                if (SkillScriptExtensions.Contains(ext))
                {
                    string relative = MakeVaultRelative(file);
                    if (relative != null)
                        collected.Add(relative);
                }
            }
            foreach (string subDir in Directory.GetDirectories(dir))
                DiscoverScripts(subDir, collected);
        }

        private static string ResolveRuntimeScriptPath(string skillDir, string value)
        {
            string trimmed = value.Trim();
            if (string.IsNullOrEmpty(trimmed)) return null;

            string absoluteCandidate;
            if (trimmed.StartsWith("vault/system/skills/"))
                absoluteCandidate = Path.Combine(BaseDir, trimmed.Replace('/', Path.DirectorySeparatorChar));
            else
                absoluteCandidate = Path.Combine(skillDir, trimmed.Replace('/', Path.DirectorySeparatorChar));

            return MakeVaultRelative(absoluteCandidate);
        }

        private static string MakeVaultRelative(string absolutePath)
        {
            try
            {
                string full = Path.GetFullPath(absolutePath);
                string baseNorm = Path.GetFullPath(BaseDir);
                if (!full.StartsWith(baseNorm)) return null;

                string relative = full.Substring(baseNorm.Length)
                    .TrimStart(Path.DirectorySeparatorChar)
                    .Replace('\\', '/');

                if (!relative.StartsWith("vault/system/skills/")) return null;
                if (!relative.Contains("/scripts/")) return null;

                string ext = Path.GetExtension(full);
                if (!SkillScriptExtensions.Contains(ext)) return null;

                return relative;
            }
            catch
            {
                return null;
            }
        }

        private static string FormatSkill(SkillTemplate skill)
        {
            var metadata = new List<string>();
            metadata.Add("source: " + skill.RelativePath);
            metadata.Add("description: " + (string.IsNullOrEmpty(skill.Description) ? "(none)" : skill.Description));
            metadata.Add("disable-model-invocation: " + skill.DisableModelInvocation.ToString().ToLowerInvariant());
            metadata.Add("user-invocable: " + skill.UserInvocable.ToString().ToLowerInvariant());

            if (!string.IsNullOrEmpty(skill.ArgumentHint))
                metadata.Add("argument-hint: " + skill.ArgumentHint);

            if (skill.AllowedTools.Count > 0)
                metadata.Add("allowed-tools: " + string.Join(", ", skill.AllowedTools));

            if (skill.RuntimeScripts.Count > 0)
                metadata.Add("runtime-scripts: " + string.Join(", ", skill.RuntimeScripts));

            var sb = new StringBuilder();
            sb.AppendLine("### " + skill.Name);
            foreach (string line in metadata)
                sb.AppendLine("- " + line);
            sb.AppendLine();
            sb.Append(skill.Instructions);
            return sb.ToString();
        }
    }
}
