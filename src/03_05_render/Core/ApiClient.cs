using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Render.Core
{
    /// <summary>
    /// Raw HTTP wrapper for the OpenAI Responses API.
    /// </summary>
    internal static class ApiClient
    {
        public static async Task<JObject> PostAsync(JObject body)
        {
            string json = body.ToString(Formatting.None);

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

                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject parsed = JObject.Parse(responseBody);

                    if (!response.IsSuccessStatusCode || parsed["error"] != null)
                    {
                        string msg = parsed["error"]?["message"]?.ToString()
                            ?? "Request failed with status " + (int)response.StatusCode;
                        throw new InvalidOperationException(msg);
                    }

                    return parsed;
                }
            }
        }

        public static string ExtractText(JObject parsed)
        {
            string outputText = parsed["output_text"]?.ToString();
            if (!string.IsNullOrWhiteSpace(outputText))
                return outputText;

            var outputArray = parsed["output"] as JArray;
            if (outputArray != null)
            {
                foreach (JToken item in outputArray)
                {
                    if (item["type"]?.ToString() == "message")
                    {
                        var contentArr = item["content"] as JArray;
                        if (contentArr != null)
                        {
                            foreach (JToken part in contentArr)
                            {
                                if (part["type"]?.ToString() == "output_text")
                                {
                                    string text = part["text"]?.ToString();
                                    if (!string.IsNullOrEmpty(text))
                                        return text;
                                }
                            }
                        }
                    }
                }
            }

            return string.Empty;
        }

        public static List<JObject> GetToolCalls(JObject parsed)
        {
            var calls = new List<JObject>();
            var outputArray = parsed["output"] as JArray;
            if (outputArray == null)
                return calls;

            foreach (JToken item in outputArray)
            {
                if (item["type"]?.ToString() == "function_call")
                    calls.Add((JObject)item);
            }

            return calls;
        }
    }
}
