using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FourthDevs.Artifacts.Models;

namespace FourthDevs.Artifacts.Core
{
    internal class ArtifactEditResult
    {
        public ArtifactDocument Artifact { get; set; }
        public List<string> Reports { get; set; }
    }

    internal static class ArtifactEditor
    {
        /// <summary>
        /// Applies search/replace operations to the artifact HTML.
        /// </summary>
        public static ArtifactEditResult EditWithSearchReplace(
            ArtifactDocument artifact,
            List<SearchReplaceOperation> replacements,
            string instructions,
            string title)
        {
            if (artifact == null)
                throw new ArgumentNullException("artifact");
            if (replacements == null)
                throw new ArgumentNullException("replacements");

            string html = artifact.Html ?? string.Empty;
            var reports = new List<string>();

            foreach (var op in replacements)
            {
                if (string.IsNullOrEmpty(op.Search))
                {
                    reports.Add("SKIP (empty search term)");
                    continue;
                }

                string before = html;
                int matchCount;

                if (op.UseRegex)
                {
                    RegexOptions options = RegexOptions.None;

                    // caseSensitive defaults to true for regex
                    bool caseSensitive = !op.CaseSensitive.HasValue || op.CaseSensitive.Value;
                    if (!caseSensitive)
                        options |= RegexOptions.IgnoreCase;

                    // Additional flags
                    if (!string.IsNullOrEmpty(op.RegexFlags))
                    {
                        if (op.RegexFlags.IndexOf('m') >= 0)
                            options |= RegexOptions.Multiline;
                        if (op.RegexFlags.IndexOf('s') >= 0)
                            options |= RegexOptions.Singleline;
                        if (op.RegexFlags.IndexOf('i') >= 0)
                            options |= RegexOptions.IgnoreCase;
                    }

                    var regex = new Regex(op.Search, options);
                    matchCount = regex.Matches(html).Count;

                    html = op.ReplaceAll
                        ? regex.Replace(html, op.Replace ?? string.Empty)
                        : regex.Replace(html, op.Replace ?? string.Empty, 1);
                }
                else
                {
                    // Literal string replacement
                    StringComparison comparison = (op.CaseSensitive.HasValue && !op.CaseSensitive.Value)
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal;

                    matchCount = CountOccurrences(html, op.Search, comparison);

                    html = op.ReplaceAll
                        ? ReplaceAll(html, op.Search, op.Replace ?? string.Empty, comparison)
                        : ReplaceFirst(html, op.Search, op.Replace ?? string.Empty, comparison);
                }

                int replacements_made = op.ReplaceAll ? matchCount : Math.Min(matchCount, 1);
                reports.Add(string.Format(
                    "'{0}' → {1} match(es), {2} replaced",
                    Truncate(op.Search, 40),
                    matchCount,
                    replacements_made));
            }

            var updated = new ArtifactDocument
            {
                Id = artifact.Id,
                Title = !string.IsNullOrWhiteSpace(title) ? title : artifact.Title,
                Prompt = !string.IsNullOrWhiteSpace(instructions) ? instructions : artifact.Prompt,
                Html = html,
                Model = artifact.Model,
                Packs = artifact.Packs,
                CreatedAt = artifact.CreatedAt
            };

            return new ArtifactEditResult { Artifact = updated, Reports = reports };
        }

        private static int CountOccurrences(string source, string search, StringComparison comparison)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(search, index, comparison)) >= 0)
            {
                count++;
                index += search.Length;
            }
            return count;
        }

        private static string ReplaceFirst(string source, string search, string replacement, StringComparison comparison)
        {
            int index = source.IndexOf(search, comparison);
            if (index < 0)
                return source;
            return source.Substring(0, index) + replacement + source.Substring(index + search.Length);
        }

        private static string ReplaceAll(string source, string search, string replacement, StringComparison comparison)
        {
            if (comparison == StringComparison.Ordinal)
                return source.Replace(search, replacement);

            // Case-insensitive literal replace-all
            var sb = new StringBuilder();
            int index = 0;
            int prev = 0;
            while ((index = source.IndexOf(search, prev, comparison)) >= 0)
            {
                sb.Append(source, prev, index - prev);
                sb.Append(replacement);
                prev = index + search.Length;
            }
            sb.Append(source, prev, source.Length - prev);
            return sb.ToString();
        }

        private static string Truncate(string s, int max)
        {
            if (s == null) return string.Empty;
            return s.Length > max ? s.Substring(0, max) + "..." : s;
        }
    }
}
