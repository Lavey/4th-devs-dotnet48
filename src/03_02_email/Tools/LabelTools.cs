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
    /// Tools for managing labels: list_labels, create_label, label_email, unlabel_email.
    /// </summary>
    public static class LabelTools
    {
        private static int _labelCounter = 0;

        public static List<ToolDef> GetTools()
        {
            return new List<ToolDef>
            {
                new ToolDef
                {
                    Name = "list_labels",
                    Description = "List all labels for a given account (both system and user-created).",
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
                        var result = MockInbox.Labels.Where(l => l.Account == account).ToList();
                        return (object)new { labels = result };
                    },
                },

                new ToolDef
                {
                    Name = "create_label",
                    Description = "Create a new user label for a given account.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""account"": { ""type"": ""string"", ""description"": ""Email address of the account"" },
                            ""name"": { ""type"": ""string"", ""description"": ""Label display name"" },
                            ""color"": { ""type"": ""string"", ""description"": ""Hex color code (optional, e.g. \""#ff6d01\"")"" }
                        },
                        ""required"": [""account"", ""name""],
                        ""additionalProperties"": false
                    }"),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        string account = args.Value<string>("account");
                        string name = args.Value<string>("name");

                        var duplicate = MockInbox.Labels.FirstOrDefault(
                            l => l.Account == account &&
                                 string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
                        if (duplicate != null)
                            return (object)new { error = $"Label \"{name}\" already exists", label = duplicate };

                        _labelCounter++;
                        var label = new Label
                        {
                            Id = $"user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{_labelCounter}",
                            Account = account,
                            Name = name,
                            Type = "user",
                            Color = args.Value<string>("color"),
                        };
                        MockInbox.Labels.Add(label);
                        return (object)new { label = label };
                    },
                },

                new ToolDef
                {
                    Name = "label_email",
                    Description = "Add a label to an email.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""email_id"": { ""type"": ""string"", ""description"": ""ID of the email"" },
                            ""label_id"": { ""type"": ""string"", ""description"": ""ID of the label to add"" }
                        },
                        ""required"": [""email_id"", ""label_id""],
                        ""additionalProperties"": false
                    }"),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        string emailId = args.Value<string>("email_id");
                        string labelId = args.Value<string>("label_id");

                        var email = MockInbox.Emails.FirstOrDefault(e => e.Id == emailId);
                        if (email == null)
                            return (object)new { error = $"Email not found: {emailId}" };

                        var label = MockInbox.Labels.FirstOrDefault(l => l.Id == labelId);
                        if (label == null)
                            return (object)new { error = $"Label not found: {labelId}" };

                        if (email.Account != label.Account)
                            return (object)new { error = "Label and email belong to different accounts" };

                        if (email.LabelIds.Contains(label.Id))
                            return (object)new { already_applied = true, email_id = email.Id, label_id = label.Id };

                        email.LabelIds.Add(label.Id);
                        return (object)new { success = true, email_id = email.Id, label_id = label.Id, label_name = label.Name };
                    },
                },

                new ToolDef
                {
                    Name = "unlabel_email",
                    Description = "Remove a label from an email.",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""email_id"": { ""type"": ""string"", ""description"": ""ID of the email"" },
                            ""label_id"": { ""type"": ""string"", ""description"": ""ID of the label to remove"" }
                        },
                        ""required"": [""email_id"", ""label_id""],
                        ""additionalProperties"": false
                    }"),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        string emailId = args.Value<string>("email_id");
                        string labelId = args.Value<string>("label_id");

                        var email = MockInbox.Emails.FirstOrDefault(e => e.Id == emailId);
                        if (email == null)
                            return (object)new { error = $"Email not found: {emailId}" };

                        int idx = email.LabelIds.IndexOf(labelId);
                        if (idx == -1)
                            return (object)new { not_applied = true, email_id = email.Id, label_id = labelId };

                        email.LabelIds.RemoveAt(idx);
                        return (object)new { success = true, email_id = email.Id, removed_label_id = labelId };
                    },
                },
            };
        }
    }
}
