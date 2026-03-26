using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Gmail.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Gmail.Gmail
{
    internal class GmailClient
    {
        private const string BaseUrl = "https://gmail.googleapis.com/gmail/v1/users/me";

        private readonly string _accessToken;
        private readonly HttpClient _http;

        public GmailClient(string accessToken)
        {
            _accessToken = accessToken;
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        // ----------------------------------------------------------------
        // Search
        // ----------------------------------------------------------------

        public async Task<List<GmailMessageSummary>> SearchAsync(
            string query,
            int maxResults = 20,
            bool includeSpamTrash = false)
        {
            string url = BaseUrl + "/messages?q=" + Uri.EscapeDataString(query) +
                         "&maxResults=" + maxResults +
                         (includeSpamTrash ? "&includeSpamTrash=true" : string.Empty);

            string json = await GetAsync(url);
            JObject obj  = JObject.Parse(json);
            JArray  msgs = obj["messages"] as JArray;

            if (msgs == null || msgs.Count == 0)
                return new List<GmailMessageSummary>();

            var results = new List<GmailMessageSummary>();
            foreach (JToken m in msgs)
            {
                string id = m["id"]?.ToString();
                if (string.IsNullOrEmpty(id)) continue;

                string metaJson = await GetAsync(
                    BaseUrl + "/messages/" + id +
                    "?format=metadata&metadataHeaders=From&metadataHeaders=Subject&metadataHeaders=Date");

                results.Add(ParseSummary(metaJson));
            }

            return results;
        }

        // ----------------------------------------------------------------
        // Read
        // ----------------------------------------------------------------

        public async Task<GmailMessageDetail> ReadAsync(string messageId)
        {
            string json = await GetAsync(BaseUrl + "/messages/" + messageId + "?format=full");
            return ParseDetail(json);
        }

        // ----------------------------------------------------------------
        // Send / Draft
        // ----------------------------------------------------------------

        public async Task<string> SendAsync(
            string to,
            string subject,
            string body,
            string threadId    = null,
            string inReplyTo   = null,
            string references  = null,
            string cc          = null)
        {
            string raw = BuildRawMessage(to, subject, body, inReplyTo, references, cc);
            var payload = new JObject { ["raw"] = raw };
            if (!string.IsNullOrEmpty(threadId)) payload["threadId"] = threadId;

            string json = await PostAsync(BaseUrl + "/messages/send", payload.ToString(Formatting.None));
            JObject resp = JObject.Parse(json);
            return resp["id"]?.ToString();
        }

        public async Task<string> CreateDraftAsync(
            string to,
            string subject,
            string body,
            string threadId    = null,
            string inReplyTo   = null,
            string references  = null,
            string cc          = null)
        {
            string raw = BuildRawMessage(to, subject, body, inReplyTo, references, cc);
            var msgPayload = new JObject { ["raw"] = raw };
            if (!string.IsNullOrEmpty(threadId)) msgPayload["threadId"] = threadId;

            var payload = new JObject { ["message"] = msgPayload };

            string json = await PostAsync(BaseUrl + "/drafts", payload.ToString(Formatting.None));
            JObject resp = JObject.Parse(json);
            return resp["id"]?.ToString();
        }

        // ----------------------------------------------------------------
        // Modify
        // ----------------------------------------------------------------

        public async Task<List<string>> ModifyAsync(
            string messageId,
            IEnumerable<string> addLabels    = null,
            IEnumerable<string> removeLabels = null)
        {
            var payload = new JObject
            {
                ["addLabelIds"]    = new JArray(addLabels    ?? new string[0]),
                ["removeLabelIds"] = new JArray(removeLabels ?? new string[0])
            };

            string json = await PostAsync(
                BaseUrl + "/messages/" + messageId + "/modify",
                payload.ToString(Formatting.None));

            JObject resp = JObject.Parse(json);
            var labelIds = resp["labelIds"] as JArray;
            var list = new List<string>();
            if (labelIds != null)
                foreach (JToken l in labelIds)
                    list.Add(l.ToString());
            return list;
        }

        // ----------------------------------------------------------------
        // Attachment
        // ----------------------------------------------------------------

        public async Task<byte[]> GetAttachmentBytesAsync(string messageId, string attachmentId)
        {
            string json = await GetAsync(
                BaseUrl + "/messages/" + messageId + "/attachments/" + attachmentId);
            JObject resp = JObject.Parse(json);
            string b64 = resp["data"]?.ToString() ?? string.Empty;
            return Base64UrlDecode(b64);
        }

        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

        private async Task<string> GetAsync(string url)
        {
            using (var response = await _http.GetAsync(url))
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        "Gmail API GET error " + (int)response.StatusCode + ": " + body);
                return body;
            }
        }

        private async Task<string> PostAsync(string url, string jsonBody)
        {
            using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
            using (var response = await _http.PostAsync(url, content))
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        "Gmail API POST error " + (int)response.StatusCode + ": " + body);
                return body;
            }
        }

        private static GmailMessageSummary ParseSummary(string json)
        {
            JObject obj = JObject.Parse(json);
            var headers = obj["payload"]?["headers"] as JArray ?? new JArray();

            var summary = new GmailMessageSummary
            {
                Id       = obj["id"]?.ToString(),
                ThreadId = obj["threadId"]?.ToString(),
                Snippet  = obj["snippet"]?.ToString()
            };

            foreach (JToken h in headers)
            {
                string name = h["name"]?.ToString() ?? string.Empty;
                string value = h["value"]?.ToString() ?? string.Empty;
                if (string.Equals(name, "From",    StringComparison.OrdinalIgnoreCase)) summary.From    = value;
                if (string.Equals(name, "Subject", StringComparison.OrdinalIgnoreCase)) summary.Subject = value;
                if (string.Equals(name, "Date",    StringComparison.OrdinalIgnoreCase)) summary.Date    = value;
            }

            var labelIds = obj["labelIds"] as JArray;
            if (labelIds != null)
                foreach (JToken l in labelIds)
                    summary.Labels.Add(l.ToString());

            return summary;
        }

        private static GmailMessageDetail ParseDetail(string json)
        {
            JObject obj     = JObject.Parse(json);
            var headers     = obj["payload"]?["headers"] as JArray ?? new JArray();
            var detail      = new GmailMessageDetail
            {
                Id       = obj["id"]?.ToString(),
                ThreadId = obj["threadId"]?.ToString()
            };

            foreach (JToken h in headers)
            {
                string name  = h["name"]?.ToString()  ?? string.Empty;
                string value = h["value"]?.ToString() ?? string.Empty;
                if (string.Equals(name, "From",       StringComparison.OrdinalIgnoreCase)) detail.From      = value;
                if (string.Equals(name, "To",         StringComparison.OrdinalIgnoreCase)) detail.To        = value;
                if (string.Equals(name, "Cc",         StringComparison.OrdinalIgnoreCase)) detail.Cc        = value;
                if (string.Equals(name, "Subject",    StringComparison.OrdinalIgnoreCase)) detail.Subject   = value;
                if (string.Equals(name, "Date",       StringComparison.OrdinalIgnoreCase)) detail.Date      = value;
                if (string.Equals(name, "Message-Id", StringComparison.OrdinalIgnoreCase)) detail.MessageId = value;
            }

            var labelIds = obj["labelIds"] as JArray;
            if (labelIds != null)
                foreach (JToken l in labelIds)
                    detail.Labels.Add(l.ToString());

            JObject payload = obj["payload"] as JObject;
            if (payload != null)
                detail.Body = ExtractBody(payload, detail.Attachments);

            return detail;
        }

        private static string ExtractBody(JObject payload, List<GmailAttachmentInfo> attachments)
        {
            string mimeType = payload["mimeType"]?.ToString() ?? string.Empty;

            // Simple message body
            if (!mimeType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
            {
                JObject body = payload["body"] as JObject;
                if (body != null)
                {
                    string attachId = body["attachmentId"]?.ToString();
                    string filename = payload["filename"]?.ToString();
                    if (!string.IsNullOrEmpty(attachId) && !string.IsNullOrEmpty(filename))
                    {
                        attachments.Add(new GmailAttachmentInfo
                        {
                            AttachmentId = attachId,
                            Filename     = filename,
                            MimeType     = mimeType,
                            Size         = body["size"]?.Value<int>() ?? 0
                        });
                        return string.Empty;
                    }

                    string data = body["data"]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(data))
                    {
                        byte[] bytes = Base64UrlDecode(data);
                        return Encoding.UTF8.GetString(bytes);
                    }
                }
                return string.Empty;
            }

            // Multipart: walk parts
            string textBody = null;
            var parts = payload["parts"] as JArray ?? new JArray();
            foreach (JToken part in parts)
            {
                string partMime = part["mimeType"]?.ToString() ?? string.Empty;
                string partFilename = part["filename"]?.ToString();

                if (!string.IsNullOrEmpty(partFilename))
                {
                    JObject partBody = part["body"] as JObject;
                    string attachId = partBody?["attachmentId"]?.ToString();
                    if (!string.IsNullOrEmpty(attachId))
                    {
                        attachments.Add(new GmailAttachmentInfo
                        {
                            AttachmentId = attachId,
                            Filename     = partFilename,
                            MimeType     = partMime,
                            Size         = partBody?["size"]?.Value<int>() ?? 0
                        });
                    }
                    continue;
                }

                if (partMime.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase) &&
                    textBody == null)
                {
                    JObject partBody = part["body"] as JObject;
                    string data = partBody?["data"]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(data))
                        textBody = Encoding.UTF8.GetString(Base64UrlDecode(data));
                }
                else if (partMime.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
                {
                    string nested = ExtractBody((JObject)part, attachments);
                    if (!string.IsNullOrEmpty(nested) && textBody == null)
                        textBody = nested;
                }
            }

            return textBody ?? string.Empty;
        }

        private static string BuildRawMessage(
            string to,
            string subject,
            string body,
            string inReplyTo,
            string references,
            string cc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("From: me");
            sb.AppendLine("To: " + to);
            if (!string.IsNullOrEmpty(cc))
                sb.AppendLine("Cc: " + cc);
            sb.AppendLine("Subject: " + subject);
            sb.AppendLine("Content-Type: text/plain; charset=UTF-8");
            if (!string.IsNullOrEmpty(inReplyTo))
                sb.AppendLine("In-Reply-To: " + inReplyTo);
            if (!string.IsNullOrEmpty(references))
                sb.AppendLine("References: " + references);
            sb.AppendLine();
            sb.Append(body);

            return Base64UrlEncode(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        internal static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        internal static byte[] Base64UrlDecode(string s)
        {
            string padded = s.Replace('-', '+').Replace('_', '/');
            int pad = padded.Length % 4;
            if (pad == 2) padded += "==";
            else if (pad == 3) padded += "=";
            return Convert.FromBase64String(padded);
        }
    }
}
