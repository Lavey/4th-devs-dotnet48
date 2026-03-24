using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FourthDevs.Events.Helpers
{
    /// <summary>
    /// File-system safety and existence helpers.
    /// </summary>
    internal static class FsHelper
    {
        public static bool Exists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        public static bool IsPathSafe(string basePath, string candidatePath)
        {
            try
            {
                string fullBase = Path.GetFullPath(basePath).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullCandidate = Path.GetFullPath(candidatePath).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return fullCandidate.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
