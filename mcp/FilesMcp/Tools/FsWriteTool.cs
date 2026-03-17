using System;
using System.IO;
using System.Text;
using FourthDevs.FilesMcp.Config;
using FourthDevs.FilesMcp.Lib;
using FourthDevs.FilesMcp.Utils;
using Newtonsoft.Json.Linq;

namespace FourthDevs.FilesMcp.Tools
{
    internal class FsWriteTool
    {
        private readonly PathResolver _resolver;

        public FsWriteTool(EnvironmentConfig config)
        {
            _resolver = new PathResolver(config);
        }

        public JObject GetToolDefinition()
        {
            return JObject.Parse(@"{
                ""name"": ""fs_write"",
                ""description"": ""Create or edit files"",
                ""inputSchema"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""path"": {""type"": ""string"", ""description"": ""File path to write""},
                        ""operation"": {""type"": ""string"", ""enum"": [""create"", ""update""], ""description"": ""Operation type""},
                        ""content"": {""type"": ""string"", ""description"": ""Full content (for create, or update without lines)""},
                        ""lines"": {""type"": ""string"", ""description"": ""Line range for line-based edit, e.g. '10-20'""},
                        ""action"": {""type"": ""string"", ""enum"": [""replace"", ""insert_before"", ""insert_after"", ""delete_lines""], ""description"": ""Line edit action""},
                        ""checksum"": {""type"": ""string"", ""description"": ""Expected SHA256 checksum of current file (safety check)""},
                        ""dryRun"": {""type"": ""boolean"", ""description"": ""If true, return diff without writing"", ""default"": false}
                    },
                    ""required"": [""path"", ""operation""]
                }
            }");
        }

        public string Execute(JObject args)
        {
            string path      = (string)args["path"];
            string operation = (string)args["operation"];
            string content   = (string)args["content"];
            string linesArg  = (string)args["lines"];
            string action    = (string)args["action"];
            string checksum  = (string)args["checksum"];
            bool dryRun      = (bool?)args["dryRun"] ?? false;

            if (string.IsNullOrWhiteSpace(path))
                return "Error: 'path' parameter is required.";
            if (string.IsNullOrWhiteSpace(operation))
                return "Error: 'operation' parameter is required.";

            string resolved = _resolver.Resolve(path);
            if (resolved == null)
                return $"Error: Access denied. Path '{path}' is outside configured mount points.";

            try
            {
                switch (operation)
                {
                    case "create":
                        return CreateFile(resolved, content, dryRun);
                    case "update":
                        return UpdateFile(resolved, content, linesArg, action, checksum, dryRun);
                    default:
                        return $"Error: Unknown operation '{operation}'. Use 'create' or 'update'.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"fs_write error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string CreateFile(string filePath, string content, bool dryRun)
        {
            if (File.Exists(filePath))
                return $"Error: File already exists: {filePath}\nUse operation='update' to modify existing files.";

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            content = content ?? "";

            if (dryRun)
            {
                string diff = DiffGenerator.GenerateUnifiedDiff("", content, "/dev/null", filePath);
                return $"[Dry Run] Would create file: {filePath}\n\n{diff}";
            }

            File.WriteAllText(filePath, content, Encoding.UTF8);
            string newChecksum = ChecksumHelper.ComputeFileChecksum(filePath);
            return $"Created: {filePath}\nChecksum (SHA256): {newChecksum}\nSize: {new FileInfo(filePath).Length:N0} bytes";
        }

        private string UpdateFile(string filePath, string content, string linesArg,
            string action, string expectedChecksum, bool dryRun)
        {
            if (!File.Exists(filePath))
                return $"Error: File not found: {filePath}\nUse operation='create' to create new files.";

            // Checksum verification
            if (!string.IsNullOrWhiteSpace(expectedChecksum))
            {
                string actual = ChecksumHelper.ComputeFileChecksum(filePath);
                if (!string.Equals(actual, expectedChecksum.Trim(), StringComparison.OrdinalIgnoreCase))
                    return $"Error: Checksum mismatch.\n  Expected: {expectedChecksum}\n  Actual:   {actual}\n" +
                           "File may have been modified since you last read it.";
            }

            string oldContent = File.ReadAllText(filePath, Encoding.UTF8);

            // Full-file update
            if (string.IsNullOrWhiteSpace(linesArg))
            {
                if (content == null)
                    return "Error: 'content' is required for full-file update.";

                if (dryRun)
                {
                    string diff = DiffGenerator.GenerateUnifiedDiff(oldContent, content, filePath, filePath);
                    return $"[Dry Run] Diff for: {filePath}\n\n{diff}";
                }

                File.WriteAllText(filePath, content, Encoding.UTF8);
                string newChecksum = ChecksumHelper.ComputeFileChecksum(filePath);
                return $"Updated: {filePath}\nChecksum (SHA256): {newChecksum}\nSize: {new FileInfo(filePath).Length:N0} bytes";
            }

            // Line-based update
            ParseLineRange(linesArg, out int startLine, out int endLine);
            string[] lines = LineManipulator.GetLines(oldContent);
            string lineEnding = LineManipulator.DetectLineEnding(oldContent);

            string[] newContentLines = content != null
                ? LineManipulator.GetLinesStripped(content)
                : new string[0];

            string[] resultLines;
            action = action ?? "replace";
            switch (action)
            {
                case "replace":
                    resultLines = LineManipulator.ReplaceLines(lines, startLine, endLine, newContentLines);
                    break;
                case "insert_before":
                    resultLines = LineManipulator.InsertBefore(lines, startLine, newContentLines);
                    break;
                case "insert_after":
                    resultLines = LineManipulator.InsertAfter(lines, endLine, newContentLines);
                    break;
                case "delete_lines":
                    resultLines = LineManipulator.DeleteLines(lines, startLine, endLine);
                    break;
                default:
                    return $"Error: Unknown action '{action}'. Use replace, insert_before, insert_after, or delete_lines.";
            }

            string newContent = string.Join(lineEnding, resultLines);
            // Preserve trailing newline if original had one
            if (oldContent.EndsWith("\n") || oldContent.EndsWith("\r"))
            {
                if (!newContent.EndsWith("\n") && !newContent.EndsWith("\r"))
                    newContent += lineEnding;
            }

            if (dryRun)
            {
                string diff = DiffGenerator.GenerateUnifiedDiff(oldContent, newContent, filePath, filePath);
                return $"[Dry Run] Diff for: {filePath}\n\n{diff}";
            }

            File.WriteAllText(filePath, newContent, Encoding.UTF8);
            string checksum2 = ChecksumHelper.ComputeFileChecksum(filePath);
            return $"Updated: {filePath} (lines {startLine}-{endLine}, action: {action})\nChecksum (SHA256): {checksum2}\nSize: {new FileInfo(filePath).Length:N0} bytes";
        }

        private static void ParseLineRange(string s, out int start, out int end)
        {
            int dash = s.IndexOf('-');
            if (dash >= 0)
            {
                int.TryParse(s.Substring(0, dash).Trim(), out start);
                int.TryParse(s.Substring(dash + 1).Trim(), out end);
                if (start <= 0) start = 1;
                if (end < start) end = start;
            }
            else
            {
                int.TryParse(s.Trim(), out start);
                if (start <= 0) start = 1;
                end = start;
            }
        }
    }
}
