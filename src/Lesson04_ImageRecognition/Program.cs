using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson04_ImageRecognition
{
    /// <summary>
    /// Lesson 04 – Image Recognition
    /// Vision-based image classification agent.
    ///
    /// Workflow:
    ///   1. Reads character profiles from knowledge/*.md
    ///   2. Analyses each image in images/ using gpt-4o vision
    ///   3. Matches visible traits against the profiles
    ///   4. Copies each image to images/organized/<category>/
    ///
    /// Source: 01_04_image_recognition/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        // gpt-4o has vision; gpt-4.1 also supports vision via the Responses API
        private const string Model = "gpt-4o";

        private static readonly HashSet<string> ImageExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            string exeDir       = AppDomain.CurrentDomain.BaseDirectory;
            string imagesDir    = Path.Combine(exeDir, "images");
            string knowledgeDir = Path.Combine(exeDir, "knowledge");
            string organizedDir = Path.Combine(imagesDir, "organized");

            Directory.CreateDirectory(imagesDir);
            Directory.CreateDirectory(knowledgeDir);
            Directory.CreateDirectory(organizedDir);

            Console.WriteLine("=== Image Recognition Agent ===");
            Console.WriteLine("Classify images by character\n");

            // ----------------------------------------------------------------
            // Step 1 – Load character profiles
            // ----------------------------------------------------------------
            string[] knowledgeFiles = Directory.GetFiles(knowledgeDir, "*.md");

            if (knowledgeFiles.Length == 0)
            {
                EnsureSampleKnowledge(knowledgeDir);
                knowledgeFiles = Directory.GetFiles(knowledgeDir, "*.md");
                Console.WriteLine("Created sample knowledge files.\n");
            }

            var profiles = new List<KnowledgeProfile>();
            foreach (string kf in knowledgeFiles)
            {
                string name    = Path.GetFileNameWithoutExtension(kf);
                string content = File.ReadAllText(kf, Encoding.UTF8);
                profiles.Add(new KnowledgeProfile { Category = name, Description = content });
                Console.WriteLine("Loaded profile: " + name);
            }
            Console.WriteLine();

            // ----------------------------------------------------------------
            // Step 2 – Find images (skip the organized/ subdirectory)
            // ----------------------------------------------------------------
            string[] allImages = Directory.GetFiles(imagesDir);
            var imagesToProcess = new List<string>();
            foreach (string img in allImages)
            {
                if (ImageExtensions.Contains(Path.GetExtension(img)))
                    imagesToProcess.Add(img);
            }

            if (imagesToProcess.Count == 0)
            {
                Console.WriteLine("No images found in images/");
                Console.WriteLine("Add .jpg/.png/.webp files to classify them.");
                return;
            }

            Console.WriteLine(string.Format("Found {0} image(s) to classify.\n", imagesToProcess.Count));

            // ----------------------------------------------------------------
            // Step 3 – Classify each image
            // ----------------------------------------------------------------
            int classified = 0;
            int errors     = 0;

            foreach (string imagePath in imagesToProcess)
            {
                string fileName = Path.GetFileName(imagePath);
                Console.WriteLine("Classifying: " + fileName + " ...");

                try
                {
                    string category = await ClassifyImageAsync(imagePath, profiles);
                    Console.WriteLine("  → Category: " + category);

                    // Copy to organized/<category>/
                    string destDir  = Path.Combine(organizedDir, category);
                    Directory.CreateDirectory(destDir);
                    string destPath = Path.Combine(destDir, fileName);
                    File.Copy(imagePath, destPath, overwrite: true);
                    Console.WriteLine("  → Copied to: images/organized/" + category + "/" + fileName);
                    classified++;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("  Error: " + ex.Message);
                    errors++;
                }
                Console.WriteLine();
            }

            Console.WriteLine(string.Format(
                "Done. Classified: {0}  Errors: {1}", classified, errors));
        }

        // ----------------------------------------------------------------
        // Vision classification
        // ----------------------------------------------------------------

        static async Task<string> ClassifyImageAsync(
            string imagePath,
            List<KnowledgeProfile> profiles)
        {
            // Build the system prompt from knowledge profiles
            var sb = new StringBuilder();
            sb.AppendLine("You are an image classification assistant.");
            sb.AppendLine("Classify the provided image into ONE of the following categories.");
            sb.AppendLine("Respond with ONLY the category name — nothing else.\n");
            sb.AppendLine("Categories:");
            foreach (var p in profiles)
            {
                sb.AppendLine(string.Format("\n## {0}", p.Category));
                sb.AppendLine(p.Description);
            }
            sb.AppendLine("\nIf the image does not match any category, respond with: unknown");

            // Encode the image as base64
            byte[] imageBytes  = File.ReadAllBytes(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);
            string mimeType    = GetImageMime(imagePath);
            string dataUri     = string.Format("data:{0};base64,{1}", mimeType, base64Image);

            // Build the Responses API request with image content
            var requestBody = new JObject
            {
                ["model"] = AiConfig.ResolveModel(Model),
                ["input"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "message",
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "input_text",
                                ["text"] = sb.ToString()
                            },
                            new JObject
                            {
                                ["type"]      = "input_image",
                                ["image_url"] = dataUri
                            }
                        }
                    }
                }
            };

            string responseJson = await PostRawAsync(requestBody.ToString(Formatting.None));
            var parsed  = JObject.Parse(responseJson);

            // Check for API error
            var error = parsed["error"];
            if (error != null)
                throw new InvalidOperationException(error["message"]?.ToString() ?? "API error");

            // Extract text from the response
            string category =
                parsed["output_text"]?.ToString()
                ?? parsed["output"]?[0]?["content"]?[0]?["text"]?.ToString()
                ?? "unknown";

            return category.Trim().ToLowerInvariant().Replace(" ", "_");
        }

        // ----------------------------------------------------------------
        // Sample knowledge files
        // ----------------------------------------------------------------

        static void EnsureSampleKnowledge(string knowledgeDir)
        {
            File.WriteAllText(Path.Combine(knowledgeDir, "nature.md"),
                "# Nature\nImages of natural landscapes, animals, plants, forests, mountains, rivers, or skies.",
                Encoding.UTF8);

            File.WriteAllText(Path.Combine(knowledgeDir, "people.md"),
                "# People\nImages containing one or more human faces or full-body portraits.",
                Encoding.UTF8);

            File.WriteAllText(Path.Combine(knowledgeDir, "architecture.md"),
                "# Architecture\nImages of buildings, bridges, interiors, or constructed structures.",
                Encoding.UTF8);

            File.WriteAllText(Path.Combine(knowledgeDir, "technology.md"),
                "# Technology\nImages of computers, phones, electronic devices, or circuit boards.",
                Encoding.UTF8);
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        static async Task<string> PostRawAsync(string jsonBody)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", AiConfig.AppName);
                }

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        static string GetImageMime(string filePath)
        {
            switch (Path.GetExtension(filePath).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png":  return "image/png";
                case ".gif":  return "image/gif";
                case ".webp": return "image/webp";
                default:      return "image/jpeg";
            }
        }

        // ----------------------------------------------------------------
        // Data
        // ----------------------------------------------------------------

        private sealed class KnowledgeProfile
        {
            public string Category    { get; set; }
            public string Description { get; set; }
        }
    }
}
