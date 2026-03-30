using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Garden.Core
{
    /// <summary>
    /// Thin HTTP client for the OpenAI / OpenRouter Responses API.
    /// Sends raw JObject requests and returns raw JObject responses.
    /// </summary>
    internal static class ApiClient
    {
        public static async Task<JObject> CompletionAsync(
            string model,
            string instructions,
            JArray input,
            JArray tools,
            string previousResponseId)
        {
            var body = new JObject
            {
                ["model"] = AiConfig.ResolveModel(model),
                ["instructions"] = instructions,
                ["input"] = input,
            };

            if (tools != null && tools.Count > 0)
                body["tools"] = tools;

            if (previousResponseId != null)
                body["previous_response_id"] = previousResponseId;

            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "X-Title", AiConfig.AppName);
                }

                string json = body.ToString(Formatting.None);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    return JObject.Parse(responseBody);
                }
            }
        }
    }
}
