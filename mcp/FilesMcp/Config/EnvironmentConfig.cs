using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

namespace FourthDevs.FilesMcp.Config
{
    internal class MountPoint
    {
        public string Alias { get; set; }
        public string AbsolutePath { get; set; }
    }

    internal class EnvironmentConfig
    {
        public List<MountPoint> MountPoints { get; private set; }
        public string LogLevel { get; private set; }
        public long MaxFileSize { get; private set; }

        private EnvironmentConfig() { }

        public static EnvironmentConfig Load()
        {
            var config = new EnvironmentConfig();

            string logLevel = GetSetting("LOG_LEVEL") ?? "info";
            config.LogLevel = logLevel.ToLowerInvariant();

            string maxFileSizeStr = GetSetting("MAX_FILE_SIZE");
            config.MaxFileSize = 1048576; // 1 MB default
            if (!string.IsNullOrWhiteSpace(maxFileSizeStr) && long.TryParse(maxFileSizeStr, out long maxFs))
                config.MaxFileSize = maxFs;

            config.MountPoints = new List<MountPoint>();

            string fsRoots = GetSetting("FS_ROOTS");
            if (string.IsNullOrWhiteSpace(fsRoots))
                fsRoots = GetSetting("FS_ROOT");

            if (!string.IsNullOrWhiteSpace(fsRoots))
            {
                var parts = fsRoots.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;
                    config.MountPoints.Add(ParseMountSpec(trimmed));
                }
            }

            if (config.MountPoints.Count == 0)
            {
                string cwd = Directory.GetCurrentDirectory();
                config.MountPoints.Add(new MountPoint
                {
                    Alias = Path.GetFileName(cwd) ?? "root",
                    AbsolutePath = cwd
                });
            }

            return config;
        }

        private static MountPoint ParseMountSpec(string spec)
        {
            // Format: "alias:/absolute/path" or just "/absolute/path" or "C:\path"
            // Detect alias by looking for ':' that is not a drive letter (i.e., position > 1)
            int colonPos = spec.IndexOf(':');
            if (colonPos > 1)
            {
                // Could be "alias:/path" or "C:\path" - check if the part before colon has no slashes
                string potentialAlias = spec.Substring(0, colonPos);
                if (!potentialAlias.Contains("/") && !potentialAlias.Contains("\\"))
                {
                    string path = spec.Substring(colonPos + 1);
                    return new MountPoint
                    {
                        Alias = potentialAlias,
                        AbsolutePath = Path.GetFullPath(path)
                    };
                }
            }

            // Just a path
            string absPath = Path.GetFullPath(spec);
            return new MountPoint
            {
                Alias = Path.GetFileName(absPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "root",
                AbsolutePath = absPath
            };
        }

        private static string GetSetting(string key)
        {
            string value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            try
            {
                value = ConfigurationManager.AppSettings[key];
            }
            catch
            {
                // Ignore configuration errors
            }
            return value;
        }
    }
}
