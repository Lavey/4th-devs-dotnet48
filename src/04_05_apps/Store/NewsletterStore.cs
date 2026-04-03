using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FourthDevs.McpApps.Models;
using Newtonsoft.Json;

namespace FourthDevs.McpApps.Store
{
    internal static class NewsletterStore
    {
        private static string CampaignsPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace", "newsletters", "campaigns.json"); }
        }

        public static List<Campaign> ReadCampaigns()
        {
            return JsonConvert.DeserializeObject<List<Campaign>>(File.ReadAllText(CampaignsPath, Encoding.UTF8));
        }

        public static Campaign FindCampaign(string idOrName)
        {
            var campaigns = ReadCampaigns();
            string needle = idOrName.Trim().ToLowerInvariant();
            return campaigns.FirstOrDefault(c =>
                c.Id.ToLowerInvariant() == needle || c.Name.ToLowerInvariant() == needle);
        }

        public static string SummarizeCampaigns()
        {
            var campaigns = ReadCampaigns();
            if (campaigns.Count == 0) return "No campaigns sent yet.";
            return string.Join("\n", campaigns.Select(c =>
            {
                string timeline = GetTimelineLabel(c);
                string openRate = c.Delivered > 0 ? Pct(c.Opened, c.Delivered) : "0%";
                string ctr = c.Delivered > 0 ? Pct(c.Clicked, c.Delivered) : "0%";
                return string.Format("{0} ({1}): {2} — {3} open, {4} CTR, {5} conversions, {6} revenue",
                    c.Name, c.Id, timeline, openRate, ctr, c.Conversions, FormatCents(c.Revenue));
            }));
        }

        public static string FormatCampaignReport(Campaign c)
        {
            var lines = new List<string>
            {
                "Campaign: " + c.Name,
                "Subject: " + c.Subject
            };
            if (c.Status == "sent")
            {
                lines.Add(string.Format("Sent: {0} to {1:N0} subscribers", FormatDate(c.SentAt), c.Audience));
                lines.Add(string.Format("Delivered: {0:N0} ({1})", c.Delivered, Pct(c.Delivered, c.Audience)));
                lines.Add(string.Format("Opened: {0:N0} ({1})", c.Opened, Pct(c.Opened, c.Delivered)));
                lines.Add(string.Format("Clicked: {0:N0} ({1})", c.Clicked, Pct(c.Clicked, c.Delivered)));
                lines.Add("Conversions: " + c.Conversions);
                lines.Add("Revenue: " + FormatCents(c.Revenue));
            }
            else if (c.Status == "scheduled")
                lines.Add(string.Format("Scheduled: {0} for {1:N0} subscribers", FormatDate(c.ScheduledAt), c.Audience));
            else
                lines.Add(string.Format("Status: Draft for {0:N0} subscribers", c.Audience));

            if (!string.IsNullOrEmpty(c.CouponCode)) lines.Add("Coupon: " + c.CouponCode);
            if (!string.IsNullOrEmpty(c.Summary)) lines.Add("Summary: " + c.Summary);
            return string.Join("\n", lines);
        }

        public static CampaignComparison CompareCampaigns(string leftName, string rightName)
        {
            var left = FindCampaign(leftName);
            var right = FindCampaign(rightName);
            if (left == null) throw new Exception("Campaign not found: " + leftName);
            if (right == null) throw new Exception("Campaign not found: " + rightName);

            string summary = string.Join("\n", new[]
            {
                left.Name + " vs " + right.Name,
                string.Format("Open rate: {0} vs {1}", Pct(left.Opened, left.Delivered), Pct(right.Opened, right.Delivered)),
                string.Format("Click rate: {0} vs {1}", Pct(left.Clicked, left.Delivered), Pct(right.Clicked, right.Delivered)),
                string.Format("Conversions: {0} vs {1}", left.Conversions, right.Conversions),
                string.Format("Revenue: {0} vs {1}", FormatCents(left.Revenue), FormatCents(right.Revenue))
            });

            return new CampaignComparison { Left = left, Right = right, Summary = summary };
        }

        // ── helpers ──

        private static string GetTimelineLabel(Campaign c)
        {
            if (c.Status == "sent") return "sent " + FormatDate(c.SentAt);
            if (c.Status == "scheduled") return "scheduled " + FormatDate(c.ScheduledAt);
            return "draft";
        }

        private static string Pct(int numerator, int denominator)
        {
            if (denominator <= 0) return "0%";
            return ((double)numerator / denominator * 100).ToString("F1") + "%";
        }

        private static string FormatCents(long cents)
        {
            return "$" + (cents / 100.0).ToString("F2");
        }

        private static string FormatDate(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "unknown";
            DateTime dt;
            if (DateTime.TryParse(iso, out dt)) return dt.ToString("yyyy-MM-dd");
            return iso;
        }
    }
}
