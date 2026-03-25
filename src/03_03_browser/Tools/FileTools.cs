using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Browser.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Browser.Tools
{
    internal static class FileTools
    {
        private static string GetWorkspaceDir()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");
        }

        private static string SafePath(string relativePath)
        {
            string workspace = GetWorkspaceDir();
            Directory.CreateDirectory(workspace);
            string full = Path.GetFullPath(Path.Combine(workspace, relativePath));
            if (!full.StartsWith(workspace, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path escapes workspace: " + relativePath);
            return full;
        }

        public static List<LocalToolDefinition> CreateFileTools()
        {
            return new List<LocalToolDefinition>
            {
                new LocalToolDefinition
                {
                    Name = "fs_read",
                    Description = "Read a text file from the workspace directory.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Relative path within workspace" }
                        },
                        required = new[] { "path" }
                    }),
                    Handler = async (args) =>
                    {
                        string path = args["path"]?.ToString();
                        if (string.IsNullOrEmpty(path))
                            return JsonConvert.SerializeObject(new { error = "path is required" });

                        await Task.CompletedTask;
                        try
                        {
                            string full = SafePath(path);
                            if (!File.Exists(full))
                                return JsonConvert.SerializeObject(new { error = "File not found: " + path });
                            return File.ReadAllText(full);
                        }
                        catch (Exception ex)
                        {
                            return JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }
                },

                new LocalToolDefinition
                {
                    Name = "fs_write",
                    Description = "Write or create a text file in the workspace directory. Supports append mode.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Relative path within workspace" },
                            content = new { type = "string", description = "Text content to write" },
                            append = new { type = "boolean", description = "If true, append to existing file" }
                        },
                        required = new[] { "path", "content" }
                    }),
                    Handler = async (args) =>
                    {
                        string path = args["path"]?.ToString();
                        string content = args["content"]?.ToString() ?? string.Empty;
                        bool append = args["append"]?.Value<bool>() ?? false;

                        if (string.IsNullOrEmpty(path))
                            return JsonConvert.SerializeObject(new { error = "path is required" });

                        await Task.CompletedTask;
                        try
                        {
                            string full = SafePath(path);
                            Directory.CreateDirectory(Path.GetDirectoryName(full));
                            if (append)
                                File.AppendAllText(full, content);
                            else
                                File.WriteAllText(full, content);

                            return JsonConvert.SerializeObject(new
                            {
                                status = "ok",
                                path,
                                bytes = content.Length
                            });
                        }
                        catch (Exception ex)
                        {
                            return JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }
                },

                new LocalToolDefinition
                {
                    Name = "fs_list",
                    Description = "List files in a directory within the workspace.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Relative directory path within workspace (use '.' for root)" }
                        },
                        required = new[] { "path" }
                    }),
                    Handler = async (args) =>
                    {
                        string path = args["path"]?.ToString() ?? ".";
                        await Task.CompletedTask;
                        try
                        {
                            string full = SafePath(path);
                            if (!Directory.Exists(full))
                                return JsonConvert.SerializeObject(new { error = "Directory not found: " + path });

                            var entries = new List<object>();
                            foreach (string dir in Directory.GetDirectories(full))
                                entries.Add(new { name = Path.GetFileName(dir), type = "dir" });
                            foreach (string file in Directory.GetFiles(full))
                                entries.Add(new { name = Path.GetFileName(file), type = "file" });

                            return JsonConvert.SerializeObject(entries);
                        }
                        catch (Exception ex)
                        {
                            return JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }
                }
            };
        }
    }
}
