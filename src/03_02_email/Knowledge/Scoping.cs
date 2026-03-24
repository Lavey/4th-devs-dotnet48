using System.Collections.Generic;
using System.Linq;
using FourthDevs.Email.Data;
using FourthDevs.Email.Models;

namespace FourthDevs.Email.Knowledge
{
    /// <summary>
    /// Result of scoping knowledge base entries by account + contact type.
    /// </summary>
    public class ScopedKBResult
    {
        public List<ScopedKBLoaded> Loaded { get; set; } = new List<ScopedKBLoaded>();
        public List<KBBlockedInfo> Blocked { get; set; } = new List<KBBlockedInfo>();
    }

    public class ScopedKBLoaded
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Content { get; set; }
    }

    /// <summary>
    /// Scopes knowledge base entries by account and contact type.
    /// Only entries whose category is in the allowed set for the contact type are loaded.
    /// Entries from other accounts are always blocked.
    /// </summary>
    public static class Scoping
    {
        public static ScopedKBResult GetScopedKnowledge(string account, string contactType)
        {
            AccessLock.AssertAccountAccess(account);

            string[] allowedCats;
            if (!Contacts.KBCategories.TryGetValue(contactType, out allowedCats))
            {
                allowedCats = new string[0];
            }
            var allowedSet = new HashSet<string>(allowedCats);

            var accountEntries = KnowledgeBase.Entries
                .Where(e => e.Account == "shared" || e.Account == account)
                .ToList();

            var loaded = new List<ScopedKBLoaded>();
            var blocked = new List<KBBlockedInfo>();

            foreach (var entry in accountEntries)
            {
                if (allowedSet.Contains(entry.Category))
                {
                    loaded.Add(new ScopedKBLoaded
                    {
                        Id = entry.Id,
                        Title = entry.Title,
                        Category = entry.Category,
                        Content = entry.Content,
                    });
                }
                else
                {
                    blocked.Add(new KBBlockedInfo
                    {
                        Title = entry.Title,
                        Category = entry.Category,
                        Reason = $"Category \"{entry.Category}\" not permitted for contact type \"{contactType}\"",
                    });
                }
            }

            // Block entries from other accounts
            var otherAccountEntries = KnowledgeBase.Entries
                .Where(e => e.Account != "shared" && e.Account != account)
                .ToList();

            foreach (var entry in otherAccountEntries)
            {
                blocked.Add(new KBBlockedInfo
                {
                    Title = entry.Title,
                    Category = entry.Category,
                    Reason = $"Belongs to {entry.Account} — account isolation",
                });
            }

            return new ScopedKBResult { Loaded = loaded, Blocked = blocked };
        }
    }
}
