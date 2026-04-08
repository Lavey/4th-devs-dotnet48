using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FourthDevs.CodingAgent.Tools
{
    /// <summary>
    /// Direct C# implementations of filesystem tools that mirror the
    /// MCP files server from the TypeScript original.
    /// All paths are sandboxed within the workspace directory.
    /// </summary>
    internal static class FileSystemTools
    {
        /// <summary>
        /// Resolves and validates a path is within the workspace. Throws if path escapes.
        /// </summary>
        private static string ResolveSafe(string workspace, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Path must not be empty.");

            string full = Path.GetFullPath(Path.Combine(workspace, relativePath));
            string normalizedWorkspace = Path.GetFullPath(workspace);

            if (!full.StartsWith(normalizedWorkspace, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException(
                    string.Format("Access denied: path '{0}' is outside workspace.", relativePath));

            return full;
        }

        public static string ReadFile(string workspace, JObject args)
        {
            string path = (string)args["path"];
            string full = ResolveSafe(workspace, path);

            if (!File.Exists(full))
                return string.Format("Error: file not found: {0}", path);

            return File.ReadAllText(full);
        }

        public static string WriteFile(string workspace, JObject args)
        {
            string path = (string)args["path"];
            string content = (string)args["content"] ?? string.Empty;
            string full = ResolveSafe(workspace, path);

            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(full, content);
            return string.Format("File written: {0} ({1} chars)", path, content.Length);
        }

        public static string ListDirectory(string workspace, JObject args)
        {
            string path = (string)args["path"] ?? ".";
            string full = ResolveSafe(workspace, path);

            if (!Directory.Exists(full))
                return string.Format("Error: directory not found: {0}", path);

            var entries = new List<string>();

            foreach (string dir in Directory.GetDirectories(full))
            {
                entries.Add("[DIR]  " + Path.GetFileName(dir));
            }

            foreach (string file in Directory.GetFiles(full))
            {
                var info = new FileInfo(file);
                entries.Add(string.Format("[FILE] {0} ({1} bytes)", info.Name, info.Length));
            }

            if (entries.Count == 0)
                return string.Format("Directory '{0}' is empty.", path);

            return string.Join("\n", entries.ToArray());
        }

        public static string CreateDirectory(string workspace, JObject args)
        {
            string path = (string)args["path"];
            string full = ResolveSafe(workspace, path);

            Directory.CreateDirectory(full);
            return string.Format("Directory created: {0}", path);
        }

        public static string DeleteFile(string workspace, JObject args)
        {
            string path = (string)args["path"];
            string full = ResolveSafe(workspace, path);

            if (File.Exists(full))
            {
                File.Delete(full);
                return string.Format("File deleted: {0}", path);
            }

            if (Directory.Exists(full))
            {
                Directory.Delete(full, true);
                return string.Format("Directory deleted: {0}", path);
            }

            return string.Format("Error: not found: {0}", path);
        }

        public static string MoveFile(string workspace, JObject args)
        {
            string source = (string)args["source"];
            string destination = (string)args["destination"];

            string fullSrc = ResolveSafe(workspace, source);
            string fullDst = ResolveSafe(workspace, destination);

            string dstDir = Path.GetDirectoryName(fullDst);
            if (!string.IsNullOrEmpty(dstDir))
                Directory.CreateDirectory(dstDir);

            if (File.Exists(fullSrc))
            {
                File.Move(fullSrc, fullDst);
                return string.Format("Moved: {0} -> {1}", source, destination);
            }

            if (Directory.Exists(fullSrc))
            {
                Directory.Move(fullSrc, fullDst);
                return string.Format("Moved directory: {0} -> {1}", source, destination);
            }

            return string.Format("Error: source not found: {0}", source);
        }

        public static string SearchFiles(string workspace, JObject args)
        {
            string pattern = (string)args["pattern"] ?? "*";
            string path = (string)args["path"] ?? ".";
            string full = ResolveSafe(workspace, path);

            if (!Directory.Exists(full))
                return string.Format("Error: directory not found: {0}", path);

            var matches = Directory.GetFiles(full, pattern, SearchOption.AllDirectories);

            if (matches.Length == 0)
                return string.Format("No files matching '{0}' found.", pattern);

            string normalizedWorkspace = Path.GetFullPath(workspace);
            var relative = matches
                .Select(m =>
                {
                    string rel = m.Substring(normalizedWorkspace.Length);
                    return rel.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                })
                .ToArray();

            return string.Join("\n", relative);
        }
    }
}
