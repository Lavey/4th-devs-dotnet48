using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Wonderlands.Core;
using FourthDevs.Wonderlands.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Wonderlands.Tools
{
    public static class ArtifactShared
    {
        public const int MaxReadArtifactChars = 12000;

        private static string ArtifactFilesRoot(Runtime rt) => Path.Combine(rt.DataDir, "files");

        private static string ArtifactFilePath(Runtime rt, string artifactPath)
        {
            var parts = artifactPath.Split('/');
            var result = ArtifactFilesRoot(rt);
            foreach (var p in parts) result = Path.Combine(result, p);
            return result;
        }

        public static string NormalizeArtifactPath(string artifactPath)
        {
            if (string.IsNullOrWhiteSpace(artifactPath))
                throw new Exception("Artifact path must be a non-empty relative path");
            var trimmed = artifactPath.Replace('\\', '/').Trim();
            if (trimmed.StartsWith("/"))
                throw new Exception("Artifact path must be relative, not absolute");
            var parts = trimmed.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var stack = new System.Collections.Generic.Stack<string>();
            foreach (var part in parts)
            {
                if (part == ".") continue;
                if (part == "..")
                {
                    if (stack.Count == 0) throw new Exception("Artifact path cannot escape the artifact directory");
                    stack.Pop();
                }
                else stack.Push(part);
            }
            if (stack.Count == 0) throw new Exception("Artifact path cannot escape the artifact directory");
            return string.Join("/", stack.Reverse());
        }

        public static string ReadArtifactContent(Runtime rt, string artifactPath)
        {
            return File.ReadAllText(ArtifactFilePath(rt, artifactPath));
        }

        public static void WriteArtifactContent(Runtime rt, string artifactPath, string content)
        {
            var fullPath = ArtifactFilePath(rt, artifactPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, content);
        }

        public static async Task<Artifact> GetLatestArtifactByPath(string sessionId, string artifactPath, Runtime rt)
        {
            var matches = await rt.Artifacts.Find(a => a.SessionId == sessionId && a.Path == artifactPath);
            return matches.OrderByDescending(a => a.Version).ThenByDescending(a => a.CreatedAt).FirstOrDefault();
        }

        public static async Task<Artifact> UpsertArtifact(Runtime rt, Job job, string artifactPath, string kind, string content)
        {
            var metadata = new JObject
            {
                ["chars"] = content.Length,
                ["format"] = artifactPath.EndsWith(".md") ? "markdown" : "text"
            };

            var existing = await GetLatestArtifactByPath(job.SessionId, artifactPath, rt);
            if (existing == null)
            {
                var created = await RuntimeHelpers.AddArtifact(rt, job.SessionId, kind, artifactPath, job.Id, metadata);
                await RuntimeHelpers.EnsureRelation(rt, job.SessionId, "job", job.Id, "produces", "artifact", created.Id);
                return created;
            }

            var updated = await rt.Artifacts.Update(existing.Id, a =>
            {
                a.Version = existing.Version + 1;
                a.JobId = job.Id;
                a.Metadata = metadata;
            });
            await RuntimeHelpers.EnsureRelation(rt, job.SessionId, "job", job.Id, "produces", "artifact", updated.Id);
            return updated;
        }
    }
}
