using System;
using System.IO;
using System.Text;
using FourthDevs.FilesMcp.Config;
using FourthDevs.FilesMcp.Lib;
using FourthDevs.FilesMcp.Utils;
using Newtonsoft.Json.Linq;

namespace FourthDevs.FilesMcp.Tools
{
    internal class FsManageTool
    {
        private readonly PathResolver _resolver;

        public FsManageTool(EnvironmentConfig config)
        {
            _resolver = new PathResolver(config);
        }

        public JObject GetToolDefinition()
        {
            return JObject.Parse(@"{
                ""name"": ""fs_manage"",
                ""description"": ""File system management operations (delete, rename, move, copy, mkdir, stat)"",
                ""inputSchema"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""operation"": {""type"": ""string"", ""enum"": [""delete"", ""rename"", ""move"", ""copy"", ""mkdir"", ""stat""]},
                        ""path"": {""type"": ""string"", ""description"": ""Source path""},
                        ""target"": {""type"": ""string"", ""description"": ""Target path (for rename/move/copy)""},
                        ""recursive"": {""type"": ""boolean"", ""description"": ""Recursive operation (default: false)""},
                        ""force"": {""type"": ""boolean"", ""description"": ""Overwrite existing (default: false)""}
                    },
                    ""required"": [""operation"", ""path""]
                }
            }");
        }

        public string Execute(JObject args)
        {
            string operation = (string)args["operation"];
            string path      = (string)args["path"];
            string target    = (string)args["target"];
            bool recursive   = (bool?)args["recursive"] ?? false;
            bool force       = (bool?)args["force"] ?? false;

            if (string.IsNullOrWhiteSpace(operation))
                return "Error: 'operation' parameter is required.";
            if (string.IsNullOrWhiteSpace(path))
                return "Error: 'path' parameter is required.";

            string resolved = _resolver.Resolve(path);
            if (resolved == null)
                return $"Error: Access denied. Path '{path}' is outside configured mount points.";

            try
            {
                switch (operation)
                {
                    case "delete":
                        return Delete(resolved, recursive);
                    case "rename":
                    case "move":
                        return Move(resolved, target, force, operation);
                    case "copy":
                        return Copy(resolved, target, force);
                    case "mkdir":
                        return MakeDirectory(resolved);
                    case "stat":
                        return Stat(resolved);
                    default:
                        return $"Error: Unknown operation '{operation}'.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"fs_manage error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string Delete(string path, bool recursive)
        {
            if (Directory.Exists(path))
            {
                if (!recursive)
                {
                    var entries = Directory.GetFileSystemEntries(path);
                    if (entries.Length > 0)
                        return $"Error: Directory '{path}' is not empty. Use recursive=true to delete non-empty directories.";
                }
                Directory.Delete(path, recursive);
                return $"Deleted directory: {path}";
            }
            if (File.Exists(path))
            {
                File.Delete(path);
                return $"Deleted file: {path}";
            }
            return $"Error: Path not found: {path}";
        }

        private string Move(string source, string targetArg, bool force, string opName)
        {
            if (string.IsNullOrWhiteSpace(targetArg))
                return $"Error: 'target' parameter is required for '{opName}' operation.";

            string targetResolved = _resolver.Resolve(targetArg);
            if (targetResolved == null)
                return $"Error: Access denied. Target path '{targetArg}' is outside configured mount points.";

            if (File.Exists(source))
            {
                if (File.Exists(targetResolved))
                {
                    if (!force)
                        return $"Error: Target file already exists: {targetResolved}\nUse force=true to overwrite.";
                    File.Delete(targetResolved);
                }
                string dir = Path.GetDirectoryName(targetResolved);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.Move(source, targetResolved);
                return $"{(opName == "rename" ? "Renamed" : "Moved")}: {source} → {targetResolved}";
            }
            if (Directory.Exists(source))
            {
                if (Directory.Exists(targetResolved))
                {
                    if (!force)
                        return $"Error: Target directory already exists: {targetResolved}\nUse force=true to overwrite.";
                    Directory.Delete(targetResolved, true);
                }
                Directory.Move(source, targetResolved);
                return $"{(opName == "rename" ? "Renamed" : "Moved")}: {source} → {targetResolved}";
            }
            return $"Error: Source path not found: {source}";
        }

        private string Copy(string source, string targetArg, bool force)
        {
            if (string.IsNullOrWhiteSpace(targetArg))
                return "Error: 'target' parameter is required for 'copy' operation.";

            string targetResolved = _resolver.Resolve(targetArg);
            if (targetResolved == null)
                return $"Error: Access denied. Target path '{targetArg}' is outside configured mount points.";

            if (File.Exists(source))
            {
                if (File.Exists(targetResolved) && !force)
                    return $"Error: Target file already exists: {targetResolved}\nUse force=true to overwrite.";
                string dir = Path.GetDirectoryName(targetResolved);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(source, targetResolved, force);
                return $"Copied: {source} → {targetResolved}";
            }
            if (Directory.Exists(source))
            {
                CopyDirectory(source, targetResolved, force);
                return $"Copied directory: {source} → {targetResolved}";
            }
            return $"Error: Source path not found: {source}";
        }

        private static void CopyDirectory(string source, string target, bool overwrite)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
            {
                string dest = Path.Combine(target, Path.GetFileName(file));
                File.Copy(file, dest, overwrite);
            }
            foreach (var dir in Directory.GetDirectories(source))
            {
                string dest = Path.Combine(target, Path.GetFileName(dir));
                CopyDirectory(dir, dest, overwrite);
            }
        }

        private static string MakeDirectory(string path)
        {
            if (Directory.Exists(path))
                return $"Directory already exists: {path}";
            Directory.CreateDirectory(path);
            return $"Created directory: {path}";
        }

        private string Stat(string path)
        {
            var sb = new StringBuilder();

            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                string checksum = ChecksumHelper.ComputeFileChecksum(path);
                sb.AppendLine($"Type:     file");
                sb.AppendLine($"Path:     {path}");
                sb.AppendLine($"Name:     {info.Name}");
                sb.AppendLine($"Size:     {FormatSize(info.Length)} ({info.Length:N0} bytes)");
                sb.AppendLine($"Created:  {info.CreationTimeUtc:yyyy-MM-ddTHH:mm:ssZ}");
                sb.AppendLine($"Modified: {info.LastWriteTimeUtc:yyyy-MM-ddTHH:mm:ssZ}");
                sb.AppendLine($"Checksum (SHA256): {checksum}");
                sb.AppendLine($"Text:     {FileTypeDetector.IsTextFile(path)}");
                return sb.ToString();
            }

            if (Directory.Exists(path))
            {
                var info = new DirectoryInfo(path);
                int fileCount = 0, dirCount = 0;
                long totalSize = 0;
                try
                {
                    foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        fileCount++;
                        try { totalSize += new FileInfo(f).Length; } catch { }
                    }
                    dirCount = Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Length;
                }
                catch { }

                sb.AppendLine($"Type:      directory");
                sb.AppendLine($"Path:      {path}");
                sb.AppendLine($"Name:      {info.Name}");
                sb.AppendLine($"Files:     {fileCount:N0}");
                sb.AppendLine($"Dirs:      {dirCount:N0}");
                sb.AppendLine($"TotalSize: {FormatSize(totalSize)}");
                sb.AppendLine($"Created:   {info.CreationTimeUtc:yyyy-MM-ddTHH:mm:ssZ}");
                sb.AppendLine($"Modified:  {info.LastWriteTimeUtc:yyyy-MM-ddTHH:mm:ssZ}");
                return sb.ToString();
            }

            return $"Error: Path not found: {path}";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F1} GB";
        }
    }
}
