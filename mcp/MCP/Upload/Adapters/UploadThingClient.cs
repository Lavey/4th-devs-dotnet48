using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Mcp.Upload.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Mcp.Upload.Adapters
{
    internal class UploadThingClient : IDisposable
    {
        private const string BaseUrl = "https://api.uploadthing.com/v6/";

        private readonly HttpClient _http;
        private readonly bool _hasToken;
        private bool _disposed;

        public UploadThingClient(string apiToken)
        {
            _hasToken = !string.IsNullOrWhiteSpace(apiToken);
            _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (_hasToken)
                _http.DefaultRequestHeaders.Add("X-Uploadthing-Api-Key", apiToken);
        }

        public async Task<UploadResult[]> UploadFromUrlsAsync(UploadFileRequest[] files)
        {
            var body = new JObject { ["files"] = JArray.FromObject(files) };
            string responseJson = await PostAsync("uploadFiles", body.ToString(Formatting.None));

            var result = JObject.Parse(responseJson);
            // API may return { data: [...] } or { files: [...] }
            var data = (result["data"] as JArray) ?? (result["files"] as JArray);
            if (data == null)
                throw new InvalidOperationException("Unexpected response from uploadFiles: " + responseJson);

            return data.ToObject<UploadResult[]>();
        }

        public async Task<FileListResult> ListFilesAsync(int limit = 500, int offset = 0)
        {
            var body = new JObject { ["limit"] = limit, ["offset"] = offset };
            string responseJson = await PostAsync("listFiles", body.ToString(Formatting.None));
            return JsonConvert.DeserializeObject<FileListResult>(responseJson);
        }

        public async Task<bool> RenameFilesAsync(RenameRequest[] renames)
        {
            var body = new JObject { ["updates"] = JArray.FromObject(renames) };
            string responseJson = await PostAsync("renameFiles", body.ToString(Formatting.None));
            var result = JObject.Parse(responseJson);
            return result["success"]?.Value<bool>() ?? false;
        }

        public async Task<bool> DeleteFilesAsync(string[] fileKeys)
        {
            var body = new JObject { ["fileKeys"] = JArray.FromObject(fileKeys) };
            string responseJson = await PostAsync("deleteFiles", body.ToString(Formatting.None));
            var result = JObject.Parse(responseJson);
            return result["success"]?.Value<bool>() ?? false;
        }

        private async Task<string> PostAsync(string endpoint, string jsonBody)
        {
            if (!_hasToken)
                throw new InvalidOperationException(
                    "UPLOADTHING_TOKEN is not configured. " +
                    "Set the UPLOADTHING_TOKEN environment variable or add it to App.config.");

            using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
            {
                using (var response = await _http.PostAsync(endpoint, content))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException(
                            string.Format("UploadThing API error ({0}): {1}", (int)response.StatusCode, body));
                    return body;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _http.Dispose();
            }
        }
    }
}
