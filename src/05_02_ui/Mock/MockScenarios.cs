using System;
using System.Collections.Generic;
using FourthDevs.ChatUi.Data;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Mock
{
    /// <summary>
    /// Four pre-scripted mock scenarios: sales, email, artifact, research.
    /// </summary>
    internal static class MockScenarios
    {
        public static List<DelayedEvent> Sales(string messageId)
        {
            return new MockBuilder(messageId)
                .Start()
                .ThinkingStart("Analyzing request...")
                .ThinkingDelta("The user wants sales data. ")
                .ThinkingDelta("I should fetch the sales report first, ")
                .ThinkingDelta("then generate a chart visualization.")
                .ThinkingEnd()
                .TextChunked("Let me pull up the sales data for you.\n\n")
                .ToolCall("tc_sales_1", "get_sales_report",
                    new JObject { ["quarter"] = "Q4", ["year"] = 2025 })
                .ToolResult("tc_sales_1", true, new JObject
                {
                    ["summary"] = "Q4 2025 Revenue: $431K across 5 product lines",
                    ["rows"] = MockData.ProductRows
                })
                .TextChunked("Here's the quarterly breakdown:\n\n" +
                    "| Product | Q1 | Q2 | Q3 | Q4 | Total |\n" +
                    "|---------|-----|-----|-----|-----|-------|\n" +
                    "| Widget A | $12.4K | $15.8K | $14.2K | $18.6K | $61K |\n" +
                    "| Widget B | $8.2K | $9.1K | $11.5K | $10.8K | $39.6K |\n" +
                    "| Service X | $22K | $24.5K | $26.8K | $29.1K | $102.4K |\n" +
                    "| Service Y | $5.6K | $6.2K | $7.8K | $8.4K | $28K |\n" +
                    "| Enterprise Z | $45K | $48K | $52K | $55K | $200K |\n\n" +
                    "Now let me generate a chart.\n\n")
                .ToolCall("tc_chart_1", "render_chart",
                    new JObject { ["type"] = "bar", ["title"] = "Revenue by Product" })
                .ToolResult("tc_chart_1", true, new JObject
                {
                    ["chartId"] = "chart_001",
                    ["preview"] = MockData.GetChartPreview("bar")
                })
                .TextChunked("The chart has been generated! Enterprise Z is clearly the top performer at $200K, " +
                    "followed by Service X showing strong growth at $102.4K. " +
                    "Would you like me to create a detailed report artifact?")
                .Complete()
                .Build();
        }

        public static List<DelayedEvent> Email(string messageId)
        {
            return new MockBuilder(messageId)
                .Start()
                .ThinkingStart("Processing email request...")
                .ThinkingDelta("I need to look up the contact first, ")
                .ThinkingDelta("then compose and send the email.")
                .ThinkingEnd()
                .TextChunked("I'll look up the contact information first.\n\n")
                .ToolCall("tc_contact_1", "lookup_contact_context",
                    new JObject { ["email"] = "alice@techcorp.com" })
                .ToolResult("tc_contact_1", true, MockData.GetContactContext("alice@techcorp.com"))
                .TextChunked("Found Alice Johnson — VP Engineering at TechCorp. " +
                    "Last contact was May 15. She's interested in the Enterprise Z renewal.\n\n" +
                    "Drafting the email now.\n\n")
                .ToolCall("tc_email_1", "send_email",
                    new JObject
                    {
                        ["to"] = "alice@techcorp.com",
                        ["subject"] = "Enterprise Z Renewal — Next Steps",
                        ["body"] = MockData.GetMockEmailBody("Alice", "the Enterprise Z renewal")
                    })
                .ToolResult("tc_email_1", true, new JObject
                {
                    ["sent"] = true,
                    ["messageId"] = "msg_" + Guid.NewGuid().ToString("N").Substring(0, 8)
                })
                .TextChunked("Done! I've sent the follow-up email to Alice Johnson at TechCorp " +
                    "regarding the Enterprise Z renewal. The email references your previous discussion " +
                    "and proposes scheduling a call this week.")
                .Complete()
                .Build();
        }

        public static List<DelayedEvent> ArtifactScenario(string messageId)
        {
            string artifactId = "art_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            return new MockBuilder(messageId)
                .Start()
                .ThinkingStart("Planning the report...")
                .ThinkingDelta("The user wants a comprehensive report. ")
                .ThinkingDelta("I'll gather data and create a markdown artifact.")
                .ThinkingEnd()
                .TextChunked("I'll create a comprehensive quarterly report for you.\n\n")
                .ToolCall("tc_sales_2", "get_sales_report",
                    new JObject { ["quarter"] = "all", ["year"] = 2025 })
                .ToolResult("tc_sales_2", true, new JObject
                {
                    ["summary"] = "Full Year 2025: $431K total revenue",
                    ["rows"] = MockData.ProductRows
                })
                .TextChunked("Got the data. Now creating the artifact.\n\n")
                .ToolCall("tc_artifact_1", "create_artifact",
                    new JObject
                    {
                        ["kind"] = "markdown",
                        ["title"] = "Quarterly Sales Report — 2025",
                        ["description"] = "Comprehensive revenue analysis with recommendations",
                        ["content"] = MockData.GetArtifactPreview()
                    })
                .ToolResult("tc_artifact_1", true, new JObject
                {
                    ["artifactId"] = artifactId,
                    ["path"] = "reports/quarterly-2025.md"
                })
                .Artifact(artifactId, "markdown", "Quarterly Sales Report — 2025",
                    "Comprehensive revenue analysis with recommendations",
                    "reports/quarterly-2025.md",
                    MockData.GetArtifactPreview())
                .TextChunked("I've created the quarterly report artifact. It includes:\n\n" +
                    "- **Executive summary** with key revenue figures\n" +
                    "- **Product-level breakdown** across all quarters\n" +
                    "- **Strategic recommendations** for the next quarter\n\n" +
                    "You can view the full artifact above.")
                .Complete()
                .Build();
        }

        public static List<DelayedEvent> Research(string messageId)
        {
            return new MockBuilder(messageId)
                .Start()
                .ThinkingStart("Searching notes...")
                .ThinkingDelta("I'll search through the user's notes ")
                .ThinkingDelta("for product roadmap information.")
                .ThinkingEnd()
                .TextChunked("Let me search your notes for roadmap information.\n\n")
                .ToolCall("tc_notes_1", "search_notes",
                    new JObject { ["query"] = "product roadmap" })
                .ToolResult("tc_notes_1", true, new JObject
                {
                    ["results"] = MockData.NoteSnippets
                })
                .TextChunked("Here's what I found in your notes:\n\n" +
                    "### Product Roadmap\n" +
                    "- **Widget A v2** launch is scheduled for September with real-time analytics as the key feature\n" +
                    "- **Service X** APAC expansion has approved budget for 2 additional sales reps\n\n" +
                    "### Related Notes\n" +
                    "- **Customer Feedback**: Enterprise Z users requesting API rate limit increases — consider a premium tier\n" +
                    "- **Competitive Analysis**: A competitor launched a similar service at 20% lower price — we should emphasize reliability and support\n" +
                    "- **Backend Migration**: Infrastructure migration is 80% complete, expected to finish by end of July\n\n" +
                    "Would you like me to compile these into a planning document?")
                .Complete()
                .Build();
        }

        public static List<DelayedEvent> GetScenario(int index, string messageId)
        {
            switch (index % 4)
            {
                case 0: return Sales(messageId);
                case 1: return Email(messageId);
                case 2: return ArtifactScenario(messageId);
                case 3: return Research(messageId);
                default: return Sales(messageId);
            }
        }
    }
}
