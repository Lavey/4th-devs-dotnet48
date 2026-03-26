using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.VideoGeneration.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.VideoGeneration.Native
{
    /// <summary>
    /// Defines the tool functions exposed to the OpenAI model for video generation:
    /// read_file, write_file, list_files, create_image, and image_to_video.
    /// </summary>
    internal static class VideoGenTools
    {
        private const string WorkspaceRoot = "workspace";
        private const string OutputDir     = "workspace/output";
        private const string PromptsDir    = "workspace/prompts";

        public static List<VideoGenToolDefinition> CreateTools()
        {
            return new List<VideoGenToolDefinition>
            {
                BuildReadFileTool(),
                BuildWriteFileTool(),
                BuildListFilesTool(),
                BuildCreateImageTool(),
                BuildImageToVideoTool()
            };
        }

        // ----------------------------------------------------------------
        // read_file
        // ----------------------------------------------------------------

        private static VideoGenToolDefinition BuildReadFileTool()
        {
            return new VideoGenToolDefinition
            {
                Name        = "read_file",
                Description = "Read a text file from the workspace directory.",
                Parameters  = JObject.Parse(@"{
  ""type"": ""object"",
  ""properties"": {
    ""path"": {
      ""type"": ""string"",
      ""description"": ""Workspace-relative path to the file (e.g. 'template.json' or 'prompts/fox_123.json').""
    }
  },
  ""required"": [""path""],
  ""additionalProperties"": false
}"),
                Handler = async (args) =>
                {
                    string relPath  = args["path"]?.ToString() ?? string.Empty;
                    string fullPath = ResolvePath(relPath);
                    LogTool("read_file", relPath);

                    if (!File.Exists(fullPath))
                        return new { error = "File not found: " + relPath };

                    string content = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                    return new { path = relPath, content = content };
                }
            };
        }

        // ----------------------------------------------------------------
        // write_file
        // ----------------------------------------------------------------

        private static VideoGenToolDefinition BuildWriteFileTool()
        {
            return new VideoGenToolDefinition
            {
                Name        = "write_file",
                Description = "Write content to a file in the workspace directory. Creates directories as needed.",
                Parameters  = JObject.Parse(@"{
  ""type"": ""object"",
  ""properties"": {
    ""path"": {
      ""type"": ""string"",
      ""description"": ""Workspace-relative path to the file to write.""
    },
    ""content"": {
      ""type"": ""string"",
      ""description"": ""Text content to write to the file.""
    }
  },
  ""required"": [""path"", ""content""],
  ""additionalProperties"": false
}"),
                Handler = async (args) =>
                {
                    string relPath  = args["path"]?.ToString() ?? string.Empty;
                    string content  = args["content"]?.ToString() ?? string.Empty;
                    string fullPath = ResolvePath(relPath);
                    LogTool("write_file", relPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    File.WriteAllText(fullPath, content, System.Text.Encoding.UTF8);
                    return new { success = true, path = relPath };
                }
            };
        }

        // ----------------------------------------------------------------
        // list_files
        // ----------------------------------------------------------------

        private static VideoGenToolDefinition BuildListFilesTool()
        {
            return new VideoGenToolDefinition
            {
                Name        = "list_files",
                Description = "List files in a workspace directory.",
                Parameters  = JObject.Parse(@"{
  ""type"": ""object"",
  ""properties"": {
    ""path"": {
      ""type"": ""string"",
      ""description"": ""Workspace-relative directory path. Defaults to the workspace root.""
    }
  },
  ""required"": [],
  ""additionalProperties"": false
}"),
                Handler = async (args) =>
                {
                    string relPath  = args["path"]?.ToString() ?? string.Empty;
                    string fullPath = string.IsNullOrWhiteSpace(relPath)
                        ? WorkspaceRoot
                        : ResolvePath(relPath);
                    LogTool("list_files", relPath);

                    if (!Directory.Exists(fullPath))
                        return new { error = "Directory not found: " + relPath, files = new string[0] };

                    var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);
                    var relative = new List<string>();
                    string wsRoot = Path.GetFullPath(WorkspaceRoot);
                    foreach (string f in files)
                    {
                        string rel = f.StartsWith(wsRoot)
                            ? f.Substring(wsRoot.Length).TrimStart(Path.DirectorySeparatorChar, '/')
                            : f;
                        relative.Add(rel.Replace('\\', '/'));
                    }
                    return new { path = relPath, files = relative.ToArray() };
                }
            };
        }

        // ----------------------------------------------------------------
        // create_image
        // ----------------------------------------------------------------

        private static VideoGenToolDefinition BuildCreateImageTool()
        {
            return new VideoGenToolDefinition
            {
                Name        = "create_image",
                Description = "Generate an image using Gemini (or OpenRouter). " +
                              "Optionally supply reference_images (workspace-relative paths) to maintain character consistency. " +
                              "Saves the result to workspace/output/{output_name}.png.",
                Parameters  = JObject.Parse(@"{
  ""type"": ""object"",
  ""properties"": {
    ""prompt"": {
      ""type"": ""string"",
      ""description"": ""Detailed description of the image to generate.""
    },
    ""aspect_ratio"": {
      ""type"": ""string"",
      ""enum"": [""16:9"", ""1:1"", ""9:16"", ""4:3"", ""3:4""],
      ""description"": ""Desired aspect ratio. Defaults to '16:9'.""
    },
    ""image_size"": {
      ""type"": ""string"",
      ""enum"": [""2k"", ""1k"", ""hd""],
      ""description"": ""Image size hint appended to the prompt. Defaults to '2k'.""
    },
    ""reference_images"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""string"" },
      ""description"": ""Optional workspace-relative paths to reference images for consistency.""
    },
    ""output_name"": {
      ""type"": ""string"",
      ""description"": ""Filename without extension for saving in workspace/output/. Defaults to 'image_{timestamp}'.""
    }
  },
  ""required"": [""prompt""],
  ""additionalProperties"": false
}"),
                Handler = async (args) =>
                {
                    string prompt      = args["prompt"]?.ToString() ?? string.Empty;
                    string aspectRatio = args["aspect_ratio"]?.ToString() ?? "16:9";
                    string imageSize   = args["image_size"]?.ToString() ?? "2k";
                    string outputName  = args["output_name"]?.ToString();

                    if (string.IsNullOrWhiteSpace(outputName))
                        outputName = "image_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    // Resolve reference image paths
                    string[] refPaths = null;
                    var refToken = args["reference_images"] as JArray;
                    if (refToken != null && refToken.Count > 0)
                    {
                        var paths = new List<string>();
                        foreach (JToken t in refToken)
                        {
                            string rp = ResolvePath(t.ToString());
                            if (File.Exists(rp)) paths.Add(rp);
                        }
                        if (paths.Count > 0) refPaths = paths.ToArray();
                    }

                    // Build full prompt with size/ratio hints
                    string fullPrompt = prompt +
                        "\n\nAspect ratio: " + aspectRatio +
                        ". Image quality: " + imageSize + " resolution, high detail, professional illustration.";

                    LogTool("create_image", outputName, aspectRatio);

                    byte[] imageBytes = await GeminiImageClient.GenerateImageAsync(fullPrompt, refPaths);

                    Directory.CreateDirectory(OutputDir);
                    string outputPath = Path.Combine(OutputDir, outputName + ".png");
                    File.WriteAllBytes(outputPath, imageBytes);

                    string wsRelPath = "output/" + outputName + ".png";
                    ColorLine("[tool] Image saved to workspace/" + wsRelPath + " (" + imageBytes.Length + " bytes)", ConsoleColor.DarkGray);

                    return new { success = true, path = wsRelPath, size_bytes = imageBytes.Length };
                }
            };
        }

        // ----------------------------------------------------------------
        // image_to_video
        // ----------------------------------------------------------------

        private static VideoGenToolDefinition BuildImageToVideoTool()
        {
            return new VideoGenToolDefinition
            {
                Name        = "image_to_video",
                Description = "Animate between two frames using the Replicate Kling model. " +
                              "Provide workspace-relative paths for start_image and optional end_image. " +
                              "The generated video is saved to workspace/output/.",
                Parameters  = JObject.Parse(@"{
  ""type"": ""object"",
  ""properties"": {
    ""start_image"": {
      ""type"": ""string"",
      ""description"": ""Workspace-relative path to the starting frame image.""
    },
    ""prompt"": {
      ""type"": ""string"",
      ""description"": ""Motion description: what happens between the start and end frames.""
    },
    ""end_image"": {
      ""type"": ""string"",
      ""description"": ""Optional workspace-relative path to the ending frame image.""
    },
    ""duration"": {
      ""type"": ""integer"",
      ""description"": ""Video duration in seconds. Defaults to 10.""
    },
    ""aspect_ratio"": {
      ""type"": ""string"",
      ""enum"": [""16:9"", ""1:1"", ""9:16"", ""4:3"", ""3:4""],
      ""description"": ""Video aspect ratio. Defaults to '16:9'.""
    },
    ""output_name"": {
      ""type"": ""string"",
      ""description"": ""Filename without extension for saving in workspace/output/. Defaults to 'video_{timestamp}'.""
    }
  },
  ""required"": [""start_image"", ""prompt""],
  ""additionalProperties"": false
}"),
                Handler = async (args) =>
                {
                    string startImage  = args["start_image"]?.ToString() ?? string.Empty;
                    string prompt      = args["prompt"]?.ToString() ?? string.Empty;
                    string endImage    = args["end_image"]?.ToString();
                    int    duration    = args["duration"]?.Value<int>() ?? 10;
                    string aspectRatio = args["aspect_ratio"]?.ToString() ?? "16:9";
                    string outputName  = args["output_name"]?.ToString();

                    if (string.IsNullOrWhiteSpace(outputName))
                        outputName = "video_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    string startPath = ResolvePath(startImage);
                    string endPath   = !string.IsNullOrWhiteSpace(endImage)
                        ? ResolvePath(endImage)
                        : null;

                    LogTool("image_to_video", outputName, aspectRatio);

                    byte[] videoBytes = await ReplicateClient.GenerateVideoAsync(
                        startPath, prompt, endPath, duration, aspectRatio);

                    Directory.CreateDirectory(OutputDir);
                    string outputPath = Path.Combine(OutputDir, outputName + ".mp4");
                    File.WriteAllBytes(outputPath, videoBytes);

                    string wsRelPath = "output/" + outputName + ".mp4";
                    ColorLine("[tool] Video saved to workspace/" + wsRelPath + " (" + videoBytes.Length + " bytes)", ConsoleColor.DarkGray);

                    return new { success = true, path = wsRelPath, size_bytes = videoBytes.Length };
                }
            };
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static string ResolvePath(string workspaceRelativePath)
        {
            // Prevent directory traversal
            string combined = Path.Combine(WorkspaceRoot, workspaceRelativePath.TrimStart('/', '\\'));
            return Path.GetFullPath(combined);
        }

        private static void LogTool(string tool, string subject, string detail = null)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            if (detail != null)
                Console.WriteLine("[tool] " + tool + " | " + detail + " | " + subject);
            else
                Console.WriteLine("[tool] " + tool + " | " + subject);
            Console.ResetColor();
        }

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
