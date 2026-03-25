using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Calendar.Data;
using FourthDevs.Calendar.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Calendar.Tools
{
    public static class ContactTools
    {
        private static int ScoreContact(Contact contact, string query)
        {
            string q = query.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q)) return 0;

            var parts = new List<string> { contact.Name, contact.Email };
            if (!string.IsNullOrEmpty(contact.Company)) parts.Add(contact.Company);
            if (!string.IsNullOrEmpty(contact.Role)) parts.Add(contact.Role);
            if (!string.IsNullOrEmpty(contact.Notes)) parts.Add(contact.Notes);
            if (contact.Preferences != null) parts.AddRange(contact.Preferences);

            string haystack = string.Join(" ", parts).ToLowerInvariant();

            if (haystack.Contains(q)) return 100;

            string[] tokens = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return 0;

            return tokens.Sum(token => haystack.Contains(token) ? 10 : 0);
        }

        public static List<LocalToolDefinition> GetTools()
        {
            return new List<LocalToolDefinition>
            {
                new LocalToolDefinition
                {
                    Name = "search_contacts",
                    Description = "Search contacts by name, email, company, role, notes, and preferences.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Free-text query, e.g. \"Kasia\", \"Tomek Brandt\", \"investor\"" },
                            limit = new { type = "number", description = "Maximum number of contacts to return (default 5)" },
                        },
                        required = new[] { "query" },
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        string query = args["query"]?.Value<string>();
                        if (string.IsNullOrWhiteSpace(query))
                            return new { error = "query is required and must be a non-empty string" };

                        int limit = args["limit"] != null && args["limit"].Type == JTokenType.Integer
                            ? Math.Max(1, args["limit"].Value<int>()) : 5;

                        var ranked = ContactStore.Contacts
                            .Select(c => new { Contact = c, Score = ScoreContact(c, query) })
                            .Where(x => x.Score > 0)
                            .OrderByDescending(x => x.Score)
                            .Take(limit)
                            .Select(x => x.Contact)
                            .ToList();

                        return new { total = ranked.Count, contacts = ranked };
                    },
                },

                new LocalToolDefinition
                {
                    Name = "get_contact",
                    Description = "Get a contact by exact contact ID.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            contact_id = new { type = "string", description = "Contact ID, e.g. c-001" },
                        },
                        required = new[] { "contact_id" },
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        string contactId = args["contact_id"]?.Value<string>();
                        if (string.IsNullOrEmpty(contactId))
                            return new { error = "contact_id is required and must be a string" };

                        var contact = ContactStore.Contacts.FirstOrDefault(c => c.Id == contactId);
                        if (contact == null) return new { error = "Contact not found: " + contactId };
                        return contact;
                    },
                },
            };
        }
    }
}
