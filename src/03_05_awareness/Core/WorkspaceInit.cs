using System;
using System.IO;

namespace FourthDevs.Awareness.Core
{
    internal static class WorkspaceInit
    {
        public static void EnsureWorkspace()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] dirs = {
                "workspace/profile/user",
                "workspace/profile/agent",
                "workspace/environment",
                "workspace/memory/episodic",
                "workspace/memory/factual",
                "workspace/memory/procedural",
                "workspace/notes/scout",
                "workspace/system/chat",
                "workspace/system/awareness",
                "workspace/traces"
            };

            foreach (string dir in dirs)
            {
                string fullPath = Path.Combine(baseDir, dir);
                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);
            }
        }

        public static string WorkspacePath(string relativePath)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace", relativePath);
        }

        public static string BaseDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }
    }
}
