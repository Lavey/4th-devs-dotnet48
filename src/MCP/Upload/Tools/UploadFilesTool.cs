using System;
using System.Text;
using FourthDevs.Mcp.Upload.Adapters;
using FourthDevs.Mcp.Upload.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Mcp.Upload.Tools
{
    internal class UploadFilesTool
    {
        private readonly UploadThingClient _client;

        public UploadFilesTool(UploadThingClient client)
        {
            _client = client;
        }

        public JObject GetToolDefinition()
        {
            return JObject.Parse(@"{
                ""name"": ""upload_files"",
                ""description"": ""Upload files to UploadThing from URLs or base64 content"",
                ""inputSchema"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""files"": {
                            ""type"": ""array"",
                            ""description"": ""Files to upload"",
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""url"":    {""type"": ""string"", ""description"": ""URL to download and upload""},
                                    ""base64"": {""type"": ""string"", ""description"": ""Base64-encoded file content""},
                                    ""name"":   {""type"": ""string"", ""description"": ""File name""}
                                },
                                ""required"": [""name""]
                            }
                        }
                    },
                    ""required"": [""files""]
                }
            }");
        }

        public string Execute(JObject args)
        {
            var filesArr = args["files"] as JArray;
            if (filesArr == null || filesArr.Count == 0)
                return "Error: 'files' parameter is required and must not be empty.";

            var requests = new UploadFileRequest[filesArr.Count];
            for (int i = 0; i < filesArr.Count; i++)
            {
                var f = filesArr[i] as JObject;
                if (f == null) continue;
                requests[i] = new UploadFileRequest
                {
                    Url    = (string)f["url"],
                    Name   = (string)f["name"],
                    Base64 = (string)f["base64"]
                };
            }

            try
            {
                var results = _client.UploadFromUrlsAsync(requests).GetAwaiter().GetResult();

                var sb = new StringBuilder();
                sb.AppendLine($"Uploaded {results.Length} file(s):");
                foreach (var r in results)
                    sb.AppendLine($"  - {r.Name} | key: {r.Key} | url: {r.Url} | size: {r.Size} bytes");
                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                return "Error uploading files: " + ex.Message;
            }
        }
    }
}
