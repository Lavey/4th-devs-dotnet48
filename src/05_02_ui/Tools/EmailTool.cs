using System;
using FourthDevs.ChatUi.Data;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Tools
{
    internal static class EmailTool
    {
        public static JObject LookupContactContextDef()
        {
            return new JObject
            {
                ["type"] = "function",
                ["name"] = "lookup_contact_context",
                ["description"] = "Look up information about a contact before emailing them.",
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["email"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Email address of the contact"
                        }
                    },
                    ["required"] = new JArray("email")
                }
            };
        }

        public static JObject SendEmailDef()
        {
            return new JObject
            {
                ["type"] = "function",
                ["name"] = "send_email",
                ["description"] = "Send an email to a specified recipient.",
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["to"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Recipient email address"
                        },
                        ["subject"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Email subject line"
                        },
                        ["body"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Email body text"
                        }
                    },
                    ["required"] = new JArray("to", "subject", "body")
                }
            };
        }

        public static ToolResult LookupContactContext(JObject args)
        {
            string email = args["email"]?.ToString() ?? "";
            return new ToolResult
            {
                Ok = true,
                Output = MockData.GetContactContext(email)
            };
        }

        public static ToolResult SendEmail(JObject args)
        {
            string to = args["to"]?.ToString() ?? "unknown";
            string subject = args["subject"]?.ToString() ?? "(no subject)";
            return new ToolResult
            {
                Ok = true,
                Output = new JObject
                {
                    ["sent"] = true,
                    ["messageId"] = "msg_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    ["to"] = to,
                    ["subject"] = subject
                }
            };
        }
    }
}
