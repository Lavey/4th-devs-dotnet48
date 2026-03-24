using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.Email.Models
{
    public class Email
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("threadId")]
        public string ThreadId { get; set; }

        [JsonProperty("account")]
        public string Account { get; set; }

        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public List<string> To { get; set; } = new List<string>();

        [JsonProperty("cc")]
        public List<string> Cc { get; set; } = new List<string>();

        [JsonProperty("subject")]
        public string Subject { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("snippet")]
        public string Snippet { get; set; }

        [JsonProperty("date")]
        public string Date { get; set; }

        [JsonProperty("labelIds")]
        public List<string> LabelIds { get; set; } = new List<string>();

        [JsonProperty("isRead")]
        public bool IsRead { get; set; }

        [JsonProperty("hasAttachments")]
        public bool HasAttachments { get; set; }
    }

    public class Label
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("account")]
        public string Account { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
        public string Color { get; set; }
    }

    public class Draft
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("account")]
        public string Account { get; set; }

        [JsonProperty("to")]
        public List<string> To { get; set; } = new List<string>();

        [JsonProperty("cc")]
        public List<string> Cc { get; set; } = new List<string>();

        [JsonProperty("subject")]
        public string Subject { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("inReplyTo", NullValueHandling = NullValueHandling.Ignore)]
        public string InReplyTo { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }
    }

    public class Account
    {
        [JsonProperty("email")]
        public string EmailAddress { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("projectName")]
        public string ProjectName { get; set; }
    }

    public class ReplyPlan
    {
        public string EmailId { get; set; }
        public string Account { get; set; }
        public string RecipientEmail { get; set; }
        public string ContactType { get; set; }
        public string Reason { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
    }

    public class DraftSessionResult
    {
        public ReplyPlan Plan { get; set; }
        public string DraftId { get; set; }
        public List<KBEntryInfo> KBEntriesLoaded { get; set; } = new List<KBEntryInfo>();
        public List<KBBlockedInfo> KBEntriesBlocked { get; set; } = new List<KBBlockedInfo>();
        public string DraftBody { get; set; }
    }

    public class KBEntryInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
    }

    public class KBBlockedInfo
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public string Reason { get; set; }
    }

    public class KnowledgeEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("account")]
        public string Account { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("updatedAt")]
        public string UpdatedAt { get; set; }
    }

    public class KnowledgeAccess
    {
        public string Tool { get; set; }
        public string Account { get; set; }
        public string Query { get; set; }
        public List<KBReturnedEntry> Returned { get; set; } = new List<KBReturnedEntry>();
        public List<KBBlockedEntry> Blocked { get; set; } = new List<KBBlockedEntry>();
    }

    public class KBReturnedEntry
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Scope { get; set; }
    }

    public class KBBlockedEntry
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Owner { get; set; }
    }

    public class EmailSnapshot
    {
        public string Id { get; set; }
        public string Account { get; set; }
        public string From { get; set; }
        public string Subject { get; set; }
        public List<string> LabelIds { get; set; } = new List<string>();
        public bool IsRead { get; set; }
    }

    public class Change
    {
        public string Type { get; set; }
        public string Account { get; set; }
        public string EmailId { get; set; }
        public string EmailSubject { get; set; }
        public string LabelName { get; set; }
        public string LabelColor { get; set; }
        public List<string> DraftTo { get; set; }
        public string DraftSubject { get; set; }
    }

    /// <summary>
    /// Represents a tool with name, description, JSON schema for parameters, and a handler.
    /// </summary>
    public class ToolDef
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Newtonsoft.Json.Linq.JObject Parameters { get; set; }
        public System.Func<Newtonsoft.Json.Linq.JObject, System.Threading.Tasks.Task<object>> Handler { get; set; }
    }
}
