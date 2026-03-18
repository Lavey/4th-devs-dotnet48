using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson04_ImageEditing
{
    /// <summary>
    /// Lesson 04 – Image Editing
    /// Image generation and editing agent.
    ///
    /// Workflow:
    ///   1. Reads workspace/style-guide.md for style constraints
    ///   2. Generates a new image using DALL-E 3 (gpt-image-1 via OpenAI)
    ///   3. If workspace/input/ contains an image, edits it instead
    ///   4. Analyses the result with gpt-4o vision and reports any issues
    ///   5. Saves the final image to workspace/output/
    ///
    /// Source: 01_04_image_editing/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string VisionModel  = "gpt-4o";
        private const string ImageModel   = "dall-e-3";
        private const string ImageSize    = "1024x1024";

        private const string GenerateEndpoint = "https://api.openai.com/v1/images/generations";
        private const string EditEndpoint     = "https://api.openai.com/v1/images/edits";

        private static readonly HashSet<string> ImageExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".webp" };

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            string exeDir    = AppDomain.CurrentDomain.BaseDirectory;
            string inputDir  = Path.Combine(exeDir, "workspace", "input");
            string outputDir = Path.Combine(exeDir, "workspace", "output");
            string stylePath = Path.Combine(exeDir, "workspace", "style-guide.md");

            Directory.CreateDirectory(inputDir);
            Directory.CreateDirectory(outputDir);

            Console.WriteLine("=== Image Editing Agent ===\n");

            // Read style guide if available
            string styleGuide = File.Exists(stylePath)
                ? File.ReadAllText(stylePath, Encoding.UTF8)
                : DefaultStyleGuide;

            Console.WriteLine("Style guide loaded.");

            // Determine the user's request (from args or default)
            string userRequest = args.Length > 0
                ? string.Join(" ", args)
                : "Create a monochrome concept sketch of a futuristic motorcycle";

            Console.WriteLine("Request: " + userRequest + "\n");

            // ----------------------------------------------------------------
            // Look for a source image to edit, otherwise generate from scratch
            // ----------------------------------------------------------------
            string sourceImagePath = FindFirstImage(inputDir);
            string generatedImageUrl;

            if (sourceImagePath != null)
            {
                Console.WriteLine("Source image: " + Path.GetFileName(sourceImagePath));
                Console.WriteLine("Editing image ...");
                generatedImageUrl = await EditImageAsync(sourceImagePath, userRequest, styleGuide);
            }
            else
            {
                Console.WriteLine("No source image found — generating from scratch ...");
                generatedImageUrl = await GenerateImageAsync(userRequest, styleGuide);
            }

            // ----------------------------------------------------------------
            // Download the generated image
            // ----------------------------------------------------------------
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string outputPath = Path.Combine(outputDir, string.Format("generated_{0}.png", timestamp));

            await DownloadImageAsync(generatedImageUrl, outputPath);
            Console.WriteLine("\nImage saved: " + outputPath);

            // ----------------------------------------------------------------
            // Review the result using vision
            // ----------------------------------------------------------------
            Console.WriteLine("\nReviewing result with vision ...");
            string review = await ReviewImageAsync(outputPath, userRequest, styleGuide);
            Console.WriteLine("\nReview:\n" + review);

            Console.WriteLine("\nDone.");
        }

        // ----------------------------------------------------------------
        // Image generation (DALL-E 3)
        // ----------------------------------------------------------------

        static async Task<string> GenerateImageAsync(string prompt, string styleGuide)
        {
            string enrichedPrompt = string.Format(
                "{0}\n\nStyle requirements:\n{1}", prompt, styleGuide);

            var requestBody = new JObject
            {
                ["model"]   = ImageModel,
                ["prompt"]  = enrichedPrompt,
                ["n"]       = 1,
                ["size"]    = ImageSize,
                ["quality"] = "standard"
            };

            return await PostImageRequestAsync(
                GenerateEndpoint, requestBody.ToString(Formatting.None));
        }

        // ----------------------------------------------------------------
        // Image editing (DALL-E 2 edit endpoint — accepts PNG + mask)
        // ----------------------------------------------------------------

        static async Task<string> EditImageAsync(
            string sourceImagePath, string prompt, string styleGuide)
        {
            string enrichedPrompt = string.Format(
                "{0}\n\nStyle requirements:\n{1}", prompt, styleGuide);

            // The edit endpoint requires a square PNG with alpha channel
            byte[] imageBytes = File.ReadAllBytes(sourceImagePath);

            using (var http = BuildHttpClient())
            using (var form = new MultipartFormDataContent())
            {
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                form.Add(imageContent, "image", Path.GetFileName(sourceImagePath));
                form.Add(new StringContent("dall-e-2"),    "model");
                form.Add(new StringContent(enrichedPrompt), "prompt");
                form.Add(new StringContent("1"),            "n");
                form.Add(new StringContent("1024x1024"),    "size");

                using (var response = await http.PostAsync(EditEndpoint, form))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        // Fall back to generate if edit fails (e.g. non-square input)
                        Console.WriteLine("  Edit failed — falling back to generation.");
                        return await GenerateImageAsync(prompt, styleGuide);
                    }
                    return ExtractImageUrl(body);
                }
            }
        }

        // ----------------------------------------------------------------
        // Vision review
        // ----------------------------------------------------------------

        static async Task<string> ReviewImageAsync(
            string imagePath, string originalRequest, string styleGuide)
        {
            byte[] imageBytes  = File.ReadAllBytes(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);
            string dataUri     = "data:image/png;base64," + base64Image;

            string systemPrompt =
                "You are a quality assurance reviewer for AI-generated images. " +
                "Analyse the provided image and report:\n" +
                "1. Whether it matches the original request\n" +
                "2. Any quality issues or artifacts\n" +
                "3. Style consistency with the style guide\n" +
                "Be concise and actionable.";

            string userMessage = string.Format(
                "Original request: {0}\n\nStyle guide:\n{1}\n\nPlease review the image.",
                originalRequest, styleGuide);

            var requestBody = new JObject
            {
                ["model"] = AiConfig.ResolveModel(VisionModel),
                ["input"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "message",
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject { ["type"] = "input_text",  ["text"]      = systemPrompt + "\n\n" + userMessage },
                            new JObject { ["type"] = "input_image", ["image_url"] = dataUri }
                        }
                    }
                }
            };

            string responseJson = await PostRawAsync(
                AiConfig.ApiEndpoint, requestBody.ToString(Formatting.None));
            var parsed = JObject.Parse(responseJson);

            var apiError = parsed["error"];
            if (apiError != null)
                return "Vision review error: " + (apiError["message"]?.ToString() ?? "unknown");

            return parsed["output_text"]?.ToString()
                ?? parsed["output"]?[0]?["content"]?[0]?["text"]?.ToString()
                ?? "(no review generated)";
        }

        // ----------------------------------------------------------------
        // HTTP helpers
        // ----------------------------------------------------------------

        static async Task<string> PostImageRequestAsync(string endpoint, string jsonBody)
        {
            using (var http = BuildHttpClient())
            using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
            using (var response = await http.PostAsync(endpoint, content))
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        string.Format("Image API error ({0}): {1}", (int)response.StatusCode, body));
                return ExtractImageUrl(body);
            }
        }

        static async Task DownloadImageAsync(string url, string outputPath)
        {
            if (url.StartsWith("data:"))
            {
                // Base64 inline (some APIs return b64_json)
                int commaIndex = url.IndexOf(',');
                if (commaIndex >= 0)
                {
                    byte[] bytes = Convert.FromBase64String(url.Substring(commaIndex + 1));
                    File.WriteAllBytes(outputPath, bytes);
                    return;
                }
            }

            using (var http = new HttpClient())
            using (var response = await http.GetAsync(url))
            {
                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(outputPath, bytes);
            }
        }

        static async Task<string> PostRawAsync(string endpoint, string jsonBody)
        {
            using (var http = BuildHttpClient())
            using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
            using (var response = await http.PostAsync(endpoint, content))
            {
                return await response.Content.ReadAsStringAsync();
            }
        }

        static HttpClient BuildHttpClient()
        {
            // Image generation endpoints are OpenAI-only
            string apiKey = System.Configuration.ConfigurationManager
                .AppSettings["OPENAI_API_KEY"]?.Trim() ?? AiConfig.ApiKey;

            var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            return http;
        }

        static string ExtractImageUrl(string responseBody)
        {
            var json = JObject.Parse(responseBody);
            var error = json["error"];
            if (error != null)
                throw new InvalidOperationException(
                    error["message"]?.ToString() ?? "Image API error");

            return json["data"]?[0]?["url"]?.ToString()
                ?? json["data"]?[0]?["b64_json"]?.ToString()
                ?? throw new InvalidOperationException("No image URL in response: " + responseBody);
        }

        static string FindFirstImage(string directory)
        {
            foreach (string f in Directory.GetFiles(directory))
            {
                if (ImageExtensions.Contains(Path.GetExtension(f)))
                    return f;
            }
            return null;
        }

        // ----------------------------------------------------------------
        // Defaults
        // ----------------------------------------------------------------

        private const string DefaultStyleGuide =
            "Style: clean, minimalist, high contrast\n" +
            "Palette: monochrome with optional accent colour\n" +
            "Format: 1024x1024 square\n" +
            "Mood: professional, futuristic";
    }
}
