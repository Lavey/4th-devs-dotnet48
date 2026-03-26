using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ContextAgent
{
    internal static class LlmClient
    {
        public static async Task<string> PostAsync(JObject body)
        {
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
                using (var resp = await http.PostAsync(AiConfig.ApiEndpoint, content)
                    .ConfigureAwait(false))
                {
                    string responseBody = await resp.Content.ReadAsStringAsync()
                        .ConfigureAwait(false);
                    var parsed = JObject.Parse(responseBody);

                    if (!resp.IsSuccessStatusCode)
                    {
                        string errMsg = (string)parsed["error"]?["message"]
                            ?? string.Format("Request failed ({0})", (int)resp.StatusCode);
                        throw new System.InvalidOperationException(errMsg);
                    }

                    // Extract text from response
                    var outputArr = parsed["output"] as JArray;
                    if (outputArr != null)
                    {
                        foreach (JObject item in outputArr)
                        {
                            if ((string)item["type"] == "message")
                            {
                                var contentToken = item["content"];
                                if (contentToken != null)
                                {
                                    if (contentToken.Type == JTokenType.String)
                                        return (string)contentToken;
                                    if (contentToken.Type == JTokenType.Array)
                                    {
                                        foreach (JObject c in (JArray)contentToken)
                                        {
                                            if ((string)c["type"] == "output_text")
                                                return (string)c["text"] ?? "";
                                            if (c["text"] != null)
                                                return (string)c["text"] ?? "";
                                        }
                                    }
                                }
                            }
                        }
                    }

                    return responseBody;
                }
            }
        }
    }
}
