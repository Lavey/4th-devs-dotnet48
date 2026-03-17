using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FourthDevs.FilesMcp.Config;
using FourthDevs.FilesMcp.Lib;
using FourthDevs.FilesMcp.Utils;
using Newtonsoft.Json.Linq;

namespace FourthDevs.FilesMcp.Tools
{
    internal class FsReadTool
    {
        private readonly PathResolver _resolver;
        private readonly long _maxFileSize;
        private readonly GitIgnoreHandler _gitIgnore = new GitIgnoreHandler();

        public FsReadTool(EnvironmentConfig config)
        {
            _resolver = new PathResolver(config);
            _maxFileSize = config.MaxFileSize;
        }

        public JObject GetToolDefinition()
        {
            return JObject.Parse(@"{
                ""name"": ""fs_read"",
                ""description"": ""Read files or explore directory structure"",
                ""inputSchema"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""path"": {""type"": ""string"", ""description"": ""Path to read (file or directory)""},
                        ""depth"": {""type"": ""integer"", ""description"": ""Max directory traversal depth (default: 2)"", ""default"": 2},
                        ""lines"": {""type"": ""string"", ""description"": ""Line range to read, e.g. '10-20' or '5'""},
                        ""glob"": {""type"": ""string"", ""description"": ""Glob filter for directory listing""},
                        ""exclude"": {""type"": ""string"", ""description"": ""Glob pattern to exclude""},
                        ""respectIgnore"": {""type"": ""boolean"", ""description"": ""Respect .gitignore files (default: true)"", ""default"": true}
                    },
                    ""required"": [""path""]
                }
            }");
        }

        public string Execute(JObject args)
        {
            string path = (string)args["path"];
            if (string.IsNullOrWhiteSpace(path))
                return "Error: 'path' parameter is required.";

            // Special case: list mounts
            if (path == "/" || path == "mounts" || path == ".")
            {
                var mounts = _resolver.GetMounts();
                if (path == "mounts" || (path == "/" && !Directory.Exists("/")))
                {
                    var sb = new StringBuilder("Available mount points:\n");
                    foreach (var m in mounts)
                        sb.AppendLine($"  {m.Alias} → {m.AbsolutePath}");
                    return sb.ToString();
                }
            }

            string resolved = _resolver.Resolve(path);
            if (resolved == null)
                return $"Error: Access denied. Path '{path}' is outside configured mount points.\n" +
                       $"Available mounts:\n" +
                       string.Join("\n", _resolver.GetMountDescriptions());

            int depth = (int?)args["depth"] ?? 2;
            string glob = (string)args["glob"];
            string exclude = (string)args["exclude"];
            bool respectIgnore = (bool?)args["respectIgnore"] ?? true;
            string linesParam = (string)args["lines"];

            try
            {
                if (Directory.Exists(resolved))
                    return ReadDirectory(resolved, depth, glob, exclude, respectIgnore);

                if (File.Exists(resolved))
                    return ReadFile(resolved, linesParam);

                return $"Error: Path not found: {resolved}";
            }
            catch (Exception ex)
            {
                Logger.Error($"fs_read error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string ReadFile(string filePath, string linesParam)
        {
            var info = new FileInfo(filePath);
            if (info.Length > _maxFileSize)
                return $"Error: File exceeds maximum size limit ({_maxFileSize:N0} bytes). File is {info.Length:N0} bytes.\n" +
                       "Use the 'lines' parameter to read a specific range.";

            if (!FileTypeDetector.IsTextFile(filePath))
                return $"Binary file: {filePath}\nSize: {FormatSize(info.Length)}\nChecksum (SHA256): {ChecksumHelper.ComputeFileChecksum(filePath)}";

            string content = File.ReadAllText(filePath, Encoding.UTF8);
            string[] lines = LineManipulator.GetLinesStripped(content);
            int totalLines = lines.Length;

            int startLine = 1, endLine = totalLines;
            bool partial = false;

            if (!string.IsNullOrWhiteSpace(linesParam))
            {
                ParseLineRange(linesParam, out startLine, out endLine);
                startLine = Math.Max(1, Math.Min(startLine, totalLines));
                endLine   = Math.Max(startLine, Math.Min(endLine, totalLines));
                partial = true;
            }

            string checksum = ChecksumHelper.ComputeFileChecksum(filePath);
            var sb = new StringBuilder();
            sb.AppendLine($"File: {filePath}");
            sb.AppendLine($"Checksum (SHA256): {checksum}");
            sb.AppendLine($"Size: {FormatSize(info.Length)}");
            if (partial)
                sb.AppendLine($"Lines: {startLine}-{endLine} of {totalLines} (partial view)");
            else
                sb.AppendLine($"Lines: {totalLines}");
            sb.AppendLine();

            for (int i = startLine - 1; i < endLine && i < lines.Length; i++)
                sb.AppendLine($"{i + 1,5}: {lines[i]}");

            return sb.ToString();
        }

        private string ReadDirectory(string dirPath, int maxDepth, string glob, string exclude, bool respectIgnore)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Directory: {dirPath}");
            sb.AppendLine();
            AppendTree(sb, dirPath, "", maxDepth, 0, glob, exclude, respectIgnore);
            return sb.ToString();
        }

        private void AppendTree(StringBuilder sb, string dir, string prefix,
            int maxDepth, int depth, string glob, string exclude, bool respectIgnore)
        {
            if (depth > maxDepth) return;

            List<string> dirs;
            List<string> files;
            try
            {
                dirs  = new List<string>(Directory.GetDirectories(dir));
                files = new List<string>(Directory.GetFiles(dir));
                dirs.Sort(StringComparer.OrdinalIgnoreCase);
                files.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                sb.AppendLine($"{prefix}[access denied]");
                return;
            }

            // Filter by glob/exclude/gitignore
            var filteredFiles = new List<string>();
            foreach (var f in files)
            {
                if (respectIgnore && _gitIgnore.IsIgnored(f, dir)) continue;
                string name = Path.GetFileName(f);
                if (!string.IsNullOrWhiteSpace(glob) && !PatternMatcher.MatchGlob(glob, name)) continue;
                if (!string.IsNullOrWhiteSpace(exclude) && PatternMatcher.MatchGlob(exclude, name)) continue;
                filteredFiles.Add(f);
            }

            var filteredDirs = new List<string>();
            if (depth < maxDepth)
            {
                foreach (var d in dirs)
                {
                    if (respectIgnore && _gitIgnore.IsIgnored(d, dir)) continue;
                    string name = Path.GetFileName(d);
                    if (!string.IsNullOrWhiteSpace(exclude) && PatternMatcher.MatchGlob(exclude, name)) continue;
                    filteredDirs.Add(d);
                }
            }

            int total = filteredDirs.Count + filteredFiles.Count;
            int idx = 0;

            foreach (var d in filteredDirs)
            {
                idx++;
                bool isLast = idx == total;
                string connector = isLast ? "└── " : "├── ";
                string childPrefix = isLast ? "    " : "│   ";
                sb.AppendLine($"{prefix}{connector}{Path.GetFileName(d)}/");
                AppendTree(sb, d, prefix + childPrefix, maxDepth, depth + 1, glob, exclude, respectIgnore);
            }

            foreach (var f in filteredFiles)
            {
                idx++;
                bool isLast = idx == total;
                string connector = isLast ? "└── " : "├── ";
                long size = 0;
                try { size = new FileInfo(f).Length; } catch { }
                sb.AppendLine($"{prefix}{connector}{Path.GetFileName(f)} ({FormatSize(size)})");
            }
        }

        private static void ParseLineRange(string s, out int start, out int end)
        {
            int dash = s.IndexOf('-');
            if (dash >= 0)
            {
                int.TryParse(s.Substring(0, dash).Trim(), out start);
                int.TryParse(s.Substring(dash + 1).Trim(), out end);
                if (start <= 0) start = 1;
                if (end <= 0) end = start;
            }
            else
            {
                int.TryParse(s.Trim(), out start);
                if (start <= 0) start = 1;
                end = start;
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F1} GB";
        }
    }
}
