using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FourthDevs.Lesson07_Chunking.Strategies
{
    /// <summary>
    /// Utilities for detecting heading positions in Markdown text and finding
    /// the section a chunk belongs to.
    ///
    /// Mirrors 02_02_chunking/src/utils.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class MarkdownUtils
    {
        internal struct Heading
        {
            internal int    Position { get; set; }
            internal int    Level    { get; set; }
            internal string Title    { get; set; }
        }

        // ----------------------------------------------------------------
        // BuildHeadingIndex
        // ----------------------------------------------------------------

        /// <summary>
        /// Builds a sorted list of headings detected in the given Markdown text.
        /// Detects both markdown <c>#</c> headings and plain-text headings
        /// (short standalone lines followed immediately by content).
        /// </summary>
        internal static List<Heading> BuildHeadingIndex(string text)
        {
            var headings = new List<Heading>();
            var mdTitles = new HashSet<string>();

            // 1. Markdown # headings
            var mdRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);
            foreach (Match m in mdRegex.Matches(text))
            {
                string title = m.Groups[2].Value.Trim();
                headings.Add(new Heading
                {
                    Position = m.Index,
                    Level    = m.Groups[1].Length,
                    Title    = title
                });
                mdTitles.Add(title);
            }

            // 2. Plain-text headings: short line after blank line (or start),
            //    followed by content on the very next line.
            var plainRegex = new Regex(@"(?:^|\n\n)([^\n]{1,80})\n(?=[A-Za-z""'\[(])",
                RegexOptions.Multiline);
            foreach (Match m in plainRegex.Matches(text))
            {
                string title = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(title) ||
                    title == "Conclusion:" ||
                    mdTitles.Contains(title))
                    continue;

                int offset = m.Value.StartsWith("\n") ? 2 : 0;
                headings.Add(new Heading
                {
                    Position = m.Index + offset,
                    Level    = 1,
                    Title    = title
                });
            }

            headings.Sort((a, b) => a.Position.CompareTo(b.Position));
            return headings;
        }

        // ----------------------------------------------------------------
        // FindSection
        // ----------------------------------------------------------------

        /// <summary>
        /// Finds the most recent heading for a chunk based on its position in
        /// the source text.  Samples from the middle of the chunk to avoid
        /// overlap-related false matches.
        /// </summary>
        internal static string FindSection(
            string text, string chunkContent, List<Heading> headings)
        {
            if (headings.Count == 0) return null;

            int mid    = (int)(chunkContent.Length * 0.4);
            int len    = System.Math.Min(100, chunkContent.Length - mid);
            if (len <= 0) return null;

            string sample = chunkContent.Substring(mid, len);
            int pos = text.IndexOf(sample, System.StringComparison.Ordinal);
            if (pos == -1) return null;

            Heading? current = null;
            foreach (var h in headings)
            {
                if (h.Position <= pos)
                    current = h;
                else
                    break;
            }

            if (current == null) return null;
            return new string('#', current.Value.Level) + " " + current.Value.Title;
        }
    }
}
