using System;
using System.Collections.Generic;

namespace FourthDevs.Gmail.Models
{
    internal class GmailToken
    {
        public string AccessToken  { get; set; }
        public string RefreshToken { get; set; }
        public string TokenType    { get; set; }
        public int    ExpiresIn    { get; set; }
        public DateTime ExpiryTime { get; set; }

        public bool IsExpired()
        {
            return DateTime.UtcNow >= ExpiryTime.AddSeconds(-60);
        }
    }

    internal class GmailMessageSummary
    {
        public string Id       { get; set; }
        public string ThreadId { get; set; }
        public string Snippet  { get; set; }
        public string From     { get; set; }
        public string Subject  { get; set; }
        public string Date     { get; set; }
        public List<string> Labels { get; set; } = new List<string>();
    }

    internal class GmailMessageDetail
    {
        public string Id       { get; set; }
        public string ThreadId { get; set; }
        public string From     { get; set; }
        public string To       { get; set; }
        public string Cc       { get; set; }
        public string Subject  { get; set; }
        public string Date     { get; set; }
        public string MessageId { get; set; }
        public string Body     { get; set; }
        public List<string> Labels { get; set; } = new List<string>();
        public List<GmailAttachmentInfo> Attachments { get; set; } = new List<GmailAttachmentInfo>();
    }

    internal class GmailAttachmentInfo
    {
        public string AttachmentId { get; set; }
        public string Filename     { get; set; }
        public string MimeType     { get; set; }
        public int    Size         { get; set; }
    }

    internal class GmailToolResult
    {
        public object Data   { get; set; }
        public string Hint   { get; set; }
        public string Status { get; set; }
        public string Error  { get; set; }

        public static GmailToolResult Success(object data, string hint = null)
        {
            return new GmailToolResult { Data = data, Hint = hint, Status = "success" };
        }

        public static GmailToolResult Failure(string error, string hint = null)
        {
            return new GmailToolResult
            {
                Data   = null,
                Status = "error",
                Error  = error,
                Hint   = hint ?? "Recovery: check that the OAuth token is valid and the Gmail API is enabled"
            };
        }
    }

    internal class GmailToolDefinition
    {
        public string Name        { get; set; }
        public string Description { get; set; }
        public Newtonsoft.Json.Linq.JObject Parameters { get; set; }
        public Func<Newtonsoft.Json.Linq.JObject, System.Threading.Tasks.Task<object>> Handler { get; set; }
    }

    internal class AgentRunResult
    {
        public string       Text               { get; set; } = string.Empty;
        public string       ResponseId         { get; set; }
        public int          Turns              { get; set; }
        public List<object> ConversationHistory { get; set; } = new List<object>();
    }
}
