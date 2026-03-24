using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Email.Data;
using FourthDevs.Email.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Email.Tools
{
    /// <summary>
    /// Tools for reading and searching emails: list_emails, get_email, search_emails, list_threads.
    /// </summary>
    public static class EmailTools
    {
        public static List<ToolDef> GetTools()
        {
            return new List<ToolDef>
            {
                new ToolDef
                {
                    Name = "list_emails",
                    Description = "List emails from a given account. Supports filtering by label, read status, and pagination.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""account"": { ""type"": ""string"", ""description"": ""Email address of the account to query"" },
                            ""label"": { ""type"": ""string"", ""description"": ""Filter by label ID (optional)"" },
                            ""is_read"": { ""type"": ""boolean"", ""description"": ""Filter by read status (optional)"" },
                            ""limit"": { ""type"": ""number"", ""description"": ""Max results (default 20)"" },
                            ""offset"": { ""type"": ""number"", ""description"": ""Skip N results (default 0)"" }
                        },
                        ""required"": [""account""],
                        ""additionalProperties"": false
                    }"),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        string account = args.Value<string>("account");
                        var result = MockInbox.Emails.Where(e => e.Account == account).ToList();

                        string label = args.Value<string>("label");
                        if (label != null)
                        {
                            result = result.Where(e => e.LabelIds.Contains(label)).ToList();
                        }

                        var isReadToken = args["is_read"];
                        if (isReadToken != null && isReadToken.Type == JTokenType.Boolean)
                        {
                            bool isRead = isReadToken.Value<bool>();
                            result = result.Where(e => e.IsRead == isRead).ToList();
                        }

                        result = result.OrderByDescending(e => DateTime.Parse(e.Date)).ToList();

                        int offset = args["offset"] != null ? args.Value<int>("offset") : 0;
                        int limit = args["limit"] != null ? args.Value<int>("limit") : 20;

                        var emailSlice = result.Skip(offset).Take(limit).Select(e => new
                        {
                            id = e.Id,
                            threadId = e.ThreadId,
                            from = e.From,
                            to = e.To,
                            subject = e.Subject,
                            snippet = e.Snippet,
                            date = e.Date,
                            labelIds = e.LabelIds,
                            isRead = e.IsRead,
                            hasAttachments = e.HasAttachments,
                        }).ToList();

                        return (object)new { total = result.Count, emails = emailSlice };
                    },
                },

                new ToolDef
                {
                    Name = "get_email",
                    Description = "Get full email content by ID, including the body.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""email_id"": { ""type"": ""string"", ""description"": ""ID of the email to retrieve"" }
                        },
                        ""required"": [""email_id""],
                        ""additionalProperties"": false
                    }"),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        string emailId = args.Value<string>("email_id");
                        var email = MockInbox.Emails.FirstOrDefault(e => e.Id == emailId);
                        if (email == null)
                            return (object)new { error = $"Email not found: {emailId}" };
                        return (object)email;
                    },
                },

                new ToolDef
                {
                    Name = "search_emails",
                    Description = "Search emails by query string across subject and body. Scoped to a single account.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""account"": { ""type"": ""string"", ""description"": ""Email address of the account to search"" },
                            ""query"": { ""type"": ""string"", ""description"": ""Search query (case-insensitive substring match)"" },
                            ""limit"": { ""type"": ""number"", ""description"": ""Max results (default 10)"" }
                        },
                        ""required"": [""account"", ""query""],
                        ""additionalProperties"": false
                    }"),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        string account = args.Value<string>("account");
                        string q = args.Value<string>("query").ToLowerInvariant();
                        int limit = args["limit"] != null ? args.Value<int>("limit") : 10;

                        var results = MockInbox.Emails
                            .Where(e => e.Account == account &&
                                        (e.Subject.ToLowerInvariant().Contains(q) ||
                                         e.Body.ToLowerInvariant().Contains(q)))
                            .OrderByDescending(e => DateTime.Parse(e.Date))
                            .Take(limit)
                            .ToList();

                        return (object)new { total = results.Count, emails = results };
                    },
                },

                new ToolDef
                {
                    Name = "list_threads",
                    Description = "List email threads for a given account. Groups messages by threadId, returns summaries.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""account"": { ""type"": ""string"", ""description"": ""Email address of the account"" }
                        },
                        ""required"": [""account""],
                        ""additionalProperties"": false
                    }"),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        string account = args.Value<string>("account");
                        var accountEmails = MockInbox.Emails.Where(e => e.Account == account).ToList();

                        var threadMap = new Dictionary<string, List<Models.Email>>();
                        foreach (var email in accountEmails)
                        {
                            List<Models.Email> list;
                            if (!threadMap.TryGetValue(email.ThreadId, out list))
                            {
                                list = new List<Models.Email>();
                                threadMap[email.ThreadId] = list;
                            }
                            list.Add(email);
                        }

                        var threads = threadMap
                            .Select(kv =>
                            {
                                var sorted = kv.Value.OrderBy(m => DateTime.Parse(m.Date)).ToList();
                                var allLabels = sorted.SelectMany(m => m.LabelIds).Distinct().ToList();
                                var participants = sorted
                                    .SelectMany(m => new[] { m.From }.Concat(m.To))
                                    .Distinct()
                                    .ToList();
                                var subject = sorted[0].Subject;
                                if (subject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase))
                                    subject = subject.Substring(4);

                                return new
                                {
                                    threadId = kv.Key,
                                    subject = subject,
                                    messageCount = sorted.Count,
                                    participants = participants,
                                    lastMessageDate = sorted[sorted.Count - 1].Date,
                                    labelIds = allLabels,
                                    hasUnread = sorted.Any(m => !m.IsRead),
                                };
                            })
                            .OrderByDescending(t => DateTime.Parse(t.lastMessageDate))
                            .ToList();

                        return (object)new { total = threads.Count, threads = threads };
                    },
                },
            };
        }
    }
}
