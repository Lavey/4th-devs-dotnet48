using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Ops.Tools
{
    /// <summary>
    /// Implements the tool handlers for the Daily Ops agent system.
    /// All file paths are sandboxed to the workspace directory.
    /// </summary>
    internal static class ToolExecutors
    {
        private static readonly string WorkspaceRoot =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");

        // ----------------------------------------------------------------
        // Dispatcher
        // ----------------------------------------------------------------

        public static Task<string> ExecuteAsync(string name, JObject args)
        {
            switch (name)
            {
                case "get_mail":     return GetSourceJsonAsync("mail.json");
                case "get_calendar": return GetSourceJsonAsync("calendar.json");
                case "get_tasks":    return GetSourceJsonAsync("tasks.json");
                case "get_notes":    return GetSourceJsonAsync("notes.json");
                case "read_file":    return ReadFileAsync(args);
                case "write_file":   return WriteFileAsync(args);
                default:
                    return Task.FromResult($"Unknown tool: {name}");
            }
        }

        // ----------------------------------------------------------------
        // Tool implementations
        // ----------------------------------------------------------------

        private static Task<string> GetSourceJsonAsync(string fileName)
        {
            try
            {
                string path = Path.Combine(WorkspaceRoot, "sources", fileName);
                if (!File.Exists(path))
                    return Task.FromResult($"Error: source file not found: {fileName}");

                string raw    = File.ReadAllText(path, Encoding.UTF8);
                // Validate + re-serialise to ensure clean JSON
                object parsed = JsonConvert.DeserializeObject(raw);
                return Task.FromResult(JsonConvert.SerializeObject(parsed));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error: {ex.Message}");
            }
        }

        private static Task<string> ReadFileAsync(JObject args)
        {
            try
            {
                string relPath = (string)args["path"];
                if (string.IsNullOrWhiteSpace(relPath))
                    return Task.FromResult("Error: path must be a non-empty string");

                if (!IsSafe(relPath))
                    return Task.FromResult("Error: path escapes workspace");

                string fullPath = Path.Combine(WorkspaceRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                    return Task.FromResult($"Error: file not found: {relPath}");

                string content = File.ReadAllText(fullPath, Encoding.UTF8);
                return Task.FromResult(content);
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error: {ex.Message}");
            }
        }

        private static Task<string> WriteFileAsync(JObject args)
        {
            try
            {
                string relPath = (string)args["path"];
                string content = (string)args["content"];

                if (string.IsNullOrWhiteSpace(relPath))
                    return Task.FromResult("Error: path must be a non-empty string");
                if (content == null)
                    return Task.FromResult("Error: content must be a string");

                if (!IsSafe(relPath))
                    return Task.FromResult("Error: path escapes workspace");

                string fullPath = Path.Combine(WorkspaceRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
                string dir      = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(fullPath, content, Encoding.UTF8);
                return Task.FromResult($"Wrote {relPath}");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------
        // Path safety
        // ----------------------------------------------------------------

        /// <summary>
        /// Returns true if <paramref name="relativePath"/> stays within <see cref="WorkspaceRoot"/>.
        /// </summary>
        private static bool IsSafe(string relativePath)
        {
            try
            {
                string normalised = Path.GetFullPath(
                    Path.Combine(WorkspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                string root = Path.GetFullPath(WorkspaceRoot);

                // Must start with workspace root + separator (or be exactly the root)
                return normalised.StartsWith(root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalised, root, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
