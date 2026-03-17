using System;
using System.Collections.Generic;
using System.Text;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace FourthDevs.Mcp.Files.Lib
{
    internal static class DiffGenerator
    {
        private const int DefaultContextLines = 3;
        /// <summary>
        /// Generate a unified diff between oldContent and newContent.
        /// </summary>
        public static string GenerateUnifiedDiff(string oldContent, string newContent,
            string oldLabel = "old", string newLabel = "new")
        {
            try
            {
                return BuildDiffPlexDiff(oldContent, newContent, oldLabel, newLabel);
            }
            catch
            {
                return BuildFallbackDiff(oldContent, newContent, oldLabel, newLabel);
            }
        }

        private static string BuildDiffPlexDiff(string oldContent, string newContent,
            string oldLabel, string newLabel)
        {
            var diff = InlineDiffBuilder.Instance.BuildDiffModel(oldContent, newContent);
            var sb = new StringBuilder();
            sb.AppendLine($"--- {oldLabel}");
            sb.AppendLine($"+++ {newLabel}");

            var hunkLines = new List<DiffPiece>();
            int oldLine = 0, newLine = 0;

            // Collect hunks
            var allLines = diff.Lines;
            int i = 0;
            while (i < allLines.Count)
            {
                // Skip unchanged lines, tracking line numbers
                while (i < allLines.Count && allLines[i].Type == ChangeType.Unchanged)
                {
                    oldLine++;
                    newLine++;
                    i++;
                }
                if (i >= allLines.Count) break;

                // Gather changed region with context
                int contextBefore = DefaultContextLines;
                int hunkStart = Math.Max(0, i - contextBefore);
                int oldHunkStart = oldLine - (i - hunkStart);
                int newHunkStart = newLine - (i - hunkStart);

                var hunkOld = new List<string>();
                var hunkNew = new List<string>();
                var hunkDisplay = new List<(char prefix, string text)>();

                // Add context before (walk back)
                for (int k = hunkStart; k < i; k++)
                {
                    var pl = allLines[k];
                    hunkOld.Add(pl.Text ?? "");
                    hunkNew.Add(pl.Text ?? "");
                    hunkDisplay.Add((' ', pl.Text ?? ""));
                }

                // Process changes with trailing context
                int trailingContext = 0;
                while (i < allLines.Count && trailingContext < contextBefore)
                {
                    var piece = allLines[i];
                    if (piece.Type == ChangeType.Unchanged)
                    {
                        hunkOld.Add(piece.Text ?? "");
                        hunkNew.Add(piece.Text ?? "");
                        hunkDisplay.Add((' ', piece.Text ?? ""));
                        oldLine++;
                        newLine++;
                        trailingContext++;
                    }
                    else if (piece.Type == ChangeType.Deleted)
                    {
                        hunkOld.Add(piece.Text ?? "");
                        hunkDisplay.Add(('-', piece.Text ?? ""));
                        oldLine++;
                        trailingContext = 0;
                    }
                    else if (piece.Type == ChangeType.Inserted)
                    {
                        hunkNew.Add(piece.Text ?? "");
                        hunkDisplay.Add(('+', piece.Text ?? ""));
                        newLine++;
                        trailingContext = 0;
                    }
                    else if (piece.Type == ChangeType.Modified)
                    {
                        hunkOld.Add(piece.Text ?? "");
                        hunkNew.Add(piece.Text ?? "");
                        hunkDisplay.Add(('-', piece.Text ?? ""));
                        hunkDisplay.Add(('+', piece.Text ?? ""));
                        oldLine++;
                        newLine++;
                        trailingContext = 0;
                    }
                    i++;
                }

                sb.AppendLine($"@@ -{oldHunkStart},{hunkOld.Count} +{newHunkStart},{hunkNew.Count} @@");
                foreach (var (prefix, text) in hunkDisplay)
                    sb.AppendLine($"{prefix}{text}");
            }

            return sb.ToString();
        }

        private static string BuildFallbackDiff(string oldContent, string newContent,
            string oldLabel, string newLabel)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- {oldLabel}");
            sb.AppendLine($"+++ {newLabel}");

            var oldLines = oldContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var newLines = newContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            int maxOld = oldLines.Length;
            int maxNew = newLines.Length;
            int max = Math.Max(maxOld, maxNew);

            sb.AppendLine($"@@ -1,{maxOld} +1,{maxNew} @@");
            for (int i = 0; i < max; i++)
            {
                if (i < maxOld) sb.AppendLine($"-{oldLines[i]}");
                if (i < maxNew) sb.AppendLine($"+{newLines[i]}");
            }
            return sb.ToString();
        }
    }
}
