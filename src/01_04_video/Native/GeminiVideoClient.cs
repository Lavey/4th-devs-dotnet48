using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Video.Native
{
    /// <summary>
    /// Client for the Gemini video API (generateContent and file upload).
    /// Supports YouTube URLs directly, local files via inline base64 (&lt;20 MB)
    /// or via the resumable upload API (&gt;= 20 MB).
    /// </summary>
    internal static class GeminiVideoClient
    {
        private const string GenerateUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        private const string UploadInitUrl =
            "https://generativelanguage.googleapis.com/upload/v1beta/files";

        private const long InlineThresholdBytes = 20 * 1024 * 1024; // 20 MB

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        // ----------------------------------------------------------------
        // Public entry point: call Gemini with a video + prompt
        // ----------------------------------------------------------------

        /// <summary>
        /// Sends a video (YouTube URL or local path) and a text prompt to Gemini,
        /// optionally requesting structured JSON output.
        /// </summary>
        public static async Task<string> ProcessVideoAsync(
            string videoSource,
            string prompt,
            string startTime    = null,
            string endTime      = null,
            double? fps         = null,
            bool    wantJson    = false,
            JObject jsonSchema  = null)
        {
            string apiKey = AiConfig.GeminiApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "GEMINI_API_KEY is not configured. Copy App.config.example to App.config and fill in your key.");

            JObject videoPart = await BuildVideoPartAsync(videoSource, apiKey);

            // Attach video metadata for clipping / fps
            if (!string.IsNullOrEmpty(startTime) || !string.IsNullOrEmpty(endTime) || fps.HasValue)
            {
                var meta = new JObject();
                if (!string.IsNullOrEmpty(startTime)) meta["start_offset"] = startTime;
                if (!string.IsNullOrEmpty(endTime))   meta["end_offset"]   = endTime;
                if (fps.HasValue)                      meta["fps"]          = fps.Value;
                videoPart["video_metadata"] = meta;
            }

            var parts = new JArray
            {
                videoPart,
                new JObject { ["text"] = prompt }
            };

            var requestBody = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["parts"] = parts
                    }
                }
            };

            if (wantJson)
            {
                var genConfig = new JObject
                {
                    ["response_mime_type"] = "application/json"
                };
                if (jsonSchema != null)
                    genConfig["response_schema"] = jsonSchema;
                requestBody["generation_config"] = genConfig;
            }

            return await PostToGeminiAsync(requestBody, apiKey);
        }

        // ----------------------------------------------------------------
        // Build the video part (YouTube URL vs local file)
        // ----------------------------------------------------------------

        private static async Task<JObject> BuildVideoPartAsync(string videoSource, string apiKey)
        {
            if (IsYouTubeUrl(videoSource))
            {
                return new JObject
                {
                    ["file_data"] = new JObject
                    {
                        ["file_uri"] = videoSource
                    }
                };
            }

            // Local file
            if (!File.Exists(videoSource))
                throw new FileNotFoundException("Video file not found: " + videoSource, videoSource);

            long fileSize = new FileInfo(videoSource).Length;
            string mimeType = GetVideoMimeType(videoSource);

            if (fileSize < InlineThresholdBytes)
            {
                byte[] bytes = File.ReadAllBytes(videoSource);
                return new JObject
                {
                    ["inline_data"] = new JObject
                    {
                        ["mime_type"] = mimeType,
                        ["data"]      = Convert.ToBase64String(bytes)
                    }
                };
            }

            // Large file – upload via resumable upload
            string fileUri = await UploadFileAsync(videoSource, mimeType, apiKey);
            return new JObject
            {
                ["file_data"] = new JObject
                {
                    ["file_uri"] = fileUri
                }
            };
        }

        // ----------------------------------------------------------------
        // Gemini File Upload API (for files >= 20 MB)
        // ----------------------------------------------------------------

        private static async Task<string> UploadFileAsync(string filePath, string mimeType, string apiKey)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            string displayName = Path.GetFileName(filePath);

            // Step 1: initiate resumable upload
            var initMeta = new JObject
            {
                ["file"] = new JObject { ["display_name"] = displayName }
            };

            var initRequest = new HttpRequestMessage(HttpMethod.Post, UploadInitUrl + "?uploadType=resumable")
            {
                Content = new StringContent(initMeta.ToString(Formatting.None), Encoding.UTF8, "application/json")
            };
            initRequest.Headers.Add("x-goog-api-key", apiKey);
            initRequest.Content.Headers.Add("X-Goog-Upload-Protocol", "resumable");
            initRequest.Content.Headers.Add("X-Goog-Upload-Command", "start");
            initRequest.Content.Headers.Add("X-Goog-Upload-Header-Content-Length", fileBytes.Length.ToString());
            initRequest.Content.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);

            using (var initResponse = await Http.SendAsync(initRequest))
            {
                if (!initResponse.IsSuccessStatusCode)
                {
                    string err = await initResponse.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(
                        "Gemini upload init failed: " + (err.Length > 300 ? err.Substring(0, 300) : err));
                }

                string uploadUrl = null;
                if (initResponse.Headers.TryGetValues("X-Goog-Upload-URL", out var vals))
                {
                    foreach (var v in vals) { uploadUrl = v; break; }
                }
                if (string.IsNullOrEmpty(uploadUrl))
                    throw new InvalidOperationException("Gemini upload did not return an upload URL.");

                // Step 2: upload the bytes
                var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
                {
                    Content = new ByteArrayContent(fileBytes)
                };
                uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
                uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");

                using (var uploadResponse = await Http.SendAsync(uploadRequest))
                {
                    string uploadBody = await uploadResponse.Content.ReadAsStringAsync();
                    if (!uploadResponse.IsSuccessStatusCode)
                        throw new InvalidOperationException(
                            "Gemini file upload failed: " + (uploadBody.Length > 300 ? uploadBody.Substring(0, 300) : uploadBody));

                    var uploadResult = JObject.Parse(uploadBody);
                    string fileUri = uploadResult["file"]?["uri"]?.ToString();
                    if (string.IsNullOrEmpty(fileUri))
                        throw new InvalidOperationException("Gemini upload response did not include a file URI.");
                    return fileUri;
                }
            }
        }

        // ----------------------------------------------------------------
        // HTTP call to Gemini generateContent
        // ----------------------------------------------------------------

        private static async Task<string> PostToGeminiAsync(JObject body, string apiKey)
        {
            string json = body.ToString(Formatting.None);
            var request = new HttpRequestMessage(HttpMethod.Post, GenerateUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-goog-api-key", apiKey);

            using (var response = await Http.SendAsync(request))
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        "Gemini API error " + (int)response.StatusCode + ": " +
                        (responseBody.Length > 500 ? responseBody.Substring(0, 500) : responseBody));

                return ExtractTextFromGeminiResponse(responseBody);
            }
        }

        // ----------------------------------------------------------------
        // Extract text from Gemini generateContent response
        // ----------------------------------------------------------------

        private static string ExtractTextFromGeminiResponse(string responseJson)
        {
            JObject parsed;
            try { parsed = JObject.Parse(responseJson); }
            catch { return responseJson; }

            // Gemini returns: {"candidates": [{"content": {"parts": [{"text": "..."}]}}]}
            var candidates = parsed["candidates"] as JArray;
            if (candidates == null || candidates.Count == 0)
            {
                // Check for error
                string err = parsed["error"]?["message"]?.ToString();
                if (!string.IsNullOrEmpty(err))
                    throw new InvalidOperationException("Gemini error: " + err);
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (JToken candidate in candidates)
            {
                var parts = candidate["content"]?["parts"] as JArray;
                if (parts == null) continue;
                foreach (JToken part in parts)
                {
                    string text = part["text"]?.ToString();
                    if (!string.IsNullOrEmpty(text))
                        sb.Append(text);
                }
            }
            return sb.ToString().Trim();
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static bool IsYouTubeUrl(string source)
        {
            if (string.IsNullOrEmpty(source)) return false;
            return source.StartsWith("https://www.youtube.com/", StringComparison.OrdinalIgnoreCase) ||
                   source.StartsWith("https://youtu.be/", StringComparison.OrdinalIgnoreCase) ||
                   source.StartsWith("http://www.youtube.com/", StringComparison.OrdinalIgnoreCase) ||
                   source.StartsWith("http://youtu.be/", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetVideoMimeType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".mp4":  return "video/mp4";
                case ".mpeg":
                case ".mpg":  return "video/mpeg";
                case ".mov":  return "video/quicktime";
                case ".avi":  return "video/x-msvideo";
                case ".flv":  return "video/x-flv";
                case ".webm": return "video/webm";
                case ".mkv":  return "video/x-matroska";
                case ".3gp":  return "video/3gpp";
                case ".wmv":  return "video/x-ms-wmv";
                default:      return "video/mp4";
            }
        }
    }
}
