using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FourthDevs.ChatUi.Models
{
    // -------------------------------------------------------------------------
    // Conversation data model – snapshot, messages, enums.
    // -------------------------------------------------------------------------

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessageRole
    {
        user,
        assistant
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessageStatus
    {
        complete,
        streaming,
        error
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum StreamMode
    {
        mock,
        live
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ArtifactKind
    {
        markdown,
        json,
        text,
        file
    }

    public class ConversationSnapshot
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("mode")]
        public StreamMode Mode { get; set; }

        [JsonProperty("historyCount")]
        public int HistoryCount { get; set; }

        [JsonProperty("messages")]
        public List<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
    }

    public class ConversationMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("role")]
        public MessageRole Role { get; set; }

        [JsonProperty("status")]
        public MessageStatus Status { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("events", NullValueHandling = NullValueHandling.Ignore)]
        public List<BaseStreamEvent> Events { get; set; }
    }
}
