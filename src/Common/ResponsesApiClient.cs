using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common.Models;
using Newtonsoft.Json;

namespace FourthDevs.Common
{
    /// <summary>
    /// Thin wrapper around the OpenAI / OpenRouter Responses API.
    /// Builds the HTTP request, sends it, and returns a parsed response.
    /// </summary>
    public sealed class ResponsesApiClient : IDisposable
    {
        private readonly HttpClient _http;

        public ResponsesApiClient()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

            if (AiConfig.Provider == "openrouter")
            {
                if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                    _http.DefaultRequestHeaders.TryAddWithoutValidation(
                        "HTTP-Referer", AiConfig.HttpReferer);

                if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                    _http.DefaultRequestHeaders.TryAddWithoutValidation(
                        "X-Title", AiConfig.AppName);
            }
        }

        /// <summary>
        /// Sends a Responses API request and returns the parsed response.
        /// Throws <see cref="HttpRequestException"/> or <see cref="InvalidOperationException"/>
        /// on API errors.
        /// </summary>
        public async Task<ResponsesResponse> SendAsync(ResponsesRequest request)
        {
            string json = JsonConvert.SerializeObject(request, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await _http.PostAsync(AiConfig.ApiEndpoint, content))
            {
                string body = await response.Content.ReadAsStringAsync();
                var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(body);

                if (!response.IsSuccessStatusCode || parsed?.Error != null)
                {
                    string msg = parsed?.Error?.Message
                        ?? $"Request failed with status {(int)response.StatusCode}";
                    throw new InvalidOperationException(msg);
                }

                return parsed;
            }
        }

        /// <summary>
        /// Extracts the plain text from the first output_text content part,
        /// falling back to the top-level output_text shorthand.
        /// </summary>
        public static string ExtractText(ResponsesResponse response)
        {
            if (!string.IsNullOrWhiteSpace(response.OutputText))
                return response.OutputText;

            if (response.Output != null)
            {
                foreach (var item in response.Output)
                {
                    if (item.Type == "message" && item.Content != null)
                    {
                        foreach (var part in item.Content)
                        {
                            if (part.Type == "output_text" && !string.IsNullOrEmpty(part.Text))
                                return part.Text;
                        }
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns all function-call items from the response output.
        /// </summary>
        public static List<OutputItem> GetToolCalls(ResponsesResponse response)
        {
            var calls = new List<OutputItem>();
            if (response.Output == null) return calls;

            foreach (var item in response.Output)
            {
                if (item.Type == "function_call")
                    calls.Add(item);
            }

            return calls;
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
