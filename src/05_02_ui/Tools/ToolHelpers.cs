using System.IO;
using System.Text.RegularExpressions;

namespace FourthDevs.ChatUi.Tools
{
    internal static class ToolHelpers
    {
        /// <summary>
        /// Converts a string to a URL-safe slug.
        /// </summary>
        public static string Slugify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "untitled";
            string s = text.ToLowerInvariant().Trim();
            s = Regex.Replace(s, @"[^a-z0-9\s-]", "");
            s = Regex.Replace(s, @"[\s]+", "-");
            s = Regex.Replace(s, @"-{2,}", "-");
            s = s.Trim('-');
            if (s.Length > 60) s = s.Substring(0, 60).TrimEnd('-');
            return string.IsNullOrEmpty(s) ? "untitled" : s;
        }

        /// <summary>
        /// Writes content to a file inside the data directory, creating
        /// subdirectories as needed.
        /// </summary>
        public static void PersistFile(string dataDir, string relativePath, string content)
        {
            string fullPath = Path.Combine(dataDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(fullPath, content);
        }
    }
}
