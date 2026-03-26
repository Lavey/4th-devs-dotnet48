using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ContextAgent.Agent
{
    internal static class AgentTools
    {
        private static string _workspaceRoot;

        public static void Init(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        public static JArray GetToolDefinitions()
        {
            return new JArray
            {
                new JObject
                {
                    ["type"] = "function",
                    ["name"] = "read_file",
                    ["description"] = "Read a file from the workspace directory.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["path"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "File path relative to workspace root."
                            }
                        },
                        ["required"] = new JArray("path")
                    }
                },
                new JObject
                {
                    ["type"] = "function",
                    ["name"] = "write_file",
                    ["description"] = "Write content to a file in the workspace directory.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["path"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "File path relative to workspace root."
                            },
                            ["content"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Content to write."
                            }
                        },
                        ["required"] = new JArray("path", "content")
                    }
                }
            };
        }

        public static async Task<string> ExecuteAsync(string toolName, string arguments)
        {
            JObject args;
            try { args = JObject.Parse(arguments); }
            catch { return "Error: invalid JSON arguments"; }

            if (toolName == "read_file")
                return await ReadFileAsync((string)args["path"]).ConfigureAwait(false);
            if (toolName == "write_file")
                return await WriteFileAsync((string)args["path"], (string)args["content"]).ConfigureAwait(false);

            return "Error: unknown tool " + toolName;
        }

        private static Task<string> ReadFileAsync(string relativePath)
        {
            try
            {
                string fullPath = ResolvePath(relativePath);
                if (fullPath == null)
                    return Task.FromResult("Error: path escapes workspace directory.");
                if (!File.Exists(fullPath))
                    return Task.FromResult("Error: file not found: " + relativePath);

                string content = File.ReadAllText(fullPath);
                return Task.FromResult(content);
            }
            catch (Exception ex)
            {
                return Task.FromResult("Error reading file: " + ex.Message);
            }
        }

        private static Task<string> WriteFileAsync(string relativePath, string content)
        {
            try
            {
                string fullPath = ResolvePath(relativePath);
                if (fullPath == null)
                    return Task.FromResult("Error: path escapes workspace directory.");

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, content ?? "");
                return Task.FromResult("OK: written " + relativePath);
            }
            catch (Exception ex)
            {
                return Task.FromResult("Error writing file: " + ex.Message);
            }
        }

        private static string ResolvePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            // Normalize separators
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar)
                                       .Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, relativePath));
            string workspaceFull = Path.GetFullPath(_workspaceRoot);
            if (!fullPath.StartsWith(workspaceFull, StringComparison.OrdinalIgnoreCase))
                return null;
            return fullPath;
        }
    }
}
