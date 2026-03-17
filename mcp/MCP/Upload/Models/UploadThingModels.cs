using Newtonsoft.Json;

namespace FourthDevs.Mcp.Upload.Models
{
    internal class UploadFileRequest
    {
        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("base64", NullValueHandling = NullValueHandling.Ignore)]
        public string Base64 { get; set; }
    }

    internal class UploadResult
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }
    }

    internal class UploadThingFile
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("uploadedAt")]
        public string UploadedAt { get; set; }
    }

    internal class FileListResult
    {
        [JsonProperty("files")]
        public UploadThingFile[] Files { get; set; }

        [JsonProperty("hasMore")]
        public bool HasMore { get; set; }
    }

    internal class RenameRequest
    {
        [JsonProperty("fileKey")]
        public string FileKey { get; set; }

        [JsonProperty("newName")]
        public string NewName { get; set; }
    }
}
