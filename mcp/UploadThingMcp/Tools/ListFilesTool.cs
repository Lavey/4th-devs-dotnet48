using System;
using System.Text;
using FourthDevs.UploadThingMcp.Adapters;
using Newtonsoft.Json.Linq;

namespace FourthDevs.UploadThingMcp.Tools
{
    internal class ListFilesTool
    {
        private readonly UploadThingClient _client;

        public ListFilesTool(UploadThingClient client)
        {
            _client = client;
        }

        public JObject GetToolDefinition()
        {
            return JObject.Parse(@"{
                ""name"": ""list_files"",
                ""description"": ""List files stored in UploadThing"",
                ""inputSchema"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""limit"":  {""type"": ""integer"", ""description"": ""Max files to return (default: 50)"",  ""default"": 50},
                        ""offset"": {""type"": ""integer"", ""description"": ""Offset for pagination (default: 0)"", ""default"": 0}
                    }
                }
            }");
        }

        public string Execute(JObject args)
        {
            int limit  = (int?)args["limit"]  ?? 50;
            int offset = (int?)args["offset"] ?? 0;

            try
            {
                var result = _client.ListFilesAsync(limit, offset).GetAwaiter().GetResult();

                if (result?.Files == null || result.Files.Length == 0)
                    return "No files found.";

                var sb = new StringBuilder();
                sb.AppendLine($"Found {result.Files.Length} file(s){(result.HasMore ? " (more available)" : "")}:");
                foreach (var f in result.Files)
                    sb.AppendLine($"  - {f.Name} | key: {f.Key} | url: {f.Url} | size: {f.Size} bytes | status: {f.Status} | uploaded: {f.UploadedAt}");
                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                return "Error listing files: " + ex.Message;
            }
        }
    }
}
