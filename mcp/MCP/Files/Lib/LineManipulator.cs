using System;
using System.Collections.Generic;

namespace FourthDevs.Mcp.Files.Lib
{
    internal static class LineManipulator
    {
        /// <summary>
        /// Split content into lines. The last element may be an empty string if content ends with newline.
        /// </summary>
        public static string[] GetLines(string content)
        {
            return content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        /// <summary>
        /// Split content into lines, removing the trailing empty element caused by a
        /// final newline so callers get the actual line count.
        /// </summary>
        public static string[] GetLinesStripped(string content)
        {
            var lines = GetLines(content);
            int count = lines.Length;
            if (count > 0 && string.IsNullOrEmpty(lines[count - 1]))
                Array.Resize(ref lines, count - 1);
            return lines;
        }

        /// <summary>
        /// Replace lines in range [startLine, endLine] (1-indexed, inclusive) with newLines.
        /// </summary>
        public static string[] ReplaceLines(string[] lines, int startLine, int endLine, string[] newLines)
        {
            ValidateRange(lines, startLine, endLine);
            var result = new List<string>();
            for (int i = 0; i < startLine - 1; i++)
                result.Add(lines[i]);
            result.AddRange(newLines);
            for (int i = endLine; i < lines.Length; i++)
                result.Add(lines[i]);
            return result.ToArray();
        }

        /// <summary>
        /// Insert newLines before lineNum (1-indexed).
        /// </summary>
        public static string[] InsertBefore(string[] lines, int lineNum, string[] newLines)
        {
            if (lineNum < 1 || lineNum > lines.Length + 1)
                throw new ArgumentOutOfRangeException(nameof(lineNum),
                    $"Line number {lineNum} is out of range (1–{lines.Length + 1}).");
            var result = new List<string>();
            for (int i = 0; i < lineNum - 1; i++)
                result.Add(lines[i]);
            result.AddRange(newLines);
            for (int i = lineNum - 1; i < lines.Length; i++)
                result.Add(lines[i]);
            return result.ToArray();
        }

        /// <summary>
        /// Insert newLines after lineNum (1-indexed).
        /// </summary>
        public static string[] InsertAfter(string[] lines, int lineNum, string[] newLines)
        {
            if (lineNum < 1 || lineNum > lines.Length)
                throw new ArgumentOutOfRangeException(nameof(lineNum),
                    $"Line number {lineNum} is out of range (1–{lines.Length}).");
            var result = new List<string>();
            for (int i = 0; i < lineNum; i++)
                result.Add(lines[i]);
            result.AddRange(newLines);
            for (int i = lineNum; i < lines.Length; i++)
                result.Add(lines[i]);
            return result.ToArray();
        }

        /// <summary>
        /// Delete lines in range [startLine, endLine] (1-indexed, inclusive).
        /// </summary>
        public static string[] DeleteLines(string[] lines, int startLine, int endLine)
        {
            ValidateRange(lines, startLine, endLine);
            var result = new List<string>();
            for (int i = 0; i < startLine - 1; i++)
                result.Add(lines[i]);
            for (int i = endLine; i < lines.Length; i++)
                result.Add(lines[i]);
            return result.ToArray();
        }

        /// <summary>
        /// Detect the dominant line ending in content (\r\n, \r, or \n).
        /// </summary>
        public static string DetectLineEnding(string content)
        {
            int crlfCount = 0, lfCount = 0, crCount = 0;
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                { crlfCount++; i++; }
                else if (content[i] == '\r') crCount++;
                else if (content[i] == '\n') lfCount++;
            }
            if (crlfCount >= lfCount && crlfCount >= crCount) return "\r\n";
            if (crCount > lfCount) return "\r";
            return "\n";
        }

        private static void ValidateRange(string[] lines, int startLine, int endLine)
        {
            if (startLine < 1 || startLine > lines.Length)
                throw new ArgumentOutOfRangeException(nameof(startLine),
                    $"Start line {startLine} is out of range (1–{lines.Length}).");
            if (endLine < startLine || endLine > lines.Length)
                throw new ArgumentOutOfRangeException(nameof(endLine),
                    $"End line {endLine} is out of range ({startLine}–{lines.Length}).");
        }
    }
}
