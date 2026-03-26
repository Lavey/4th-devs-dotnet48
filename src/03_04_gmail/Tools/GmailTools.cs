using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Gmail.Gmail;
using FourthDevs.Gmail.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Gmail.Tools
{
    internal static class GmailTools
    {
        private static readonly string WorkspacePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");

        public static List<GmailToolDefinition> CreateTools(string accessToken)
        {
            var client = new GmailClient(accessToken);

            return new List<GmailToolDefinition>
            {
                BuildSearchTool(client),
                BuildReadTool(client),
                BuildSendTool(client),
                BuildModifyTool(client),
                BuildAttachmentTool(client)
            };
        }

        // ----------------------------------------------------------------
        // gmail_search
        // ----------------------------------------------------------------

        private static GmailToolDefinition BuildSearchTool(GmailClient client)
        {
            return new GmailToolDefinition
            {
                Name        = "gmail_search",
                Description = "Search emails using Gmail query syntax (from:, to:, subject:, is:unread, etc.). Returns a list of message summaries with IDs, senders, subjects and snippets.",
                Parameters  = JObject.Parse(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""query"":              { ""type"": ""string"",  ""description"": ""Gmail search query"" },
                        ""max_results"":        { ""type"": ""integer"", ""description"": ""Max messages to return (default 20)"" },
                        ""include_spam_trash"": { ""type"": ""boolean"", ""description"": ""Include spam and trash (default false)"" }
                    },
                    ""required"": [""query""],
                    ""additionalProperties"": false
                }"),
                Handler = async args =>
                {
                    string query          = args["query"]?.ToString() ?? string.Empty;
                    int    maxResults     = args["max_results"]?.Value<int>()        ?? 20;
                    bool   includeSpamTrash = args["include_spam_trash"]?.Value<bool>() ?? false;

                    try
                    {
                        var messages = await client.SearchAsync(query, maxResults, includeSpamTrash);
                        string hint = messages.Count > 0
                            ? "Next: to read email content, call gmail_read with message_id='" + messages[0].Id + "'"
                            : "No messages found. Try a different query.";

                        return (object)GmailToolResult.Success(messages, hint);
                    }
                    catch (Exception ex)
                    {
                        return (object)GmailToolResult.Failure("Search failed: " + ex.Message);
                    }
                }
            };
        }

        // ----------------------------------------------------------------
        // gmail_read
        // ----------------------------------------------------------------

        private static GmailToolDefinition BuildReadTool(GmailClient client)
        {
            return new GmailToolDefinition
            {
                Name        = "gmail_read",
                Description = "Read the full content of a specific email including body, headers and attachment info.",
                Parameters  = JObject.Parse(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""message_id"": { ""type"": ""string"", ""description"": ""Gmail message ID"" }
                    },
                    ""required"": [""message_id""],
                    ""additionalProperties"": false
                }"),
                Handler = async args =>
                {
                    string messageId = args["message_id"]?.ToString() ?? string.Empty;

                    try
                    {
                        var detail = await client.ReadAsync(messageId);
                        string hint = detail.Attachments.Count > 0
                            ? "Note: this email has " + detail.Attachments.Count + " attachment(s). Use gmail_attachment to download them."
                            : "Next: use gmail_send to reply or gmail_modify to label/archive this email.";

                        return (object)GmailToolResult.Success(detail, hint);
                    }
                    catch (Exception ex)
                    {
                        return (object)GmailToolResult.Failure("Read failed: " + ex.Message);
                    }
                }
            };
        }

        // ----------------------------------------------------------------
        // gmail_send
        // ----------------------------------------------------------------

        private static GmailToolDefinition BuildSendTool(GmailClient client)
        {
            return new GmailToolDefinition
            {
                Name        = "gmail_send",
                Description = "Send or reply to an email. If the recipient is outside the whitelist, creates a draft instead.",
                Parameters  = JObject.Parse(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""to"":          { ""type"": ""string"", ""description"": ""Recipient email address"" },
                        ""subject"":     { ""type"": ""string"", ""description"": ""Email subject"" },
                        ""body"":        { ""type"": ""string"", ""description"": ""Plain text body"" },
                        ""thread_id"":   { ""type"": ""string"", ""description"": ""Thread ID for replies"" },
                        ""in_reply_to"": { ""type"": ""string"", ""description"": ""Message-Id header of email being replied to"" },
                        ""references"":  { ""type"": ""string"", ""description"": ""References header for threading"" },
                        ""cc"":          { ""type"": ""string"", ""description"": ""CC recipients"" }
                    },
                    ""required"": [""to"", ""subject"", ""body""],
                    ""additionalProperties"": false
                }"),
                Handler = async args =>
                {
                    string to         = args["to"]?.ToString()          ?? string.Empty;
                    string subject    = args["subject"]?.ToString()     ?? string.Empty;
                    string body       = args["body"]?.ToString()        ?? string.Empty;
                    string threadId   = args["thread_id"]?.ToString();
                    string inReplyTo  = args["in_reply_to"]?.ToString();
                    string references = args["references"]?.ToString();
                    string cc         = args["cc"]?.ToString();

                    bool whitelisted = IsWhitelisted(to);

                    try
                    {
                        if (whitelisted)
                        {
                            string id = await client.SendAsync(to, subject, body, threadId, inReplyTo, references, cc);
                            return (object)GmailToolResult.Success(
                                new { sent = true, message_id = id, to },
                                "Email sent successfully.");
                        }
                        else
                        {
                            string id = await client.CreateDraftAsync(to, subject, body, threadId, inReplyTo, references, cc);
                            return (object)GmailToolResult.Success(
                                new { sent = false, draft = true, draft_id = id, to,
                                      reason = "Recipient is not in GMAIL_SEND_WHITELIST. Draft created instead." },
                                "Draft created. Check Gmail drafts folder to review and send manually.");
                        }
                    }
                    catch (Exception ex)
                    {
                        return (object)GmailToolResult.Failure("Send failed: " + ex.Message);
                    }
                }
            };
        }

        // ----------------------------------------------------------------
        // gmail_modify
        // ----------------------------------------------------------------

        private static GmailToolDefinition BuildModifyTool(GmailClient client)
        {
            return new GmailToolDefinition
            {
                Name        = "gmail_modify",
                Description = "Add or remove labels on an email. Common labels: INBOX, UNREAD, STARRED, IMPORTANT, SPAM, TRASH.",
                Parameters  = JObject.Parse(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""message_id"":    { ""type"": ""string"", ""description"": ""Gmail message ID"" },
                        ""add_labels"":    { ""type"": ""array"",  ""items"": { ""type"": ""string"" }, ""description"": ""Label IDs to add"" },
                        ""remove_labels"": { ""type"": ""array"",  ""items"": { ""type"": ""string"" }, ""description"": ""Label IDs to remove"" }
                    },
                    ""required"": [""message_id""],
                    ""additionalProperties"": false
                }"),
                Handler = async args =>
                {
                    string messageId = args["message_id"]?.ToString() ?? string.Empty;

                    var addLabels    = ParseStringArray(args["add_labels"]);
                    var removeLabels = ParseStringArray(args["remove_labels"]);

                    try
                    {
                        var updatedLabels = await client.ModifyAsync(messageId, addLabels, removeLabels);
                        return (object)GmailToolResult.Success(
                            new { message_id = messageId, labels = updatedLabels },
                            "Labels updated successfully.");
                    }
                    catch (Exception ex)
                    {
                        return (object)GmailToolResult.Failure("Modify failed: " + ex.Message);
                    }
                }
            };
        }

        // ----------------------------------------------------------------
        // gmail_attachment
        // ----------------------------------------------------------------

        private static GmailToolDefinition BuildAttachmentTool(GmailClient client)
        {
            return new GmailToolDefinition
            {
                Name        = "gmail_attachment",
                Description = "Download a specific email attachment. Optionally save it to the workspace folder.",
                Parameters  = JObject.Parse(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""message_id"":    { ""type"": ""string"", ""description"": ""Gmail message ID"" },
                        ""attachment_id"": { ""type"": ""string"", ""description"": ""Attachment ID from gmail_read"" },
                        ""save_to"":       { ""type"": ""string"", ""description"": ""Optional filename to save in workspace/ (e.g. 'report.pdf')"" }
                    },
                    ""required"": [""message_id"", ""attachment_id""],
                    ""additionalProperties"": false
                }"),
                Handler = async args =>
                {
                    string messageId    = args["message_id"]?.ToString()    ?? string.Empty;
                    string attachmentId = args["attachment_id"]?.ToString() ?? string.Empty;
                    string saveTo       = args["save_to"]?.ToString();

                    try
                    {
                        byte[] data = await client.GetAttachmentBytesAsync(messageId, attachmentId);
                        string savedPath = null;

                        if (!string.IsNullOrEmpty(saveTo))
                        {
                            string safeName = Path.GetFileName(saveTo);
                            savedPath = Path.Combine(WorkspacePath, safeName);
                            Directory.CreateDirectory(WorkspacePath);
                            File.WriteAllBytes(savedPath, data);
                        }

                        return (object)GmailToolResult.Success(
                            new
                            {
                                size_bytes = data.Length,
                                saved_to   = savedPath,
                                base64     = savedPath == null ? GmailClient.Base64UrlEncode(data) : null
                            },
                            savedPath != null
                                ? "Attachment saved to " + savedPath
                                : "Attachment downloaded. Provide 'save_to' to persist to disk.");
                    }
                    catch (Exception ex)
                    {
                        return (object)GmailToolResult.Failure("Attachment download failed: " + ex.Message);
                    }
                }
            };
        }

        // ----------------------------------------------------------------
        // Whitelist helper
        // ----------------------------------------------------------------

        private static bool IsWhitelisted(string to)
        {
            string raw = ConfigurationManager.AppSettings["GMAIL_SEND_WHITELIST"]?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(raw)) return false;

            string[] allowed = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            string toAddr = to.Trim().ToLowerInvariant();
            return Array.Exists(allowed, a => a.Trim().ToLowerInvariant() == toAddr);
        }

        private static IEnumerable<string> ParseStringArray(JToken token)
        {
            if (token == null) return new string[0];
            if (token is JArray arr)
                return arr.Select(t => t.ToString()).ToArray();
            return new string[0];
        }
    }
}
