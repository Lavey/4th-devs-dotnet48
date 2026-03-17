using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FourthDevs.Mcp.Files.Config;

namespace FourthDevs.Mcp.Files.Lib
{
    internal class PathResolver
    {
        private readonly List<MountPoint> _mounts;

        public PathResolver(EnvironmentConfig config)
        {
            _mounts = config.MountPoints;
        }

        /// <summary>
        /// Resolve a virtual or absolute path to an absolute filesystem path,
        /// ensuring it stays within configured mounts. Returns null on sandbox violation.
        /// </summary>
        public string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            // Normalize separators
            path = path.Replace('/', Path.DirectorySeparatorChar)
                       .Replace('\\', Path.DirectorySeparatorChar);

            // Case 1: looks like an absolute FS path
            if (Path.IsPathRooted(path))
            {
                string fullPath = Path.GetFullPath(path);
                if (IsWithinAnyMount(fullPath))
                    return fullPath;
                return null;
            }

            // Case 2: alias-based path "alias\subpath"
            int sepIdx = path.IndexOf(Path.DirectorySeparatorChar);
            if (sepIdx > 0)
            {
                string potentialAlias = path.Substring(0, sepIdx);
                string rest = path.Substring(sepIdx + 1);
                foreach (var mount in _mounts)
                {
                    if (string.Equals(mount.Alias, potentialAlias, StringComparison.OrdinalIgnoreCase))
                    {
                        string resolved = Path.GetFullPath(Path.Combine(mount.AbsolutePath, rest));
                        if (IsWithinMount(resolved, mount))
                            return resolved;
                        return null;
                    }
                }
            }
            else
            {
                // Could be just the alias itself (list mount root)
                foreach (var mount in _mounts)
                {
                    if (string.Equals(mount.Alias, path, StringComparison.OrdinalIgnoreCase))
                        return mount.AbsolutePath;
                }
            }

            // Case 3: relative path - resolve against first mount
            if (_mounts.Count > 0)
            {
                var firstMount = _mounts[0];
                string resolved = Path.GetFullPath(Path.Combine(firstMount.AbsolutePath, path));
                if (IsWithinMount(resolved, firstMount))
                    return resolved;
            }

            return null;
        }

        /// <summary>
        /// Check if an absolute path is within any configured mount.
        /// </summary>
        public bool IsWithinAnyMount(string absolutePath)
        {
            return _mounts.Any(m => IsWithinMount(absolutePath, m));
        }

        /// <summary>
        /// Return list of available mount aliases and paths for display.
        /// </summary>
        public IEnumerable<string> GetMountDescriptions()
        {
            foreach (var mount in _mounts)
                yield return $"{mount.Alias} → {mount.AbsolutePath}";
        }

        public List<MountPoint> GetMounts()
        {
            return _mounts;
        }

        private static bool IsWithinMount(string absolutePath, MountPoint mount)
        {
            string mountPath = EnsureTrailingSeparator(mount.AbsolutePath);
            string normalized = EnsureTrailingSeparator(absolutePath);
            return normalized.StartsWith(mountPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(absolutePath, mount.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                return path + Path.DirectorySeparatorChar;
            return path;
        }
    }
}
