using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FourthDevs.Mcp.Files.Config;
using FourthDevs.Mcp.Files.Lib;
using FourthDevs.Mcp.Files.Utils;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Mcp.Files.Tools
{
    internal class FsSearchTool
    {
        private readonly PathResolver _resolver;
        private readonly long _maxFileSize;

        public FsSearchTool(EnvironmentConfig config)
        {
            _resolver = new PathResolver(config);
            _maxFileSize = config.MaxFileSize;
        }

        public JObject GetToolDefinition()
        {
            return JObject.Parse(@"{
                ""name"": ""fs_search"",
                ""description"": ""Search for files by name or content"",
                ""inputSchema"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""path"": {""type"": ""string"", ""description"": ""Directory to search in""},
                        ""query"": {""type"": ""string"", ""description"": ""Search query""},
                        ""target"": {""type"": ""string"", ""enum"": [""filename"", ""content"", ""all""], ""default"": ""all""},
                        ""patternMode"": {""type"": ""string"", ""enum"": [""literal"", ""regex"", ""fuzzy""], ""default"": ""literal""},
                        ""contextLines"": {""type"": ""integer"", ""description"": ""Context lines around content matches (default: 2)"", ""default"": 2},
                        ""maxResults"": {""type"": ""integer"", ""description"": ""Max results (default: 50)"", ""default"": 50}
                    },
                    ""required"": [""path"", ""query""]
                }
            }");
        }

        public string Execute(JObject args)
        {
            string path = (string)args["path"];
            string query = (string)args["query"];

            if (string.IsNullOrWhiteSpace(path))  return "Error: 'path' parameter is required.";
            if (string.IsNullOrWhiteSpace(query)) return "Error: 'query' parameter is required.";

            string resolved = _resolver.Resolve(path);
            if (resolved == null)
                return $"Error: Access denied. Path '{path}' is outside configured mount points.";

            if (!Directory.Exists(resolved))
                return $"Error: Directory not found: {resolved}";

            string target      = (string)args["target"]      ?? "all";
            string patternMode = (string)args["patternMode"] ?? "literal";
            int contextLines   = (int?)args["contextLines"]  ?? 2;
            int maxResults     = (int?)args["maxResults"]    ?? 50;

            try
            {
                var results = new List<string>();
                bool searchFilenames = target == "filename" || target == "all";
                bool searchContent  = target == "content"  || target == "all";

                SearchDirectory(resolved, query, patternMode, searchFilenames, searchContent,
                    contextLines, maxResults, results);

                if (results.Count == 0)
                    return $"No results found for query '{query}' in {resolved}";

                var sb = new StringBuilder();
                sb.AppendLine($"Search results for '{query}' in {resolved}:");
                sb.AppendLine($"({results.Count} result(s))");
                sb.AppendLine();
                foreach (var r in results)
                    sb.AppendLine(r);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error($"fs_search error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private void SearchDirectory(string dir, string query, string patternMode,
            bool searchFilenames, bool searchContent, int contextLines,
            int maxResults, List<string> results)
        {
            if (results.Count >= maxResults) return;

            string[] entries;
            try { entries = Directory.GetFileSystemEntries(dir); }
            catch { return; }

            Array.Sort(entries, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                if (results.Count >= maxResults) break;

                if (Directory.Exists(entry))
                {
                    if (searchFilenames)
                        CheckFilenameMatch(entry, query, patternMode, results, maxResults, isDir: true);
                    SearchDirectory(entry, query, patternMode, searchFilenames, searchContent,
                        contextLines, maxResults, results);
                }
                else if (File.Exists(entry))
                {
                    if (searchFilenames)
                        CheckFilenameMatch(entry, query, patternMode, results, maxResults, isDir: false);
                    if (searchContent && results.Count < maxResults)
                        CheckContentMatch(entry, query, patternMode, contextLines, results, maxResults);
                }
            }
        }

        private static void CheckFilenameMatch(string entryPath, string query, string patternMode,
            List<string> results, int maxResults, bool isDir)
        {
            if (results.Count >= maxResults) return;
            string name = Path.GetFileName(entryPath);
            bool matched = IsMatch(name, query, patternMode, out int score);
            if (matched)
            {
                string suffix = isDir ? "/" : "";
                results.Add($"[FILE] {entryPath}{suffix}{(patternMode == "fuzzy" ? $" (score: {score})" : "")}");
            }
        }

        private void CheckContentMatch(string filePath, string query, string patternMode,
            int contextLines, List<string> results, int maxResults)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (info.Length > _maxFileSize) return;
                if (!FileTypeDetector.IsTextFile(filePath)) return;

                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                bool headerAdded = false;

                for (int i = 0; i < lines.Length && results.Count < maxResults; i++)
                {
                    bool matched = IsMatch(lines[i], query, patternMode, out _);
                    if (!matched) continue;

                    if (!headerAdded)
                    {
                        results.Add($"[CONTENT] {filePath}");
                        headerAdded = true;
                    }

                    int from = Math.Max(0, i - contextLines);
                    int to   = Math.Min(lines.Length - 1, i + contextLines);
                    var snippet = new StringBuilder();
                    for (int j = from; j <= to; j++)
                    {
                        string marker = j == i ? ">" : " ";
                        snippet.AppendLine($"  {marker} {j + 1,5}: {lines[j]}");
                    }
                    results.Add(snippet.ToString().TrimEnd());
                }
            }
            catch
            {
                // Skip files that can't be read
            }
        }

        private static bool IsMatch(string text, string query, string patternMode, out int score)
        {
            score = 0;
            switch (patternMode)
            {
                case "regex":
                    try
                    {
                        bool m = Regex.IsMatch(text, query, RegexOptions.IgnoreCase);
                        score = m ? 100 : 0;
                        return m;
                    }
                    catch { return false; }

                case "fuzzy":
                    return PatternMatcher.MatchFuzzy(query, text, out score);

                default: // literal
                    bool hit = text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    score = hit ? 100 : 0;
                    return hit;
            }
        }
    }
}
