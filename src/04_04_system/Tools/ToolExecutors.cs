using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentSystem.Tools
{
    /// <summary>
    /// Implements the tool handlers for the 04_04_system agent.
    /// File tools replace the MCP files server with direct filesystem access
    /// scoped to the workspace directory.
    ///
    /// Mirrors 04_04_system/src/tools/ (i-am-alice/4th-devs).
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
                case "read_file":     return ReadFileAsync(args);
                case "write_file":    return WriteFileAsync(args);
                case "list_dir":      return ListDirAsync(args);
                case "search_files":  return SearchFilesAsync(args);
                case "sum":           return SumAsync(args);
                case "send_email":    return SendEmailAsync(args);
                default:
                    return Task.FromResult($"Unknown tool: {name}");
            }
        }

        // ----------------------------------------------------------------
        // File tools (replace MCP files server)
        // ----------------------------------------------------------------

        private static Task<string> ReadFileAsync(JObject args)
        {
            try
            {
                string relPath = (string)args["path"];
                if (string.IsNullOrWhiteSpace(relPath))
                    return Task.FromResult("{\"success\":false,\"error\":\"path must be a non-empty string\"}");

                if (!IsSafe(relPath))
                    return Task.FromResult("{\"success\":false,\"error\":\"path escapes workspace\"}");

                string fullPath = ResolvePath(relPath);

                if (Directory.Exists(fullPath))
                {
                    // If a directory is requested, list its contents
                    return ListDirAsync(args);
                }

                if (!File.Exists(fullPath))
                    return Task.FromResult($"{{\"success\":false,\"error\":\"file not found: {EscapeJson(relPath)}\"}}");

                string content = File.ReadAllText(fullPath, Encoding.UTF8);
                var result = new JObject
                {
                    ["success"] = true,
                    ["type"] = "file",
                    ["path"] = relPath,
                    ["content"] = content
                };
                return Task.FromResult(result.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"{{\"success\":false,\"error\":\"{EscapeJson(ex.Message)}\"}}");
            }
        }

        private static Task<string> WriteFileAsync(JObject args)
        {
            try
            {
                string relPath = (string)args["path"];
                string content = (string)args["content"];

                if (string.IsNullOrWhiteSpace(relPath))
                    return Task.FromResult("{\"success\":false,\"error\":\"path must be a non-empty string\"}");
                if (content == null)
                    return Task.FromResult("{\"success\":false,\"error\":\"content must be a string\"}");

                if (!IsSafe(relPath))
                    return Task.FromResult("{\"success\":false,\"error\":\"path escapes workspace\"}");

                string fullPath = ResolvePath(relPath);
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(fullPath, content, Encoding.UTF8);

                var result = new JObject
                {
                    ["success"] = true,
                    ["status"] = "applied",
                    ["result"] = new JObject
                    {
                        ["action"] = File.Exists(fullPath) ? "written" : "created",
                        ["path"] = relPath
                    }
                };
                return Task.FromResult(result.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"{{\"success\":false,\"error\":\"{EscapeJson(ex.Message)}\"}}");
            }
        }

        private static Task<string> ListDirAsync(JObject args)
        {
            try
            {
                string relPath = (string)args["path"] ?? string.Empty;

                if (!IsSafe(relPath))
                    return Task.FromResult("{\"success\":false,\"error\":\"path escapes workspace\"}");

                string fullPath = ResolvePath(relPath);
                if (!Directory.Exists(fullPath))
                    return Task.FromResult($"{{\"success\":false,\"error\":\"directory not found: {EscapeJson(relPath)}\"}}");

                var entries = new List<string>();

                foreach (string dir in Directory.GetDirectories(fullPath))
                    entries.Add(Path.GetFileName(dir) + "/");

                foreach (string file in Directory.GetFiles(fullPath))
                    entries.Add(Path.GetFileName(file));

                entries.Sort(StringComparer.OrdinalIgnoreCase);

                var result = new JObject
                {
                    ["success"] = true,
                    ["type"] = "directory",
                    ["path"] = relPath,
                    ["summary"] = $"{entries.Count} entries",
                    ["entries"] = new JArray(entries.ToArray())
                };
                return Task.FromResult(result.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"{{\"success\":false,\"error\":\"{EscapeJson(ex.Message)}\"}}");
            }
        }

        private static Task<string> SearchFilesAsync(JObject args)
        {
            try
            {
                string query = (string)args["query"];
                string relPath = (string)args["path"] ?? string.Empty;

                if (string.IsNullOrWhiteSpace(query))
                    return Task.FromResult("{\"success\":false,\"error\":\"query must be a non-empty string\"}");

                if (!IsSafe(relPath))
                    return Task.FromResult("{\"success\":false,\"error\":\"path escapes workspace\"}");

                string searchDir = ResolvePath(relPath);
                if (!Directory.Exists(searchDir))
                    return Task.FromResult($"{{\"success\":false,\"error\":\"directory not found: {EscapeJson(relPath)}\"}}");

                var matches = new List<string>();

                foreach (string file in Directory.GetFiles(searchDir, "*.*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string content = File.ReadAllText(file, Encoding.UTF8);
                        if (content.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string relative = file
                                .Substring(WorkspaceRoot.Length)
                                .TrimStart(Path.DirectorySeparatorChar, '/')
                                .Replace(Path.DirectorySeparatorChar, '/');
                            matches.Add(relative);
                        }
                    }
                    catch
                    {
                        // Skip files we can't read
                    }
                }

                matches.Sort(StringComparer.OrdinalIgnoreCase);

                var result = new JObject
                {
                    ["success"] = true,
                    ["query"] = query,
                    ["matches"] = new JArray(matches.ToArray()),
                    ["count"] = matches.Count
                };
                return Task.FromResult(result.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"{{\"success\":false,\"error\":\"{EscapeJson(ex.Message)}\"}}");
            }
        }

        // ----------------------------------------------------------------
        // Local tools
        // ----------------------------------------------------------------

        private static Task<string> SumAsync(JObject args)
        {
            double a = (double)(args["a"] ?? 0);
            double b = (double)(args["b"] ?? 0);
            var result = new JObject { ["result"] = a + b };
            return Task.FromResult(result.ToString(Formatting.None));
        }

        private static Task<string> SendEmailAsync(JObject args)
        {
            try
            {
                string to       = (string)args["to"];
                string subject  = (string)args["subject"];
                string htmlBody = (string)args["html_body"];

                string date      = DateTime.UtcNow.ToString("yyyy-MM-dd");
                string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss-fffZ");
                string dir       = Path.Combine(WorkspaceRoot, "ops", "daily-news", date);
                string filename  = $"sent-{timestamp}.html";
                string filepath  = Path.Combine(dir, filename);

                Directory.CreateDirectory(dir);
                File.WriteAllText(filepath, htmlBody ?? string.Empty, Encoding.UTF8);

                var result = new JObject
                {
                    ["status"]  = "sent (simulated)",
                    ["to"]      = to,
                    ["subject"] = subject,
                    ["path"]    = $"ops/daily-news/{date}/{filename}"
                };
                return Task.FromResult(result.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"{{\"success\":false,\"error\":\"{EscapeJson(ex.Message)}\"}}");
            }
        }

        // ----------------------------------------------------------------
        // Path safety
        // ----------------------------------------------------------------

        private static string ResolvePath(string relativePath)
        {
            return Path.Combine(WorkspaceRoot,
                (relativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Returns true if <paramref name="relativePath"/> stays within <see cref="WorkspaceRoot"/>.
        /// </summary>
        private static bool IsSafe(string relativePath)
        {
            try
            {
                string normalised = Path.GetFullPath(
                    Path.Combine(WorkspaceRoot,
                        (relativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
                string root = Path.GetFullPath(WorkspaceRoot);

                return normalised.StartsWith(root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalised, root, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeJson(string s)
            => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
