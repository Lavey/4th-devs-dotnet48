using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FourthDevs.FilesMcp.Lib
{
    internal static class FileTypeDetector
    {
        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".cs", ".js", ".ts", ".jsx", ".tsx", ".json", ".xml",
            ".html", ".htm", ".css", ".scss", ".less", ".yaml", ".yml", ".toml",
            ".ini", ".cfg", ".config", ".sh", ".bash", ".zsh", ".fish",
            ".bat", ".cmd", ".ps1", ".py", ".rb", ".go", ".java", ".cpp",
            ".c", ".h", ".hpp", ".hxx", ".cc", ".cxx", ".rs", ".swift",
            ".kt", ".kts", ".scala", ".clj", ".cljs", ".elm", ".ex", ".exs",
            ".erl", ".hrl", ".hs", ".lhs", ".ml", ".mli", ".fs", ".fsx",
            ".sql", ".csv", ".tsv", ".log", ".diff", ".patch", ".gitignore",
            ".gitattributes", ".editorconfig", ".env", ".properties",
            ".gradle", ".maven", ".make", ".mk", ".dockerfile", ".tf",
            ".hcl", ".nomad", ".vue", ".svelte", ".astro", ".mdx",
            ".rst", ".asciidoc", ".tex", ".bib", ".r", ".lua", ".pl", ".pm",
            ".php", ".phtml", ".asp", ".aspx", ".cshtml", ".razor"
        };

        private const int FileSampleSizeBytes = 8192;
        private const double NonPrintableCharThreshold = 0.3;

        public static bool IsTextFile(string filePath)
        {
            string ext = Path.GetExtension(filePath);
            if (!string.IsNullOrEmpty(ext) && TextExtensions.Contains(ext))
                return true;

            // Fallback: sample the file
            try
            {
                byte[] buffer = new byte[FileSampleSizeBytes];
                int bytesRead;
                using (var fs = File.OpenRead(filePath))
                    bytesRead = fs.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0) return true;

                // Check for null bytes (strong binary indicator)
                int nullBytes = 0;
                int nonPrintable = 0;
                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 0) nullBytes++;
                    else if (buffer[i] < 32 && buffer[i] != 9 && buffer[i] != 10 && buffer[i] != 13)
                        nonPrintable++;
                }

                if (nullBytes > 0) return false;
                double ratio = (double)nonPrintable / bytesRead;
                return ratio < NonPrintableCharThreshold;
            }
            catch
            {
                return false;
            }
        }
    }
}
