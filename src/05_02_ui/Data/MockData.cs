using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Data
{
    /// <summary>
    /// Static mock data for tool responses and conversation seeding.
    /// </summary>
    internal static class MockData
    {
        // ---- Sales data ----
        public static readonly JArray ProductRows = JArray.Parse(@"[
            {""product"":""Widget A"",""q1"":12400,""q2"":15800,""q3"":14200,""q4"":18600,""total"":61000},
            {""product"":""Widget B"",""q1"":8200,""q2"":9100,""q3"":11500,""q4"":10800,""total"":39600},
            {""product"":""Service X"",""q1"":22000,""q2"":24500,""q3"":26800,""q4"":29100,""total"":102400},
            {""product"":""Service Y"",""q1"":5600,""q2"":6200,""q3"":7800,""q4"":8400,""total"":28000},
            {""product"":""Enterprise Z"",""q1"":45000,""q2"":48000,""q3"":52000,""q4"":55000,""total"":200000}
        ]");

        // ---- Contact context ----
        public static JObject GetContactContext(string email)
        {
            string lower = (email ?? "").ToLowerInvariant();
            if (lower.Contains("alice"))
            {
                return JObject.Parse(@"{
                    ""name"":""Alice Johnson"",
                    ""company"":""TechCorp"",
                    ""role"":""VP Engineering"",
                    ""lastContact"":""2025-05-15"",
                    ""notes"":""Interested in Enterprise Z plan. Follow up on Q3 renewal.""
                }");
            }
            if (lower.Contains("bob"))
            {
                return JObject.Parse(@"{
                    ""name"":""Bob Smith"",
                    ""company"":""DataFlow Inc"",
                    ""role"":""CTO"",
                    ""lastContact"":""2025-06-01"",
                    ""notes"":""Evaluating Widget A vs Widget B for their pipeline.""
                }");
            }
            return new JObject
            {
                ["name"] = email,
                ["company"] = "Unknown",
                ["role"] = "Unknown",
                ["lastContact"] = "N/A",
                ["notes"] = "No prior context available."
            };
        }

        // ---- Notes ----
        public static readonly JArray NoteSnippets = JArray.Parse(@"[
            {""id"":""n1"",""title"":""Q3 Planning"",""snippet"":""Focus on expanding Service X into APAC markets. Budget approved for 2 additional sales reps."",""updatedAt"":""2025-05-20""},
            {""id"":""n2"",""title"":""Product Roadmap"",""snippet"":""Widget A v2 launch scheduled for September. Key feature: real-time analytics dashboard."",""updatedAt"":""2025-06-10""},
            {""id"":""n3"",""title"":""Customer Feedback"",""snippet"":""Enterprise Z users requesting API rate limit increases. Consider premium tier."",""updatedAt"":""2025-06-15""},
            {""id"":""n4"",""title"":""Team Standup Notes"",""snippet"":""Backend migration to new infra 80% complete. Expected finish by end of July."",""updatedAt"":""2025-06-18""},
            {""id"":""n5"",""title"":""Competitive Analysis"",""snippet"":""Competitor launched similar service at 20% lower price point. Need to emphasize our reliability and support."",""updatedAt"":""2025-06-20""}
        ]");

        // ---- Chart preview (ASCII) ----
        public static string GetChartPreview(string chartType)
        {
            if (chartType == "bar")
            {
                return @"Revenue by Product (Bar Chart)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Widget A    ████████████ $61K
Widget B    ████████ $39.6K
Service X   ████████████████████ $102.4K
Service Y   ██████ $28K
Enterprise  ████████████████████████████████████████ $200K";
            }
            return @"Revenue by Product (Pie Chart)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Widget A    14.2%
Widget B     9.2%
Service X   23.8%
Service Y    6.5%
Enterprise  46.4%";
        }

        // ---- Mock content previews ----
        public static string GetArtifactPreview()
        {
            return @"# Quarterly Sales Report — 2025

## Executive Summary
Total revenue reached **$431K** across all product lines, representing a **12% increase** from the previous year.

## Highlights
- **Enterprise Z** continues to dominate at $200K (46.4% of revenue)
- **Service X** shows strongest growth trajectory (+32% YoY)
- **Widget A** v2 launch expected to boost Q3/Q4 numbers

## Recommendations
1. Prioritize Enterprise Z renewals
2. Invest in Service X APAC expansion
3. Accelerate Widget A v2 timeline";
        }

        // ---- Mock email content ----
        public static string GetMockEmailBody(string recipientName, string topic)
        {
            return string.Format(
                "Hi {0},\n\nI wanted to follow up on our discussion about {1}. " +
                "I've attached the latest figures for your review.\n\n" +
                "Would you be available for a call this week to discuss next steps?\n\n" +
                "Best regards",
                recipientName ?? "there",
                topic ?? "our collaboration");
        }

        // ---- Seed messages ----
        public static readonly string[] SeedPrompts = new string[]
        {
            "Show me last quarter's sales data and create a chart",
            "Draft an email to alice@techcorp.com about the Enterprise Z renewal",
            "Create a comprehensive quarterly report as an artifact",
            "Search my notes for anything about the product roadmap"
        };
    }
}
