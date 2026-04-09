using Newtonsoft.Json;

namespace FourthDevs.MultiAgentApi.Models
{
    // -------------------------------------------------------------------------
    // File and upload models
    // -------------------------------------------------------------------------

    internal class FileRecord
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("tenant_id")]
        public string TenantId { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("mime_type")]
        public string MimeType { get; set; }

        [JsonProperty("size_bytes")]
        public long SizeBytes { get; set; }

        [JsonProperty("storage_path")]
        public string StoragePath { get; set; }

        [JsonProperty("created_by")]
        public string CreatedBy { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    internal class Upload
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("file_id")]
        public string FileId { get; set; }

        [JsonProperty("tenant_id")]
        public string TenantId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("completed_at")]
        public string CompletedAt { get; set; }
    }

    internal class FileLink
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("file_id")]
        public string FileId { get; set; }

        [JsonProperty("linked_type")]
        public string LinkedType { get; set; }

        [JsonProperty("linked_id")]
        public string LinkedId { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }
}
