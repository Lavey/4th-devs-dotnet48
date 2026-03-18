using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FourthDevs.Lesson08_GraphAgents.Graph
{
    /// <summary>
    /// Separator-based (recursive) text chunking.
    /// Splits using a hierarchy: headers → paragraphs → sentences → words.
    /// Mirrors 02_03_graph_agents/src/graph/chunking.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Chunking
    {
        private const int ChunkSize    = 4000;
        private const int ChunkOverlap = 500;

        private static readonly string[] Separators =
        {
            "\n## ", "\n### ", "\n\n", "\n", ". ", " "
        };

        // ── Heading index ──────────────────────────────────────────────────

        internal struct Heading
        {
            internal int    Position;
            internal int    Level;
            internal string Title;
        }

        internal static List<Heading> BuildHeadingIndex(string text)
        {
            var headings = new List<Heading>();

            // Markdown headings
            var mdRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);
            foreach (Match m in mdRegex.Matches(text))
            {
                headings.Add(new Heading
                {
                    Position = m.Index,
                    Level    = m.Groups[1].Value.Length,
                    Title    = m.Groups[2].Value.Trim()
                });
            }

            // Plain-text headings (heuristic: short lines followed by content)
            var mdTitles = new HashSet<string>();
            foreach (var h in headings) mdTitles.Add(h.Title);

            var plainRegex = new Regex(@"(?:^|\n\n)([^\n]{1,80})\n(?=[A-Za-z""'\[(])");
            foreach (Match m in plainRegex.Matches(text))
            {
                string title = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(title) || title == "Conclusion:" || mdTitles.Contains(title))
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

        internal static string FindSection(string text, string chunkContent, List<Heading> headings)
        {
            if (headings.Count == 0) return null;

            int mid    = chunkContent.Length * 2 / 5;
            string sample = chunkContent.Substring(mid, Math.Min(100, chunkContent.Length - mid));
            int pos    = text.IndexOf(sample, StringComparison.Ordinal);
            if (pos == -1) return null;

            Heading? current = null;
            foreach (var h in headings)
            {
                if (h.Position <= pos) current = h;
                else break;
            }

            if (!current.HasValue) return null;
            return new string('#', current.Value.Level) + " " + current.Value.Title;
        }

        // ── Overlap helper ─────────────────────────────────────────────────

        private static string PickOverlap(string text, int overlap, string sep)
        {
            if (overlap <= 0) return string.Empty;

            int start = Math.Max(0, text.Length - overlap);
            string tail = text.Substring(start);

            int idx = tail.IndexOf('\n');
            if (idx == -1) idx = Array.FindIndex(tail.ToCharArray(), c => char.IsWhiteSpace(c));
            if (idx == -1) return string.Empty;

            string overlapText = text.Substring(start + idx + 1);
            if (sep != null && overlapText.StartsWith(sep))
                overlapText = overlapText.Substring(sep.Length);

            return overlapText;
        }

        // ── Recursive split ────────────────────────────────────────────────

        private static List<string> Split(
            string text, int size, int overlap, string[] separators, int sepIndex = 0)
        {
            if (text.Length <= size) return new List<string> { text };

            string sep = null;
            int    foundAt = separators.Length;
            for (int i = sepIndex; i < separators.Length; i++)
            {
                if (text.Contains(separators[i]))
                {
                    sep     = separators[i];
                    foundAt = i;
                    break;
                }
            }

            if (sep == null) return new List<string> { text };

            var parts  = text.Split(new[] { sep }, StringSplitOptions.None);
            var chunks = new List<string>();
            string current = string.Empty;

            foreach (string part in parts)
            {
                string candidate = current.Length > 0 ? current + sep + part : part;
                if (candidate.Length > size && current.Length > 0)
                {
                    chunks.Add(current);
                    string overlapText = PickOverlap(current, overlap, sep);
                    current = overlapText.Length > 0 ? overlapText + sep + part : part;
                }
                else
                {
                    current = candidate;
                }
            }

            if (current.Length > 0) chunks.Add(current);

            var result = new List<string>();
            foreach (string chunk in chunks)
            {
                if (chunk.Length > size && foundAt + 1 < separators.Length)
                    result.AddRange(Split(chunk, size, overlap, separators, foundAt + 1));
                else
                    result.Add(chunk);
            }
            return result;
        }

        // ── Public API ─────────────────────────────────────────────────────

        internal struct ChunkResult
        {
            internal string Content;
            internal string Source;
            internal string Section;
            internal int    Index;
            internal int    Chars;
        }

        internal static List<ChunkResult> ChunkBySeparators(
            string text, string source, int size = ChunkSize, int overlap = ChunkOverlap)
        {
            var rawChunks = Split(text, size, overlap, Separators);
            var headings  = BuildHeadingIndex(text);
            var result    = new List<ChunkResult>();

            for (int i = 0; i < rawChunks.Count; i++)
            {
                string content = rawChunks[i];
                result.Add(new ChunkResult
                {
                    Content = content,
                    Source  = source,
                    Section = FindSection(text, content, headings),
                    Index   = i,
                    Chars   = content.Length
                });
            }

            return result;
        }
    }
}
