using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FourthDevs.Events.Helpers
{
    /// <summary>
    /// Path safety and slugification helpers.
    /// </summary>
    internal static class PathHelper
    {
        private static readonly Regex _slugRegex = new Regex(@"[^a-z0-9\-]", RegexOptions.Compiled);

        public static string Slugify(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "untitled";
            string slug = input.ToLowerInvariant().Trim();
            slug = slug.Replace(' ', '-');
            slug = _slugRegex.Replace(slug, "");
            slug = Regex.Replace(slug, @"-{2,}", "-").Trim('-');
            return string.IsNullOrEmpty(slug) ? "untitled" : slug;
        }

        public static string SafePath(string basePath, string relativePath)
        {
            string combined = Path.Combine(basePath, relativePath);
            string full = Path.GetFullPath(combined);
            string fullBase = Path.GetFullPath(basePath);
            if (!full.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path escapes workspace: " + relativePath);
            return full;
        }
    }
}
