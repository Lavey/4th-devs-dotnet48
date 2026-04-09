using Newtonsoft.Json;

namespace FourthDevs.MultiAgentApi.Models
{
    // -------------------------------------------------------------------------
    // Domain event models
    // -------------------------------------------------------------------------

    internal class DomainEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("tenant_id")]
        public string TenantId { get; set; }

        [JsonProperty("aggregate_type")]
        public string AggregateType { get; set; }

        [JsonProperty("aggregate_id")]
        public string AggregateId { get; set; }

        [JsonProperty("event_type")]
        public string EventType { get; set; }

        [JsonProperty("payload")]
        public string Payload { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    internal class EventOutbox
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("event_id")]
        public string EventId { get; set; }

        [JsonProperty("delivered")]
        public bool Delivered { get; set; }

        [JsonProperty("delivered_at")]
        public string DeliveredAt { get; set; }
    }
}
