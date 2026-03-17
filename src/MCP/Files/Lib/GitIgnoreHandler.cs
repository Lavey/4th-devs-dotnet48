using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace FourthDevs.Mcp.Files.Lib
{
    internal class GitIgnoreHandler
    {
        private readonly Dictionary<string, List<IgnoreRule>> _rulesByDir =
            new Dictionary<string, List<IgnoreRule>>(StringComparer.OrdinalIgnoreCase);

        private class IgnoreRule
        {
            public bool Negated { get; set; }
            public bool DirectoryOnly { get; set; }
            public Regex Pattern { get; set; }
            public string RawPattern { get; set; }
        }

        /// <summary>
        /// Load .gitignore patterns from a directory (not recursive).
        /// </summary>
        public void LoadFromDirectory(string directory)
        {
            if (_rulesByDir.ContainsKey(directory)) return;

            var rules = new List<IgnoreRule>();
            string gitignorePath = Path.Combine(directory, ".gitignore");
            if (File.Exists(gitignorePath))
            {
                foreach (var line in File.ReadAllLines(gitignorePath))
                    TryAddRule(line, rules);
            }
            _rulesByDir[directory] = rules;
        }

        /// <summary>
        /// Check whether a path is ignored, loading .gitignore files from
        /// the directory and all its ancestors up to the base directory.
        /// </summary>
        public bool IsIgnored(string filePath, string baseDirectory)
        {
            string dir = File.Exists(filePath)
                ? Path.GetDirectoryName(filePath)
                : filePath;

            // Collect directories from basedir down to the file's directory
            var dirs = new List<string>();
            string current = dir;
            while (!string.IsNullOrEmpty(current) &&
                   current.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                dirs.Add(current);
                string parent = Path.GetDirectoryName(current);
                if (parent == current) break;
                current = parent;
            }

            // Process from outermost to innermost (negations work correctly)
            dirs.Reverse();
            bool ignored = false;
            foreach (var d in dirs)
            {
                LoadFromDirectory(d);
                if (_rulesByDir.TryGetValue(d, out var rules))
                {
                    string relativePath = GetRelative(filePath, d);
                    bool isDir = Directory.Exists(filePath);
                    foreach (var rule in rules)
                    {
                        if (rule.DirectoryOnly && !isDir) continue;
                        if (rule.Pattern.IsMatch(relativePath))
                        {
                            ignored = !rule.Negated;
                        }
                    }
                }
            }
            return ignored;
        }

        private void TryAddRule(string line, List<IgnoreRule> rules)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                return;

            bool negated = line.StartsWith("!");
            if (negated) line = line.Substring(1);

            bool dirOnly = line.EndsWith("/");
            if (dirOnly) line = line.TrimEnd('/');

            line = line.Trim();
            if (string.IsNullOrEmpty(line)) return;

            string regexPattern = GitPatternToRegex(line);
            try
            {
                rules.Add(new IgnoreRule
                {
                    Negated = negated,
                    DirectoryOnly = dirOnly,
                    Pattern = new Regex(regexPattern, RegexOptions.IgnoreCase),
                    RawPattern = line
                });
            }
            catch
            {
                // Skip invalid patterns
            }
        }

        private static string GitPatternToRegex(string pattern)
        {
            // Escape everything except * and ?
            var sb = new System.Text.StringBuilder();
            bool anchored = pattern.StartsWith("/");
            if (anchored) pattern = pattern.Substring(1);

            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
                {
                    sb.Append(".*");
                    i++; // skip next *
                    if (i + 1 < pattern.Length && pattern[i + 1] == '/')
                        i++; // skip trailing /
                }
                else if (c == '*')
                {
                    sb.Append("[^/]*");
                }
                else if (c == '?')
                {
                    sb.Append("[^/]");
                }
                else
                {
                    sb.Append(Regex.Escape(c.ToString()));
                }
            }

            string regex = sb.ToString();
            if (anchored)
                return "^" + regex + "(/.*)?$";
            return "(^|.*/?)" + regex + "(/.*)?$";
        }

        private static string GetRelative(string path, string basePath)
        {
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;
            if (path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return path.Substring(basePath.Length).Replace('\\', '/');
            return path.Replace('\\', '/');
        }
    }
}
