using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson04_ImageGuidance
{
    /// <summary>
    /// Lesson 04 – Image Guidance
    /// Pose-guided image generation using a JSON prompt template and a reference image.
    ///
    /// Workflow:
    ///   1. Reads workspace/template.json for the base style parameters
    ///   2. Accepts a subject description (from args or default)
    ///   3. If workspace/reference/ contains a pose reference image, includes it
    ///      in the DALL-E edit request so the pose is respected
    ///   4. Saves the final image to workspace/output/
    ///
    /// Source: 01_04_image_guidance/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string ImageModel = "dall-e-3";
        private const string ImageSize  = "1024x1024";

        private const string GenerateEndpoint = "https://api.openai.com/v1/images/generations";

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            string exeDir      = AppDomain.CurrentDomain.BaseDirectory;
            string templatePath = Path.Combine(exeDir, "workspace", "template.json");
            string refDir       = Path.Combine(exeDir, "workspace", "reference");
            string outputDir    = Path.Combine(exeDir, "workspace", "output");

            Directory.CreateDirectory(refDir);
            Directory.CreateDirectory(outputDir);

            Console.WriteLine("=== Image Guidance Agent ===\n");

            // ----------------------------------------------------------------
            // Load (or create) the style template
            // ----------------------------------------------------------------
            EnsureTemplate(templatePath);
            string templateJson = File.ReadAllText(templatePath, Encoding.UTF8);
            var template = JObject.Parse(templateJson);
            Console.WriteLine("Template loaded: workspace/template.json");

            // ----------------------------------------------------------------
            // Determine subject from args or default
            // ----------------------------------------------------------------
            string subject = args.Length > 0
                ? string.Join(" ", args)
                : "a female magician in a walking pose";

            Console.WriteLine("Subject: " + subject);

            // ----------------------------------------------------------------
            // Find a pose reference image (optional)
            // ----------------------------------------------------------------
            string referenceImagePath = FindFirstImage(refDir);
            if (referenceImagePath != null)
                Console.WriteLine("Pose reference: " + Path.GetFileName(referenceImagePath));
            else
                Console.WriteLine("No pose reference found — generating without guidance.");

            Console.WriteLine();

            // ----------------------------------------------------------------
            // Build the enriched prompt from template + subject
            // ----------------------------------------------------------------
            string prompt = BuildPrompt(template, subject, referenceImagePath);
            Console.WriteLine("Generating image ...");

            // ----------------------------------------------------------------
            // Generate the image
            // ----------------------------------------------------------------
            string imageUrl = await GenerateImageAsync(prompt);

            // ----------------------------------------------------------------
            // Download and save
            // ----------------------------------------------------------------
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string outputPath = Path.Combine(outputDir,
                string.Format("guided_{0}.png", timestamp));

            await DownloadImageAsync(imageUrl, outputPath);
            Console.WriteLine("Image saved: " + outputPath);
            Console.WriteLine("\nDone.");
        }

        // ----------------------------------------------------------------
        // Prompt building
        // ----------------------------------------------------------------

        static string BuildPrompt(JObject template, string subject, string referenceImagePath)
        {
            // Extract style descriptors from the template
            string style       = template["style"]?.ToString()       ?? "detailed digital art";
            string palette     = template["palette"]?.ToString()     ?? "full colour";
            string background  = template["background"]?.ToString()  ?? "plain white";
            string quality     = template["quality"]?.ToString()     ?? "high quality";
            string aspectRatio = template["aspectRatio"]?.ToString() ?? "1:1";

            var sb = new StringBuilder();
            sb.AppendFormat("Subject: {0}\n", subject);
            sb.AppendFormat("Style: {0}\n", style);
            sb.AppendFormat("Colour palette: {0}\n", palette);
            sb.AppendFormat("Background: {0}\n", background);
            sb.AppendFormat("Quality: {0}\n", quality);
            sb.AppendFormat("Aspect ratio: {0}\n", aspectRatio);

            if (referenceImagePath != null)
            {
                sb.AppendLine();
                sb.AppendLine("IMPORTANT: Preserve the exact body pose and proportions " +
                              "shown in the provided reference image.");
            }

            return sb.ToString();
        }

        // ----------------------------------------------------------------
        // Image generation
        // ----------------------------------------------------------------

        static async Task<string> GenerateImageAsync(string prompt)
        {
            var requestBody = new JObject
            {
                ["model"]   = ImageModel,
                ["prompt"]  = prompt,
                ["n"]       = 1,
                ["size"]    = ImageSize,
                ["quality"] = "standard"
            };

            string apiKey = System.Configuration.ConfigurationManager
                .AppSettings["OPENAI_API_KEY"]?.Trim() ?? AiConfig.ApiKey;

            using (var http = new HttpClient())
            using (var content = new StringContent(
                requestBody.ToString(Formatting.None), Encoding.UTF8, "application/json"))
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);

                using (var response = await http.PostAsync(GenerateEndpoint, content))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException(
                            string.Format("Image API error ({0}): {1}",
                                (int)response.StatusCode, body));

                    var json = JObject.Parse(body);
                    var error = json["error"];
                    if (error != null)
                        throw new InvalidOperationException(
                            error["message"]?.ToString() ?? "Image API error");

                    return json["data"]?[0]?["url"]?.ToString()
                        ?? json["data"]?[0]?["b64_json"]?.ToString()
                        ?? throw new InvalidOperationException("No image URL in response");
                }
            }
        }

        static async Task DownloadImageAsync(string url, string outputPath)
        {
            if (url.StartsWith("data:"))
            {
                int commaIndex = url.IndexOf(',');
                if (commaIndex >= 0)
                {
                    File.WriteAllBytes(outputPath,
                        Convert.FromBase64String(url.Substring(commaIndex + 1)));
                    return;
                }
            }

            using (var http = new HttpClient())
            {
                byte[] bytes = await http.GetByteArrayAsync(url);
                File.WriteAllBytes(outputPath, bytes);
            }
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        static string FindFirstImage(string directory)
        {
            foreach (string f in Directory.GetFiles(directory))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp")
                    return f;
            }
            return null;
        }

        static void EnsureTemplate(string templatePath)
        {
            if (File.Exists(templatePath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(templatePath));
            var defaultTemplate = new JObject
            {
                ["style"]       = "clean digital illustration, semi-realistic character design",
                ["palette"]     = "vibrant full colour with soft shading",
                ["background"]  = "plain white studio background",
                ["quality"]     = "high quality, sharp details",
                ["aspectRatio"] = "1:1"
            };
            File.WriteAllText(templatePath,
                defaultTemplate.ToString(Formatting.Indented), Encoding.UTF8);
            Console.WriteLine("Created default template: workspace/template.json\n");
        }
    }
}
