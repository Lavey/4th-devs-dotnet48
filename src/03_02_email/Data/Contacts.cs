using System.Collections.Generic;

namespace FourthDevs.Email.Data
{
    /// <summary>
    /// Contact classification: internal, trusted_vendor, client, untrusted.
    /// Maps contact types to allowed KB categories.
    /// </summary>
    public static class Contacts
    {
        // Contact type constants
        public const string Internal = "internal";
        public const string TrustedVendor = "trusted_vendor";
        public const string Client = "client";
        public const string Untrusted = "untrusted";

        private class ContactRule
        {
            public string Match { get; set; }
            public string Type { get; set; }
            public string Label { get; set; }
        }

        private static readonly Dictionary<string, List<ContactRule>> Rules =
            new Dictionary<string, List<ContactRule>>
            {
                {
                    "adam@techvolt.io", new List<ContactRule>
                    {
                        new ContactRule { Match = "@techvolt.io", Type = Internal },
                        new ContactRule { Match = "@nexon.com", Type = Client, Label = "Nexon" },
                        new ContactRule { Match = "@shopflow.de", Type = Client, Label = "ShopFlow" },
                    }
                },
                {
                    "adam@creativespark.co", new List<ContactRule>
                    {
                        new ContactRule { Match = "@creativespark.co", Type = Internal },
                        new ContactRule { Match = "@freelance.design", Type = TrustedVendor, Label = "Freelancer" },
                        new ContactRule { Match = "@aurora-events.se", Type = Client, Label = "Aurora Events" },
                    }
                },
            };

        /// <summary>
        /// Maps contact types to allowed knowledge base categories.
        /// </summary>
        public static readonly Dictionary<string, string[]> KBCategories =
            new Dictionary<string, string[]>
            {
                { Internal, new[] { "product", "clients", "team", "vendors", "communication" } },
                { TrustedVendor, new[] { "vendors", "communication" } },
                { Client, new[] { "product", "communication" } },
                { Untrusted, new[] { "communication" } },
            };

        /// <summary>
        /// Classify a sender email address relative to the receiving account.
        /// </summary>
        public static string ClassifyContact(string account, string email)
        {
            List<ContactRule> accountRules;
            if (!Rules.TryGetValue(account, out accountRules))
                return Untrusted;

            foreach (var rule in accountRules)
            {
                if (rule.Match.StartsWith("@"))
                {
                    if (email.EndsWith(rule.Match))
                        return rule.Type;
                }
                else
                {
                    if (email == rule.Match)
                        return rule.Type;
                }
            }

            return Untrusted;
        }

        /// <summary>
        /// Return the display label for a sender, or null if no matching rule.
        /// </summary>
        public static string ContactLabel(string account, string email)
        {
            List<ContactRule> accountRules;
            if (!Rules.TryGetValue(account, out accountRules))
                return null;

            foreach (var rule in accountRules)
            {
                if (rule.Match.StartsWith("@"))
                {
                    if (email.EndsWith(rule.Match))
                        return rule.Label;
                }
                else
                {
                    if (email == rule.Match)
                        return rule.Label;
                }
            }

            return null;
        }
    }
}
