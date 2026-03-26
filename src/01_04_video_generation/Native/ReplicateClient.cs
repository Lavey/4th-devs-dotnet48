using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.VideoGeneration.Native
{
    /// <summary>
    /// Client for Replicate's Kling video generation model.
    /// Polls for prediction completion and downloads the resulting MP4.
    /// </summary>
    internal static class ReplicateClient
    {
        private const string PredictionsUrl = "https://api.replicate.com/v1/predictions";
        private const string KlingModel     = "kwaivgi/kling-v2.1-pro";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        /// <summary>
        /// Sends start/end frames to Kling and returns the generated video bytes (MP4).
        /// </summary>
        public static async Task<byte[]> GenerateVideoAsync(
            string startImagePath,
            string prompt,
            string endImagePath  = null,
            int    duration      = 10,
            string aspectRatio   = "16:9")
        {
            string token = ConfigurationManager.AppSettings["REPLICATE_API_TOKEN"]?.Trim();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException(
                    "REPLICATE_API_TOKEN is not configured. Copy App.config.example to App.config and fill in your token.");

            // Build input
            var input = new JObject
            {
                ["prompt"]       = prompt,
                ["duration"]     = duration,
                ["aspect_ratio"] = aspectRatio
            };

            if (File.Exists(startImagePath))
                input["start_image"] = ToDataUri(startImagePath);

            if (!string.IsNullOrWhiteSpace(endImagePath) && File.Exists(endImagePath))
                input["end_image"] = ToDataUri(endImagePath);

            var body = new JObject
            {
                ["version"] = KlingModel,
                ["input"]   = input
            };

            // Create prediction
            string predictionId = await CreatePredictionAsync(body.ToString(Formatting.None), token);

            // Poll for completion
            string outputUrl = await PollPredictionAsync(predictionId, token);

            // Download the video
            return await Http.GetByteArrayAsync(outputUrl);
        }

        // ----------------------------------------------------------------
        // Create prediction
        // ----------------------------------------------------------------

        private static async Task<string> CreatePredictionAsync(string jsonBody, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, PredictionsUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using (var response = await Http.SendAsync(request))
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        "Replicate prediction creation failed " + (int)response.StatusCode + ": " +
                        Truncate(responseBody, 500));

                JObject parsed = JObject.Parse(responseBody);
                string id = parsed["id"]?.ToString();
                if (string.IsNullOrEmpty(id))
                    throw new InvalidOperationException("Replicate did not return a prediction ID.");

                return id;
            }
        }

        // ----------------------------------------------------------------
        // Poll until succeeded / failed
        // ----------------------------------------------------------------

        private static async Task<string> PollPredictionAsync(string predictionId, string token)
        {
            string pollUrl = PredictionsUrl + "/" + predictionId;
            int maxAttempts = 120; // ~10 minutes at 5s intervals

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));

                var request = new HttpRequestMessage(HttpMethod.Get, pollUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using (var response = await Http.SendAsync(request))
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException(
                            "Replicate poll failed " + (int)response.StatusCode + ": " +
                            Truncate(responseBody, 500));

                    JObject parsed = JObject.Parse(responseBody);
                    string status  = parsed["status"]?.ToString();

                    ColorLine(
                        "[replicate] prediction " + predictionId + " status=" + status +
                        " (attempt " + (attempt + 1) + "/" + maxAttempts + ")",
                        ConsoleColor.DarkGray);

                    if (status == "succeeded")
                    {
                        // output can be a string or array
                        JToken output = parsed["output"];
                        string outputUrl = null;
                        if (output?.Type == JTokenType.String)
                            outputUrl = output.ToString();
                        else if (output?.Type == JTokenType.Array)
                            outputUrl = ((JArray)output)[0]?.ToString();

                        if (string.IsNullOrEmpty(outputUrl))
                            throw new InvalidOperationException("Replicate succeeded but returned no output URL.");

                        return outputUrl;
                    }

                    if (status == "failed" || status == "canceled")
                    {
                        string errDetail = parsed["error"]?.ToString() ?? "No details";
                        throw new InvalidOperationException(
                            "Replicate prediction " + status + ": " + errDetail);
                    }
                }
            }

            throw new InvalidOperationException(
                "Replicate prediction timed out after " + maxAttempts + " attempts.");
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static string ToDataUri(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            string mimeType = GetImageMimeType(filePath);
            return "data:" + mimeType + ";base64," + Convert.ToBase64String(bytes);
        }

        private static string GetImageMimeType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".webp": return "image/webp";
                case ".gif":  return "image/gif";
                default:      return "image/png";
            }
        }

        private static string Truncate(string s, int max)
            => s != null && s.Length > max ? s.Substring(0, max) + "..." : s ?? string.Empty;

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
