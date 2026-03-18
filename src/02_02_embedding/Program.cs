using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Common;

namespace FourthDevs.Lesson07_Embedding
{
    /// <summary>
    /// Lesson 07 – Embedding
    ///
    /// Interactive REPL that embeds each typed text with <c>text-embedding-3-small</c>
    /// and displays a colour-coded pairwise cosine-similarity matrix after each entry.
    ///
    /// Legend:
    ///   Green  ≥ 0.60  similar
    ///   Yellow ≥ 0.35  related
    ///   Red    &lt; 0.35  distant
    ///
    /// Type 'exit' or press Enter on an empty line to quit.
    ///
    /// Source: 02_02_embedding/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        // ---- ANSI colour helpers ------------------------------------------
        private const string Reset  = "\x1b[0m";
        private const string Dim    = "\x1b[2m";
        private const string Bold   = "\x1b[1m";
        private const string Green  = "\x1b[32m";
        private const string Yellow = "\x1b[33m";
        private const string Red    = "\x1b[31m";
        private const string Cyan   = "\x1b[36m";

        private const int LabelWidth = 14;

        // ----------------------------------------------------------------

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            string model = AiConfig.Provider == "openrouter"
                ? "openai/text-embedding-3-small"
                : "text-embedding-3-small";

            Console.WriteLine(string.Format(
                "\n{0}Embedding + Similarity Matrix{1} (model: {2})",
                Cyan, Reset, model));
            Console.WriteLine("Type 'exit' or press Enter on an empty line to quit.\n");

            var entries = new List<EmbeddingEntry>();

            using (var client = new EmbeddingClient())
            {
                while (true)
                {
                    Console.Write("Text: ");
                    string input = Console.ReadLine();

                    if (input == null) break; // EOF

                    string trimmed = input.Trim();

                    if (trimmed.ToLowerInvariant() == "exit" ||
                        string.IsNullOrEmpty(trimmed))
                        break;

                    try
                    {
                        float[] embedding = await client.EmbedAsync(trimmed);
                        entries.Add(new EmbeddingEntry { Text = trimmed, Embedding = embedding });

                        Console.WriteLine(string.Format(
                            "\n  \"{0}\" → {1}", trimmed, Preview(embedding)));

                        if (entries.Count == 1)
                        {
                            Console.WriteLine(Dim + "  Add more to see similarities." + Reset);
                            Console.WriteLine();
                            continue;
                        }

                        PrintMatrix(entries);
                        Console.WriteLine();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message + "\n");
                    }
                }
            }
        }

        // ----------------------------------------------------------------
        // Matrix printing
        // ----------------------------------------------------------------

        static void PrintMatrix(List<EmbeddingEntry> entries)
        {
            var labels = new List<string>();
            foreach (var e in entries)
                labels.Add(Truncate(e.Text, LabelWidth));

            int colWidth = LabelWidth;
            foreach (var l in labels)
                if (l.Length + 1 > colWidth) colWidth = l.Length + 1;

            // Header row
            var header = new System.Text.StringBuilder();
            header.Append(Pad(string.Empty, LabelWidth + 2));
            foreach (var l in labels)
                header.Append(Bold + PadStart(l, colWidth) + Reset);

            Console.WriteLine("\n" + header);

            // Matrix rows
            for (int i = 0; i < entries.Count; i++)
            {
                var row = new System.Text.StringBuilder();
                row.Append(Bold + Pad(labels[i], LabelWidth) + Reset + "  ");

                for (int j = 0; j < entries.Count; j++)
                {
                    if (i == j)
                    {
                        row.Append(PadStart(Dim + "  ——" + Reset, colWidth + 8));
                        continue;
                    }

                    double score = CosineSimilarity(
                        entries[i].Embedding, entries[j].Embedding);
                    string color = ColorFor(score);
                    string bar   = new string('█', (int)Math.Round(score * 8));
                    string value = score.ToString("F2");

                    row.Append(PadStart(
                        color + bar + " " + value + Reset, colWidth + 8));
                }

                Console.WriteLine(row.ToString());
            }

            // Legend
            Console.WriteLine(
                "\n  " + Dim + "Legend:" + Reset +
                "  " + Green  + "███ ≥0.60 similar" + Reset +
                "  " + Yellow + "███ ≥0.35 related" + Reset +
                "  " + Red    + "███ <0.35 distant" + Reset);
        }

        // ----------------------------------------------------------------
        // Cosine similarity
        // ----------------------------------------------------------------

        static double CosineSimilarity(float[] a, float[] b)
        {
            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot   += (double)a[i] * b[i];
                normA += (double)a[i] * a[i];
                normB += (double)b[i] * b[i];
            }
            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        // ----------------------------------------------------------------
        // Display helpers
        // ----------------------------------------------------------------

        static string Preview(float[] embedding)
        {
            string head = string.Join(", ", new[]
            {
                embedding[0].ToString("F4"),
                embedding[1].ToString("F4"),
                embedding[2].ToString("F4"),
                embedding[3].ToString("F4")
            });
            string tail = string.Join(", ", new[]
            {
                embedding[embedding.Length - 2].ToString("F4"),
                embedding[embedding.Length - 1].ToString("F4")
            });
            return string.Format(
                "{0}[{1}, …, {2}]{3} {4}({5}d){6}",
                Dim, head, tail, Reset, Cyan, embedding.Length, Reset);
        }

        static string ColorFor(double score)
            => score >= 0.6 ? Green : score >= 0.35 ? Yellow : Red;

        static string Truncate(string text, int width)
            => text.Length > width
                ? text.Substring(0, width - 1) + "…"
                : text;

        static string Pad(string text, int width)
            => text.PadRight(width);

        static string PadStart(string raw, int width)
        {
            // PadStart for strings that may contain ANSI escapes —
            // compute visible length by stripping escape sequences.
            int visible = AnsiVisibleLength(raw);
            int pad = width - visible;
            return pad > 0 ? new string(' ', pad) + raw : raw;
        }

        static int AnsiVisibleLength(string s)
        {
            // Strip ANSI escape sequences \x1b[...m before measuring
            int len = 0;
            bool inEsc = false;
            foreach (char c in s)
            {
                if (c == '\x1b') { inEsc = true; continue; }
                if (inEsc)       { if (c == 'm') inEsc = false; continue; }
                len++;
            }
            return len;
        }

        // ----------------------------------------------------------------
        // Entry type
        // ----------------------------------------------------------------

        private sealed class EmbeddingEntry
        {
            public string  Text      { get; set; }
            public float[] Embedding { get; set; }
        }
    }
}
