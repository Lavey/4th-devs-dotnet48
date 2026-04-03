using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FourthDevs.McpApps.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.McpApps.Store
{
    internal static class StripeStore
    {
        private static string StripeDir
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace", "stripe"); }
        }

        private static string ProductsPath { get { return Path.Combine(StripeDir, "products.json"); } }
        private static string CouponsPath  { get { return Path.Combine(StripeDir, "coupons.json"); } }
        private static string SalesPath    { get { return Path.Combine(StripeDir, "sales.json"); } }

        // ── Read ──

        public static List<Product> ReadProducts()
        {
            return JsonConvert.DeserializeObject<List<Product>>(File.ReadAllText(ProductsPath, Encoding.UTF8));
        }

        public static List<Coupon> ReadCoupons()
        {
            return JsonConvert.DeserializeObject<List<Coupon>>(File.ReadAllText(CouponsPath, Encoding.UTF8));
        }

        public static JObject ReadSalesRaw()
        {
            return JObject.Parse(File.ReadAllText(SalesPath, Encoding.UTF8));
        }

        // ── Summaries ──

        public static string SummarizeProducts()
        {
            var products = ReadProducts();
            return string.Join("\n", products.Select(p =>
                string.Format("{0}: {1} — {2}/{3} ({4})", p.Id, p.Name, FormatCents(p.Price), p.Interval, p.Active ? "active" : "inactive")));
        }

        public static string SummarizeCoupons()
        {
            var coupons = ReadCoupons();
            if (coupons.Count == 0) return "No coupons.";
            return string.Join("\n", coupons.Select(c =>
                string.Format("{0}: {1}% off{2} — {3} ({4}/{5} used)", c.Code, c.PercentOff,
                    c.ProductId != null ? " on " + c.ProductId : "",
                    c.Active ? "active" : "inactive", c.TimesRedeemed, c.MaxRedemptions)));
        }

        public static string SummarizeSales()
        {
            var raw = ReadSalesRaw();
            string from = raw["period"]?["from"]?.ToString() ?? "";
            string to = raw["period"]?["to"]?.ToString() ?? "";
            var totals = raw["totals"] as JObject;
            long starterRev = totals?["prod_starter"]?["revenue"]?.Value<long>() ?? 0;
            int starterSales = totals?["prod_starter"]?["sales"]?.Value<int>() ?? 0;
            long growthRev = totals?["prod_growth"]?["revenue"]?.Value<long>() ?? 0;
            int growthSales = totals?["prod_growth"]?["sales"]?.Value<int>() ?? 0;
            return string.Format("{0} to {1}: Starter {2} sales ({3}), Growth {4} sales ({5}). Total: {6}.",
                from, to, starterSales, FormatCents(starterRev), growthSales, FormatCents(growthRev),
                FormatCents(starterRev + growthRev));
        }

        // ── Mutations ──

        public static Product UpdateProduct(string productId, JObject updates)
        {
            var products = ReadProducts();
            var target = products.FirstOrDefault(p => p.Id == productId);
            if (target == null) throw new Exception("Product not found: " + productId);
            if (updates["name"] != null) target.Name = updates["name"].ToString();
            if (updates["description"] != null) target.Description = updates["description"].ToString();
            if (updates["price"] != null) target.Price = updates["price"].Value<long>();
            if (updates["active"] != null) target.Active = updates["active"].Value<bool>();
            if (updates["features"] != null) target.Features = updates["features"].ToObject<List<string>>();
            File.WriteAllText(ProductsPath, JsonConvert.SerializeObject(products, Formatting.Indented), Encoding.UTF8);
            return target;
        }

        public static Coupon CreateCoupon(string code, int percentOff, string productId, string campaignId, int maxRedemptions)
        {
            var coupons = ReadCoupons();
            string normalized = System.Text.RegularExpressions.Regex.Replace(code.ToUpperInvariant(), "[^A-Z0-9]", "");
            if (string.IsNullOrEmpty(normalized)) throw new Exception("Coupon code must be non-empty alphanumeric.");
            if (coupons.Any(c => c.Code == normalized)) throw new Exception("Coupon code already exists: " + normalized);
            if (percentOff < 1 || percentOff > 100) throw new Exception("percentOff must be between 1 and 100.");
            var coupon = new Coupon
            {
                Id = "cpn_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Code = normalized,
                PercentOff = percentOff,
                ProductId = productId,
                CampaignId = campaignId,
                MaxRedemptions = maxRedemptions > 0 ? maxRedemptions : 100,
                TimesRedeemed = 0,
                Active = true,
                ExpiresAt = DateTime.UtcNow.AddDays(30).ToString("o"),
                CreatedAt = DateTime.UtcNow.ToString("o")
            };
            coupons.Add(coupon);
            File.WriteAllText(CouponsPath, JsonConvert.SerializeObject(coupons, Formatting.Indented), Encoding.UTF8);
            return coupon;
        }

        public static Coupon DeactivateCoupon(string codeOrId)
        {
            var coupons = ReadCoupons();
            string needle = codeOrId.Trim().ToUpperInvariant();
            var target = coupons.FirstOrDefault(c => c.Code == needle || c.Id == codeOrId);
            if (target == null) throw new Exception("Coupon not found: " + codeOrId);
            target.Active = false;
            File.WriteAllText(CouponsPath, JsonConvert.SerializeObject(coupons, Formatting.Indented), Encoding.UTF8);
            return target;
        }

        // ── Filtering ──

        public static JObject ReadSalesFiltered(string from, string to, string productId)
        {
            var raw = ReadSalesRaw();
            var daily = raw["daily"] as JArray ?? new JArray();
            var filtered = new JArray();
            foreach (JToken day in daily)
            {
                string date = day["date"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(from) && string.Compare(date, from, StringComparison.Ordinal) < 0) continue;
                if (!string.IsNullOrEmpty(to) && string.Compare(date, to, StringComparison.Ordinal) > 0) continue;
                filtered.Add(day);
            }

            string[] keys = string.IsNullOrEmpty(productId) ? new[] { "prod_starter", "prod_growth" } : new[] { productId };
            var totals = new JObject();
            foreach (string key in keys)
            {
                long rev = 0; int sales = 0;
                foreach (JToken day in filtered)
                {
                    var entry = day[key];
                    if (entry == null) continue;
                    sales += entry["sales"]?.Value<int>() ?? 0;
                    rev += entry["revenue"]?.Value<long>() ?? 0;
                }
                totals[key] = new JObject { ["sales"] = sales, ["revenue"] = rev };
            }

            return new JObject
            {
                ["period"] = new JObject
                {
                    ["from"] = from ?? raw["period"]?["from"]?.ToString(),
                    ["to"] = to ?? raw["period"]?["to"]?.ToString()
                },
                ["daily"] = filtered,
                ["totals"] = totals
            };
        }

        public static List<Coupon> ReadCouponsFiltered(bool? active, string productId)
        {
            var coupons = ReadCoupons();
            if (active.HasValue) coupons = coupons.Where(c => c.Active == active.Value).ToList();
            if (!string.IsNullOrEmpty(productId)) coupons = coupons.Where(c => c.ProductId == productId || c.ProductId == null).ToList();
            return coupons;
        }

        private static string FormatCents(long cents)
        {
            return "$" + (cents / 100.0).ToString("F2");
        }
    }
}
