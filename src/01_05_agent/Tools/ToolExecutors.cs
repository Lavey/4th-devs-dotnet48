using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson05_Agent.Tools
{
    /// <summary>
    /// Implements the runtime execution of all built-in agent tools.
    /// Mirrors 01_05_agent/src/tools/registry.ts in the source repo.
    /// </summary>
    internal static class AgentToolExecutors
    {
        // Workspace root is injected at startup via Program.
        internal static string WorkspaceRoot { get; set; }

        // Send-message needs access to the shared agent/session stores.
        internal static Dictionary<string, AgentRunData> AgentRuns  { get; set; }
        internal static Dictionary<string, SessionData>  Sessions   { get; set; }
        internal static object                           StateLock  { get; set; }

        // ----------------------------------------------------------------
        // Dispatcher (mirrors tools/registry execute())
        // ----------------------------------------------------------------

        internal static object ExecuteCalculator(JObject args)
        {
            string expr = args["expression"]?.ToString() ?? string.Empty;
            try
            {
                object raw = new System.Data.DataTable().Compute(expr, null);
                double val = Convert.ToDouble(raw);
                return new { expression = expr, result = val };
            }
            catch (Exception ex)
            {
                return new { expression = expr, error = "Evaluation failed: " + ex.Message };
            }
        }

        internal static object ExecuteListFiles(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? ".";
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };
            if (!Directory.Exists(absPath)) return new { error = "Directory not found: " + rel };

            var entries = new List<object>();
            foreach (string d in Directory.GetDirectories(absPath))
                entries.Add(new { type = "directory", name = Path.GetFileName(d) });
            foreach (string f in Directory.GetFiles(absPath))
                entries.Add(new { type = "file", name = Path.GetFileName(f) });

            return new { path = rel, entries };
        }

        internal static object ExecuteReadFile(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? string.Empty;
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };
            if (!File.Exists(absPath)) return new { error = "File not found: " + rel };

            return new { path = rel, content = File.ReadAllText(absPath, Encoding.UTF8) };
        }

        internal static object ExecuteWriteFile(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? string.Empty;
            string content = args["content"]?.ToString() ?? string.Empty;
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };

            string dir = Path.GetDirectoryName(absPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(absPath, content, Encoding.UTF8);
            return new { success = true, path = rel, bytesWritten = Encoding.UTF8.GetByteCount(content) };
        }

        internal static object ExecuteSendMessage(JObject args)
        {
            string to      = args["to"]?.ToString();
            string message = args["message"]?.ToString();

            if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(message))
                return new { error = "Both \"to\" and \"message\" are required." };

            lock (StateLock)
            {
                AgentRunData targetRun;
                if (!AgentRuns.TryGetValue(to, out targetRun))
                    return new { error = string.Format("Agent not found: {0}", to) };

                SessionData targetSession;
                if (!Sessions.TryGetValue(targetRun.SessionId, out targetSession))
                    return new { error = string.Format("Session not found for agent: {0}", to) };

                targetSession.History.Add(new
                {
                    type    = "message",
                    role    = "system",
                    content = message
                });
            }

            return new { ok = true, output = string.Format("Message delivered to agent {0}", to) };
        }

        // ----------------------------------------------------------------
        // Workspace path guard (mirrors workspace sandbox logic)
        // ----------------------------------------------------------------

        internal static string ResolveWorkspacePath(string relativePath)
        {
            string full = Path.GetFullPath(Path.Combine(WorkspaceRoot, relativePath));

            return full.StartsWith(WorkspaceRoot + Path.DirectorySeparatorChar,
                                   StringComparison.OrdinalIgnoreCase)
                   || string.Equals(full, WorkspaceRoot, StringComparison.OrdinalIgnoreCase)
                ? full
                : null;
        }
    }
}
