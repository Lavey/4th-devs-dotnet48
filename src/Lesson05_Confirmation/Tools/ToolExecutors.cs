using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson05_Confirmation.Tools
{
    /// <summary>
    /// Implements the runtime execution of all built-in confirmation-agent tools.
    /// Mirrors 01_05_confirmation/src/native/tools.js (nativeHandlers)
    /// and 01_05_confirmation/src/native/resend.js in the source repo.
    /// </summary>
    internal static class ToolExecutors
    {
        // Injected at startup by Program
        internal static string WorkspaceRoot  { get; set; }
        internal static string WhitelistPath  { get; set; }
        internal static string ResendApiKey   { get; set; }
        internal static string ResendFrom     { get; set; }

        // ----------------------------------------------------------------
        // File tools
        // ----------------------------------------------------------------

        internal static object ExecuteListFiles(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? ".";
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };
            if (!Directory.Exists(absPath)) return new { error = "Directory not found: " + rel };

            var entries = new List<object>();
            foreach (string d in Directory.GetDirectories(absPath))
                entries.Add(new { type = "directory", name = Path.GetFileName(d) });
            foreach (string f in Directory.GetFiles(absPath))
                entries.Add(new { type = "file", name = Path.GetFileName(f) });

            return new { path = rel, entries };
        }

        internal static object ExecuteReadFile(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? string.Empty;
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };
            if (!File.Exists(absPath)) return new { error = "File not found: " + rel };

            string content = File.ReadAllText(absPath, Encoding.UTF8);
            return new { path = rel, content };
        }

        internal static object ExecuteWriteFile(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? string.Empty;
            string content = args["content"]?.ToString() ?? string.Empty;
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };

            string dir = Path.GetDirectoryName(absPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(absPath, content, Encoding.UTF8);
            return new { success = true, path = rel, bytesWritten = Encoding.UTF8.GetByteCount(content) };
        }

        internal static object ExecuteSearchFiles(JObject args)
        {
            string pattern = args["pattern"]?.ToString() ?? "*";
            var matches    = new List<string>();

            foreach (string f in Directory.GetFiles(WorkspaceRoot, pattern, SearchOption.AllDirectories))
            {
                string rel = f.Substring(WorkspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                matches.Add(rel);
            }

            return new { pattern, count = matches.Count, files = matches };
        }

        // ----------------------------------------------------------------
        // Email tool (mirrors src/native/resend.js + send_email handler)
        // ----------------------------------------------------------------

        internal static async Task<object> ExecuteSendEmailAsync(JObject args)
        {
            // 1. Load whitelist
            var whitelist = LoadWhitelist();

            // 2. Validate recipients
            var toToken    = args["to"];
            var recipients = new List<string>();

            if (toToken != null)
            {
                if (toToken.Type == JTokenType.Array)
                {
                    foreach (JToken t in toToken)
                        recipients.Add(t.ToString());
                }
                else
                {
                    recipients.Add(toToken.ToString());
                }
            }

            if (recipients.Count == 0)
                return new { success = false, error = "No recipients specified." };

            var blocked = recipients.Where(r => !IsEmailAllowed(r, whitelist)).ToList();
            if (blocked.Count > 0)
            {
                return new
                {
                    success = false,
                    error   = "Recipients not in whitelist: " + string.Join(", ", blocked) +
                              ". Update workspace/whitelist.json to allow them."
                };
            }

            // 3. Check Resend configuration
            if (string.IsNullOrWhiteSpace(ResendApiKey))
            {
                return new
                {
                    success = false,
                    error   = "RESEND_API_KEY is not configured. Set it in App.config."
                };
            }

            if (string.IsNullOrWhiteSpace(ResendFrom))
            {
                return new
                {
                    success = false,
                    error   = "RESEND_FROM is not configured. Set a verified sender address in App.config."
                };
            }

            // 4. Build email
            string subject = args["subject"]?.ToString() ?? "(no subject)";
            string body    = args["body"]?.ToString()    ?? string.Empty;
            string format  = args["format"]?.ToString()  ?? "text";
            string replyTo = args["reply_to"]?.ToString();

            bool   isHtml   = string.Equals(format, "html", StringComparison.OrdinalIgnoreCase);
            string htmlBody = isHtml ? body : TextToHtml(body);
            string textBody = isHtml ? StripHtml(body) : body;

            var payload = new JObject
            {
                ["from"]    = ResendFrom,
                ["to"]      = JArray.FromObject(recipients),
                ["subject"] = subject,
                ["html"]    = htmlBody,
                ["text"]    = textBody
            };

            if (!string.IsNullOrEmpty(replyTo))
                payload["reply_to"] = replyTo;

            // 5. Send via Resend API
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", ResendApiKey);

                using (var reqContent = new StringContent(
                    payload.ToString(Formatting.None), Encoding.UTF8, "application/json"))
                using (var resp = await http.PostAsync("https://api.resend.com/emails", reqContent))
                {
                    string respBody = await resp.Content.ReadAsStringAsync();

                    if (!resp.IsSuccessStatusCode)
                    {
                        return new
                        {
                            success = false,
                            error   = string.Format("Resend API error {0}: {1}",
                                (int)resp.StatusCode, respBody)
                        };
                    }

                    var result = JObject.Parse(respBody);
                    string id  = result["id"]?.ToString() ?? "unknown";

                    return new
                    {
                        success = true,
                        id,
                        to      = recipients,
                        subject
                    };
                }
            }
        }

        // ----------------------------------------------------------------
        // Whitelist helpers (mirrors isEmailAllowed / loadWhitelist)
        // ----------------------------------------------------------------

        private static List<string> LoadWhitelist()
        {
            try
            {
                if (!File.Exists(WhitelistPath)) return new List<string>();
                string json = File.ReadAllText(WhitelistPath, Encoding.UTF8);
                var    obj  = JObject.Parse(json);
                var    arr  = obj["allowed_recipients"] as JArray;
                return arr == null
                    ? new List<string>()
                    : arr.Select(t => t.ToString()).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static bool IsEmailAllowed(string email, List<string> whitelist)
        {
            string normalized = email.ToLowerInvariant();
            string domain     = normalized.Contains("@")
                ? "@" + normalized.Split('@')[1]
                : string.Empty;

            foreach (string pattern in whitelist)
            {
                string p = pattern.ToLowerInvariant();
                if (p.StartsWith("@"))
                {
                    if (domain == p) return true;
                }
                else
                {
                    if (normalized == p) return true;
                }
            }

            return false;
        }

        // ----------------------------------------------------------------
        // Text helpers
        // ----------------------------------------------------------------

        private static string TextToHtml(string text)
        {
            string escaped = text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\n", "<br>\n");
            return "<div style=\"font-family: sans-serif; line-height: 1.6; color: #333;\">"
                   + escaped + "</div>";
        }

        private static string StripHtml(string html)
        {
            return Regex.Replace(html, "<[^>]+>", string.Empty);
        }

        // ----------------------------------------------------------------
        // Workspace path guard
        // ----------------------------------------------------------------

        internal static string ResolveWorkspacePath(string relativePath)
        {
            string full = Path.GetFullPath(
                Path.Combine(WorkspaceRoot, relativePath));

            return full.StartsWith(WorkspaceRoot + Path.DirectorySeparatorChar,
                                   StringComparison.OrdinalIgnoreCase)
                   || string.Equals(full, WorkspaceRoot, StringComparison.OrdinalIgnoreCase)
                ? full
                : null;
        }
    }
}
