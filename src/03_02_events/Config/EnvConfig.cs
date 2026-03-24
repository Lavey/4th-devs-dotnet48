using System;
using System.Configuration;

namespace FourthDevs.Events.Config
{
    /// <summary>
    /// Configuration and environment variable access.
    /// Mirrors env.ts from the TypeScript project.
    /// </summary>
    internal static class EnvConfig
    {
        // Workspace paths
        public static string WorkspacePath
        {
            get
            {
                string configured = Get("WORKSPACE_PATH");
                if (!string.IsNullOrWhiteSpace(configured))
                    return System.IO.Path.GetFullPath(configured);

                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                return System.IO.Path.Combine(exeDir, "workspace");
            }
        }

        public static string TasksPath
        {
            get { return System.IO.Path.Combine(WorkspacePath, "tasks"); }
        }

        public static string ProjectPath
        {
            get { return System.IO.Path.Combine(WorkspacePath, "project"); }
        }

        public static string AgentsPath
        {
            get { return System.IO.Path.Combine(WorkspacePath, "agents"); }
        }

        public static string EventsPath
        {
            get { return System.IO.Path.Combine(WorkspacePath, "events"); }
        }

        public static string GoalPath
        {
            get { return System.IO.Path.Combine(WorkspacePath, "goal.md"); }
        }

        // Config values
        public static string DefaultModel
        {
            get { return Get("OPENAI_MODEL") ?? "gpt-4.1"; }
        }

        public static bool ReadFlag(string key, bool defaultValue)
        {
            string val = Get(key);
            if (string.IsNullOrWhiteSpace(val)) return defaultValue;
            return ParseBoolean(val, defaultValue);
        }

        public static int ParsePositiveInt(string value, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            int result;
            if (int.TryParse(value.Trim(), out result) && result > 0)
                return result;
            return defaultValue;
        }

        public static bool ParseBoolean(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            string v = value.Trim().ToLowerInvariant();
            if (v == "true" || v == "1" || v == "yes") return true;
            if (v == "false" || v == "0" || v == "no") return false;
            return defaultValue;
        }

        public static string Get(string key)
        {
            string val = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
        }
    }
}
