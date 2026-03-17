using System;
using System.Collections.Generic;

namespace FourthDevs.Lesson07_Chunking.Strategies
{
    /// <summary>
    /// Separator-based (recursive) chunking.
    /// Splits text using a hierarchy of separators: headers → paragraphs → sentences → words.
    ///
    /// Mirrors 02_02_chunking/src/strategies/separators.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Separators
    {
        private const int DefaultChunkSize    = 1000;
        private const int DefaultChunkOverlap = 200;

        private static readonly string[] SeparatorOrder =
        {
            "\n## ", "\n### ", "\n\n", "\n", ". ", " "
        };

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        internal static List<Chunk> ChunkBySeparators(
            string text,
            string source  = null,
            int    size    = DefaultChunkSize,
            int    overlap = DefaultChunkOverlap)
        {
            var stats    = new OverlapStats();
            var rawChunks = Split(text, size, overlap, SeparatorOrder, stats);

            Console.WriteLine(
                string.Format("[separators] overlap trimmed: {0}, dropped: {1}",
                    stats.Trimmed, stats.Dropped));

            var headings = MarkdownUtils.BuildHeadingIndex(text);
            var chunks   = new List<Chunk>();

            for (int i = 0; i < rawChunks.Count; i++)
            {
                string c = rawChunks[i];
                chunks.Add(new Chunk
                {
                    Content  = c,
                    Metadata = new Dictionary<string, object>
                    {
                        ["strategy"] = "separators",
                        ["index"]    = i,
                        ["chars"]    = c.Length,
                        ["section"]  = MarkdownUtils.FindSection(text, c, headings),
                        ["source"]   = source ?? (object)null
                    }
                });
            }

            return chunks;
        }

        // ----------------------------------------------------------------
        // Recursive splitter
        // ----------------------------------------------------------------

        private static List<string> Split(
            string text, int size, int overlap,
            string[] separators, OverlapStats stats)
        {
            if (text.Length <= size) return new List<string> { text };

            // Find the first separator that actually appears in the text
            string sep = null;
            int sepIdx = -1;
            for (int i = 0; i < separators.Length; i++)
            {
                if (text.Contains(separators[i]))
                {
                    sep    = separators[i];
                    sepIdx = i;
                    break;
                }
            }

            if (sep == null) return new List<string> { text };

            var parts   = text.Split(new[] { sep }, StringSplitOptions.None);
            var chunks  = new List<string>();
            string current = string.Empty;

            foreach (string part in parts)
            {
                string candidate = string.IsNullOrEmpty(current)
                    ? part
                    : current + sep + part;

                if (candidate.Length > size && !string.IsNullOrEmpty(current))
                {
                    chunks.Add(current);

                    string overlapText = PickOverlap(current, overlap, sep);
                    if (string.IsNullOrEmpty(overlapText))
                        stats.Dropped++;
                    else if (overlapText.Length < overlap)
                        stats.Trimmed++;

                    current = string.IsNullOrEmpty(overlapText)
                        ? part
                        : overlapText + sep + part;
                }
                else
                {
                    current = candidate;
                }
            }

            if (!string.IsNullOrEmpty(current))
                chunks.Add(current);

            // Recursively split chunks that are still too large
            int remStart     = sepIdx + 1;
            int remLen       = separators.Length - remStart;
            string[] remSeps = new string[remLen];
            for (int i = 0; i < remLen; i++)
                remSeps[i] = separators[remStart + i];

            var result = new List<string>();
            foreach (string c in chunks)
            {
                if (c.Length > size && remSeps.Length > 0)
                    result.AddRange(Split(c, size, overlap, remSeps, stats));
                else
                    result.Add(c);
            }

            return result;
        }

        // ----------------------------------------------------------------
        // Overlap helper
        // ----------------------------------------------------------------

        private static string PickOverlap(string text, int overlap, string sep)
        {
            if (overlap <= 0) return string.Empty;

            int start = Math.Max(0, text.Length - overlap);
            string tail = text.Substring(start);

            int idx = tail.IndexOf('\n');
            if (idx == -1)
            {
                for (int i = 0; i < tail.Length; i++)
                {
                    if (char.IsWhiteSpace(tail[i]))
                    {
                        idx = i;
                        break;
                    }
                }
            }

            if (idx == -1) return string.Empty;

            string overlapText = text.Substring(start + idx + 1);

            if (!string.IsNullOrEmpty(sep) && overlapText.StartsWith(sep,
                StringComparison.Ordinal))
                overlapText = overlapText.Substring(sep.Length);

            return overlapText;
        }

        // ----------------------------------------------------------------
        // Stats helper
        // ----------------------------------------------------------------

        private sealed class OverlapStats
        {
            public int Trimmed  { get; set; }
            public int Dropped  { get; set; }
        }
    }
}
