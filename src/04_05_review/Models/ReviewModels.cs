using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.Review.Models
{
    // ---- Markdown block ----

    internal sealed class MarkdownBlock
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("order")]
        public int Order { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("html")]
        public string Html { get; set; } = string.Empty;

        [JsonProperty("meta")]
        public Dictionary<string, object> Meta { get; set; } = new Dictionary<string, object>();

        [JsonProperty("reviewable")]
        public bool Reviewable { get; set; } = true;
    }

    // ---- Review comment ----

    internal sealed class ReviewComment
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("blockId")]
        public string BlockId { get; set; }

        [JsonProperty("quote")]
        public string Quote { get; set; }

        [JsonProperty("start")]
        public int Start { get; set; }

        [JsonProperty("end")]
        public int End { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; } = "comment";

        [JsonProperty("severity")]
        public string Severity { get; set; } = "low";

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("suggestion")]
        public string Suggestion { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = "open";

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("previousText")]
        public string PreviousText { get; set; }
    }

    // ---- Review ----

    internal sealed class ReviewData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("documentPath")]
        public string DocumentPath { get; set; }

        [JsonProperty("promptPath")]
        public string PromptPath { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("comments")]
        public List<ReviewComment> Comments { get; set; } = new List<ReviewComment>();

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("completedAt")]
        public string CompletedAt { get; set; }
    }

    // ---- Document with parsed frontmatter ----

    internal sealed class DocumentData
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("frontmatter")]
        public Dictionary<string, object> Frontmatter { get; set; } = new Dictionary<string, object>();

        [JsonProperty("blocks")]
        public List<MarkdownBlock> Blocks { get; set; } = new List<MarkdownBlock>();
    }

    // ---- Prompt ----

    internal sealed class PromptData
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("frontmatter")]
        public Dictionary<string, object> Frontmatter { get; set; } = new Dictionary<string, object>();

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("contextContent")]
        public string ContextContent { get; set; }
    }

    // ---- Agent profile ----

    internal sealed class AgentProfile
    {
        public string Name { get; set; }
        public string Model { get; set; }
        public string Content { get; set; }
        public Dictionary<string, object> Frontmatter { get; set; } = new Dictionary<string, object>();
    }

    // ---- Review event (for NDJSON streaming) ----

    internal sealed class ReviewEvent
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("reviewId", NullValueHandling = NullValueHandling.Ignore)]
        public string ReviewId { get; set; }

        [JsonProperty("blockId", NullValueHandling = NullValueHandling.Ignore)]
        public string BlockId { get; set; }

        [JsonProperty("comment", NullValueHandling = NullValueHandling.Ignore)]
        public ReviewComment Comment { get; set; }

        [JsonProperty("summary", NullValueHandling = NullValueHandling.Ignore)]
        public string Summary { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }

        [JsonProperty("review", NullValueHandling = NullValueHandling.Ignore)]
        public ReviewData Review { get; set; }
    }

    // ---- Quote range ----

    internal struct QuoteRange
    {
        public int Start;
        public int End;
        public bool Found;
    }

    // ---- Bootstrap response ----

    internal sealed class BootstrapResponse
    {
        [JsonProperty("documents")]
        public List<DocumentListItem> Documents { get; set; } = new List<DocumentListItem>();

        [JsonProperty("prompts")]
        public List<PromptListItem> Prompts { get; set; } = new List<PromptListItem>();
    }

    internal sealed class DocumentListItem
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }
    }

    internal sealed class PromptListItem
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("modes")]
        public List<string> Modes { get; set; } = new List<string>();
    }

    // ---- Document response (for GET /api/document) ----

    internal sealed class DocumentResponse
    {
        [JsonProperty("document")]
        public DocumentData Document { get; set; }

        [JsonProperty("review")]
        public ReviewData Review { get; set; }
    }

    // ---- Tool definition for agent ----

    internal sealed class ToolSpec
    {
        public Newtonsoft.Json.Linq.JObject Definition { get; set; }
        public Func<string, string> Handler { get; set; }
    }
}
