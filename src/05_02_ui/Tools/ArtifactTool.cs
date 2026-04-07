using System;
using FourthDevs.ChatUi.Data;
using FourthDevs.ChatUi.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Tools
{
    internal static class ArtifactTool
    {
        public static JObject CreateArtifactDef()
        {
            return new JObject
            {
                ["type"] = "function",
                ["name"] = "create_artifact",
                ["description"] = "Create a rich content artifact (markdown, JSON, text, or file).",
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["kind"] = new JObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JArray("markdown", "json", "text", "file"),
                            ["description"] = "Type of artifact to create"
                        },
                        ["title"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Title for the artifact"
                        },
                        ["description"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Brief description of the artifact"
                        },
                        ["content"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "The content of the artifact"
                        }
                    },
                    ["required"] = new JArray("kind", "title", "content")
                }
            };
        }

        public static ToolResult CreateArtifact(JObject args, string dataDir)
        {
            string kind = args["kind"]?.ToString() ?? "text";
            string title = args["title"]?.ToString() ?? "Untitled";
            string description = args["description"]?.ToString();
            string content = args["content"]?.ToString() ?? "";

            string artifactId = "art_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string slug = ToolHelpers.Slugify(title);
            string ext = kind == "markdown" ? ".md" : kind == "json" ? ".json" : ".txt";
            string relativePath = "artifacts/" + slug + ext;

            ToolHelpers.PersistFile(dataDir, relativePath, content);

            string preview = content.Length > 500 ? content.Substring(0, 500) + "\n..." : content;

            var artifact = new ArtifactEvent
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 12),
                Type = "artifact",
                MessageId = "",
                Seq = 0,
                At = DateTime.UtcNow.ToString("o"),
                ArtifactId = artifactId,
                Kind = kind,
                Title = title,
                Description = description,
                Path = relativePath,
                Preview = preview
            };

            return new ToolResult
            {
                Ok = true,
                Output = new JObject
                {
                    ["artifactId"] = artifactId,
                    ["path"] = relativePath
                },
                Artifact = artifact
            };
        }
    }
}
