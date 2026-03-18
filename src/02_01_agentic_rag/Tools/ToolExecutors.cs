using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson06_AgenticRag.Tools
{
    /// <summary>
    /// Implements the runtime execution of the Agentic RAG file tools.
    /// These run in-process and mirror the MCP file-server capabilities
    /// (list, search, read) from 02_01_agentic_rag/src/mcp/client.js.
    /// </summary>
    internal static class ToolExecutors
    {
        // Workspace root injected at startup by Program
        internal static string WorkspaceRoot { get; set; }

        // ----------------------------------------------------------------
        // list_files — mirrors MCP "list" tool
        // ----------------------------------------------------------------

        internal static object ExecuteListFiles(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? ".";
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null)
                return new { error = "Access denied: path outside workspace." };
            if (!Directory.Exists(absPath))
                return new { error = "Directory not found: " + rel };

            var entries = new List<object>();
            foreach (string d in Directory.GetDirectories(absPath))
                entries.Add(new { type = "directory", name = Path.GetFileName(d) });
            foreach (string f in Directory.GetFiles(absPath))
                entries.Add(new { type = "file", name = Path.GetFileName(f) });

            return new { path = rel, entries };
        }

        // ----------------------------------------------------------------
        // search_files — mirrors MCP "search" tool
        // ----------------------------------------------------------------

        internal static object ExecuteSearchFiles(JObject args)
        {
            string query   = args["query"]?.ToString()   ?? string.Empty;
            string pattern = args["pattern"]?.ToString() ?? "*";

            if (string.IsNullOrWhiteSpace(query))
                return new { error = "\"query\" is required." };

            var results = new List<object>();

            string[] files;
            try
            {
                files = Directory.GetFiles(WorkspaceRoot, pattern, SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                return new { error = "Pattern error: " + ex.Message };
            }

            foreach (string filePath in files)
            {
                string rel = filePath.Substring(WorkspaceRoot.Length)
                                     .TrimStart(Path.DirectorySeparatorChar,
                                                Path.AltDirectorySeparatorChar);
                try
                {
                    string   content = File.ReadAllText(filePath, Encoding.UTF8);
                    string[] lines   = content.Split('\n');

                    var matchingLines = new List<object>();
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchingLines.Add(new
                            {
                                line = i + 1,
                                text = lines[i].TrimEnd('\r')
                            });
                        }
                    }

                    if (matchingLines.Count > 0)
                        results.Add(new { file = rel, matches = matchingLines });
                }
                catch
                {
                    // skip unreadable files silently
                }
            }

            return new { query, count = results.Count, results };
        }

        // ----------------------------------------------------------------
        // read_file — mirrors MCP "read" tool
        // ----------------------------------------------------------------

        internal static object ExecuteReadFile(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? string.Empty;
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null)
                return new { error = "Access denied: path outside workspace." };
            if (!File.Exists(absPath))
                return new { error = "File not found: " + rel };

            return new { path = rel, content = File.ReadAllText(absPath, Encoding.UTF8) };
        }

        // ----------------------------------------------------------------
        // Path guard — prevents directory traversal
        // ----------------------------------------------------------------

        internal static string ResolveWorkspacePath(string relativePath)
        {
            string full = Path.GetFullPath(Path.Combine(WorkspaceRoot, relativePath));

            // OrdinalIgnoreCase matches the convention used across all Lesson projects
            // and is correct for Windows (the primary target of .NET Framework 4.8)
            // where the file system is case-insensitive.
            return full.StartsWith(WorkspaceRoot + Path.DirectorySeparatorChar,
                                   StringComparison.OrdinalIgnoreCase)
                   || string.Equals(full, WorkspaceRoot, StringComparison.OrdinalIgnoreCase)
                ? full
                : null;
        }
    }
}
