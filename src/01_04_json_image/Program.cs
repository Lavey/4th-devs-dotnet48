using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson04_JsonImage
{
    /// <summary>
    /// Lesson 04 – JSON Image
    /// Token-efficient image generation from JSON prompt templates.
    ///
    /// The JSON template defines all reusable style parameters once.
    /// For each generation, only the "subject" field changes. This makes
    /// prompts reproducible, version-controllable, and token-efficient.
    ///
    /// Workflow:
    ///   1. Reads (or creates) workspace/template.json
    ///   2. Copies it into workspace/prompts/<subject_slug>.json
    ///   3. Edits only the subject field in the copy
    ///   4. Builds the full prompt from the JSON and generates the image
    ///   5. Saves to workspace/output/
    ///
    /// Source: 01_04_json_image/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string ImageModel       = "dall-e-3";
        private const string ImageSize        = "1024x1024";
        private const string GenerateEndpoint = "https://api.openai.com/v1/images/generations";

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            string exeDir      = AppDomain.CurrentDomain.BaseDirectory;
            string templatePath = Path.Combine(exeDir, "workspace", "template.json");
            string promptsDir  = Path.Combine(exeDir, "workspace", "prompts");
            string outputDir   = Path.Combine(exeDir, "workspace", "output");

            Directory.CreateDirectory(promptsDir);
            Directory.CreateDirectory(outputDir);

            Console.WriteLine("=== JSON Image Agent ===");
            Console.WriteLine("Token-efficient image generation from JSON templates\n");

            // ----------------------------------------------------------------
            // Ensure template exists
            // ----------------------------------------------------------------
            EnsureTemplate(templatePath);
            Console.WriteLine("Template: workspace/template.json");

            // ----------------------------------------------------------------
            // Choose a subject (from args or provide a few defaults to cycle through)
            // ----------------------------------------------------------------
            string[] subjects = args.Length > 0
                ? new[] { string.Join(" ", args) }
                : new[]
                {
                    "a friendly robot explorer",
                    "a wise old wizard with a glowing staff",
                    "an astronaut cat floating in space"
                };

            foreach (string subject in subjects)
            {
                Console.WriteLine("\nSubject: " + subject);
                await GenerateForSubject(subject, templatePath, promptsDir, outputDir);
            }

            Console.WriteLine("\nDone.");
        }

        static async Task GenerateForSubject(
            string subject,
            string templatePath,
            string promptsDir,
            string outputDir)
        {
            // ----------------------------------------------------------------
            // Copy template → prompts/<slug>.json and update subject
            // ----------------------------------------------------------------
            string slug       = Slugify(subject);
            string promptPath = Path.Combine(promptsDir, slug + ".json");

            string templateJson = File.ReadAllText(templatePath, Encoding.UTF8);
            var promptObj = JObject.Parse(templateJson);
            promptObj["subject"] = subject;

            File.WriteAllText(promptPath, promptObj.ToString(Formatting.Indented), Encoding.UTF8);
            Console.WriteLine("Prompt file: workspace/prompts/" + slug + ".json");

            // ----------------------------------------------------------------
            // Build the DALL-E prompt from the JSON fields
            // ----------------------------------------------------------------
            string prompt = BuildPromptFromJson(promptObj);
            Console.Write("Generating image ...");

            try
            {
                string imageUrl = await GenerateImageAsync(prompt);

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string outputPath = Path.Combine(outputDir,
                    string.Format("{0}_{1}.png", slug, timestamp));

                await DownloadImageAsync(imageUrl, outputPath);
                Console.WriteLine("\nSaved: workspace/output/" + Path.GetFileName(outputPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.Error.WriteLine("  Error: " + ex.Message);
            }
        }

        // ----------------------------------------------------------------
        // Prompt assembly
        // ----------------------------------------------------------------

        static string BuildPromptFromJson(JObject obj)
        {
            var sb = new StringBuilder();

            // Subject is always first
            string subject = obj["subject"]?.ToString();
            if (!string.IsNullOrWhiteSpace(subject))
                sb.AppendFormat("Subject: {0}\n", subject);

            // Append remaining fields in order
            string[] fieldOrder = { "style", "palette", "background", "lighting", "mood", "quality" };
            foreach (string field in fieldOrder)
            {
                string val = obj[field]?.ToString();
                if (!string.IsNullOrWhiteSpace(val))
                    sb.AppendFormat("{0}: {1}\n",
                        char.ToUpper(field[0]) + field.Substring(1), val);
            }

            // Any extra fields
            foreach (var prop in obj.Properties())
            {
                if (prop.Name == "subject") continue;
                bool handled = false;
                foreach (string f in fieldOrder)
                    if (f == prop.Name) { handled = true; break; }
                if (!handled && prop.Value.Type == JTokenType.String)
                    sb.AppendFormat("{0}: {1}\n",
                        char.ToUpper(prop.Name[0]) + prop.Name.Substring(1),
                        prop.Value.ToString());
            }

            return sb.ToString().Trim();
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
                            string.Format("DALL-E error ({0}): {1}",
                                (int)response.StatusCode, body));

                    var json = JObject.Parse(body);
                    var error = json["error"];
                    if (error != null)
                        throw new InvalidOperationException(
                            error["message"]?.ToString() ?? "Image API error");

                    return json["data"]?[0]?["url"]?.ToString()
                        ?? throw new InvalidOperationException(
                            "No image URL in response: " + body);
                }
            }
        }

        static async Task DownloadImageAsync(string url, string outputPath)
        {
            using (var http = new HttpClient())
            {
                byte[] bytes = await http.GetByteArrayAsync(url);
                File.WriteAllBytes(outputPath, bytes);
            }
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        static string Slugify(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else if (c == ' ' || c == '-')
                    sb.Append('_');
            }
            // Trim trailing underscores and limit length
            string slug = sb.ToString().Trim('_');
            return slug.Length > 60 ? slug.Substring(0, 60) : slug;
        }

        static void EnsureTemplate(string templatePath)
        {
            if (File.Exists(templatePath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(templatePath));
            var defaultTemplate = new JObject
            {
                ["subject"]    = "(set per generation)",
                ["style"]      = "clean digital illustration, semi-realistic",
                ["palette"]    = "vibrant colours with soft shading",
                ["background"] = "plain white studio background",
                ["lighting"]   = "soft, even studio lighting",
                ["mood"]       = "friendly, professional",
                ["quality"]    = "high quality, sharp details, 4K"
            };
            File.WriteAllText(templatePath,
                defaultTemplate.ToString(Formatting.Indented), Encoding.UTF8);
            Console.WriteLine("Created default template: workspace/template.json\n");
        }
    }
}
