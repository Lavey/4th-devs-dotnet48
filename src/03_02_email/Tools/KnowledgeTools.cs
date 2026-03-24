using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Email.Data;
using FourthDevs.Email.Knowledge;
using FourthDevs.Email.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Email.Tools
{
    /// <summary>
    /// Tools for searching/browsing the knowledge base: search_knowledge, get_knowledge_entry, list_knowledge.
    /// All tools enforce account isolation and log accesses.
    /// </summary>
    public static class KnowledgeTools
    {
        public static List<ToolDef> GetTools()
        {
            return new List<ToolDef>
            {
                new ToolDef
                {
                    Name = "search_knowledge",
                    Description =
                        "Search the knowledge base. Returns entries matching the query. " +
                        "Automatically includes shared entries plus entries scoped to the given account. " +
                        "Entries belonging to other accounts are not accessible.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""account"": {
                                ""type"": ""string"",
                                ""description"": ""Email address of the account — used to scope results (shared + account-specific)""
                            },
                            ""query"": {
                                ""type"": ""string"",
                                ""description"": ""Search query (case-insensitive, matches against title, category, and content)""
                            }
                        },
                        ""required"": [""account"", ""query""],
                        ""additionalProperties"": false
                    }"),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        string account = args.Value<string>("account");
                        AccessLock.AssertAccountAccess(account);
                        string q = args.Value<string>("query").ToLowerInvariant();

                        var allMatching = KnowledgeBase.Entries
                            .Where(e => e.Title.ToLowerInvariant().Contains(q) ||
                                        e.Category.ToLowerInvariant().Contains(q) ||
                                        e.Content.ToLowerInvariant().Contains(q))
                            .ToList();

                        var visible = allMatching
                            .Where(e => e.Account == "shared" || e.Account == account)
                            .ToList();
                        var blocked = allMatching
                            .Where(e => e.Account != "shared" && e.Account != account)
                            .ToList();

                        AccessLog.Log.Add(new KnowledgeAccess
                        {
                            Tool = "search_knowledge",
                            Account = account,
                            Query = args.Value<string>("query"),
                            Returned = visible.Select(e => new KBReturnedEntry { Id = e.Id, Title = e.Title, Scope = e.Account }).ToList(),
                            Blocked = blocked.Select(e => new KBBlockedEntry { Id = e.Id, Title = e.Title, Owner = e.Account }).ToList(),
                        });

                        var result = new JObject
                        {
                            ["total"] = visible.Count,
                            ["filtered_by_isolation"] = blocked.Count,
                            ["entries"] = JArray.FromObject(visible.Select(e => new
                            {
                                id = e.Id,
                                account = e.Account,
                                title = e.Title,
                                category = e.Category,
                                content = e.Content,
                                updatedAt = e.UpdatedAt,
                            }).ToList()),
                        };

                        if (blocked.Count > 0)
                        {
                            result["isolation_notice"] =
                                $"{blocked.Count} entries matched but belong to other accounts and were filtered out.";
                        }

                        return (object)result;
                    },
                },

                new ToolDef
                {
                    Name = "get_knowledge_entry",
                    Description =
                        "Get a single knowledge base entry by ID. Requires the account context — " +
                        "only shared entries and entries belonging to the given account are accessible.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""account"": { ""type"": ""string"", ""description"": ""Email address of the account requesting access"" },
                            ""entry_id"": { ""type"": ""string"", ""description"": ""ID of the knowledge base entry"" }
                        },
                        ""required"": [""account"", ""entry_id""],
                        ""additionalProperties"": false
                    }"),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        string account = args.Value<string>("account");
                        AccessLock.AssertAccountAccess(account);
                        string entryId = args.Value<string>("entry_id");

                        var entry = KnowledgeBase.Entries.FirstOrDefault(e => e.Id == entryId);

                        if (entry == null)
                        {
                            AccessLog.Log.Add(new KnowledgeAccess
                            {
                                Tool = "get_knowledge_entry",
                                Account = account,
                                Returned = new List<KBReturnedEntry>(),
                                Blocked = new List<KBBlockedEntry>(),
                            });
                            return (object)new { error = $"Entry not found: {entryId}" };
                        }

                        if (entry.Account != "shared" && entry.Account != account)
                        {
                            AccessLog.Log.Add(new KnowledgeAccess
                            {
                                Tool = "get_knowledge_entry",
                                Account = account,
                                Returned = new List<KBReturnedEntry>(),
                                Blocked = new List<KBBlockedEntry>
                                {
                                    new KBBlockedEntry { Id = entry.Id, Title = entry.Title, Owner = entry.Account }
                                },
                            });
                            return (object)new
                            {
                                error = "ACCESS_DENIED",
                                message = $"Entry \"{entry.Title}\" belongs to account {entry.Account} and cannot be accessed from {account}. Account data isolation is enforced.",
                            };
                        }

                        AccessLog.Log.Add(new KnowledgeAccess
                        {
                            Tool = "get_knowledge_entry",
                            Account = account,
                            Returned = new List<KBReturnedEntry>
                            {
                                new KBReturnedEntry { Id = entry.Id, Title = entry.Title, Scope = entry.Account }
                            },
                            Blocked = new List<KBBlockedEntry>(),
                        });
                        return (object)entry;
                    },
                },

                new ToolDef
                {
                    Name = "list_knowledge",
                    Description =
                        "List all knowledge base entries visible to an account (shared + account-scoped). Returns titles and categories, not full content.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""account"": { ""type"": ""string"", ""description"": ""Email address of the account"" },
                            ""category"": { ""type"": ""string"", ""description"": ""Filter by category (optional)"" }
                        },
                        ""required"": [""account""],
                        ""additionalProperties"": false
                    }"),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        string account = args.Value<string>("account");
                        AccessLock.AssertAccountAccess(account);

                        var visible = KnowledgeBase.Entries
                            .Where(e => e.Account == "shared" || e.Account == account)
                            .ToList();
                        int hiddenCount = KnowledgeBase.Entries
                            .Count(e => e.Account != "shared" && e.Account != account);

                        string category = args.Value<string>("category");
                        if (category != null)
                        {
                            visible = visible.Where(e => e.Category == category).ToList();
                        }

                        AccessLog.Log.Add(new KnowledgeAccess
                        {
                            Tool = "list_knowledge",
                            Account = account,
                            Returned = visible.Select(e => new KBReturnedEntry { Id = e.Id, Title = e.Title, Scope = e.Account }).ToList(),
                            Blocked = KnowledgeBase.Entries
                                .Where(e => e.Account != "shared" && e.Account != account)
                                .Select(e => new KBBlockedEntry { Id = e.Id, Title = e.Title, Owner = e.Account })
                                .ToList(),
                        });

                        return (object)new
                        {
                            total = visible.Count,
                            filtered_by_isolation = hiddenCount,
                            entries = visible.Select(e => new
                            {
                                id = e.Id,
                                account = e.Account,
                                title = e.Title,
                                category = e.Category,
                                updatedAt = e.UpdatedAt,
                            }).ToList(),
                        };
                    },
                },
            };
        }
    }
}
