using System;
using System.Collections.Generic;
using System.Linq;
using FourthDevs.McpApps.Models;
using FourthDevs.McpApps.Store;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.McpApps.Core
{
    internal sealed class ToolDef
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject Parameters { get; set; }
        public Func<JObject, ToolCallResult> Handler { get; set; }
    }

    internal sealed class ToolCallResult
    {
        public string Text { get; set; }
        public object Structured { get; set; }
    }

    internal static class ToolRegistry
    {
        private static readonly List<ToolDef> _tools = new List<ToolDef>();

        static ToolRegistry()
        {
            RegisterTodoTools();
            RegisterStripeTools();
            RegisterNewsletterTools();
        }

        public static IReadOnlyList<ToolDef> All { get { return _tools; } }

        public static ToolDef Find(string name)
        {
            return _tools.FirstOrDefault(t => t.Name == name);
        }

        public static JArray GetDefinitionsForApi()
        {
            var arr = new JArray();
            foreach (var t in _tools)
            {
                arr.Add(new JObject
                {
                    ["type"] = "function",
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = t.Parameters ?? new JObject { ["type"] = "object", ["properties"] = new JObject() },
                    ["strict"] = false
                });
            }
            return arr;
        }

        // ── Todo ──

        private static void RegisterTodoTools()
        {
            Add("open_todo_board", "Open the interactive todo board.", null, args =>
            {
                var state = TodoStore.ReadState();
                return new ToolCallResult { Text = "Opened todo board. " + TodoStore.Summarize(state), Structured = state };
            });
            Add("list_todos", "List current todos with ids and status.", null, args =>
            {
                var state = TodoStore.ReadState();
                return new ToolCallResult { Text = "Todos: " + TodoStore.Summarize(state), Structured = state };
            });
            Add("add_todo", "Add a new todo item.", Props(P("text", "string", "Todo text.")), args =>
            {
                var item = TodoStore.AddTodo(args["text"]?.ToString() ?? "");
                var state = TodoStore.ReadState();
                return new ToolCallResult { Text = "Added " + item.Id + ": " + item.Text, Structured = state };
            });
            Add("complete_todo", "Mark a todo as done by id or text.", Props(P("target", "string", "Todo id or text fragment.")), args =>
            {
                var item = TodoStore.CompleteTodo(args["target"]?.ToString() ?? "");
                var state = TodoStore.ReadState();
                return new ToolCallResult { Text = "Completed " + item.Id + ": " + item.Text, Structured = state };
            });
            Add("reopen_todo", "Reopen a completed todo.", Props(P("target", "string", "Todo id or text fragment.")), args =>
            {
                var item = TodoStore.ReopenTodo(args["target"]?.ToString() ?? "");
                var state = TodoStore.ReadState();
                return new ToolCallResult { Text = "Reopened " + item.Id + ": " + item.Text, Structured = state };
            });
            Add("remove_todo", "Remove a todo by id or text.", Props(P("target", "string", "Todo id or text fragment.")), args =>
            {
                var item = TodoStore.RemoveTodo(args["target"]?.ToString() ?? "");
                var state = TodoStore.ReadState();
                return new ToolCallResult { Text = "Removed " + item.Id + ": " + item.Text, Structured = state };
            });
        }

        // ── Stripe ──

        private static void RegisterStripeTools()
        {
            Add("open_stripe_dashboard", "Open the product catalog with pricing.", null, args =>
                new ToolCallResult { Text = "Opened product catalog.\n" + StripeStore.SummarizeProducts(), Structured = StripeStore.ReadProducts() });

            Add("list_products", "List all products with pricing.", null, args =>
                new ToolCallResult { Text = "Products:\n" + StripeStore.SummarizeProducts(), Structured = StripeStore.ReadProducts() });

            Add("update_product", "Update a product's name, description, price, active status, or features.",
                Props(P("product_id", "string", "Product ID."), P("name", "string", "New name.", true), P("description", "string", "New desc.", true),
                      P("price", "integer", "New price in cents.", true), P("active", "boolean", "Active.", true)), args =>
            {
                string pid = args["product_id"]?.ToString() ?? "";
                var updates = new JObject();
                foreach (var key in new[] { "name", "description", "price", "active", "features" })
                    if (args[key] != null) updates[key] = args[key];
                var product = StripeStore.UpdateProduct(pid, updates);
                return new ToolCallResult { Text = "Updated " + product.Id + ": " + product.Name, Structured = StripeStore.ReadProducts() };
            });

            Add("open_sales_analytics", "Open scoped sales analytics. Supports date range and product filtering.",
                Props(P("from", "string", "Start date YYYY-MM-DD.", true), P("to", "string", "End date.", true), P("product_id", "string", "Product filter.", true)), args =>
            {
                string from = args["from"]?.ToString();
                string to = args["to"]?.ToString();
                string pid = args["product_id"]?.ToString();
                var data = StripeStore.ReadSalesFiltered(from, to, pid);
                return new ToolCallResult { Text = "Sales data loaded.", Structured = data };
            });

            Add("get_sales_report", "Get 30-day sales and revenue data.", null, args =>
                new ToolCallResult { Text = "Sales report:\n" + StripeStore.SummarizeSales(), Structured = StripeStore.ReadSalesRaw() });

            Add("open_coupon_manager", "Open the interactive coupon manager.",
                Props(P("active", "boolean", "Filter by active.", true), P("product_id", "string", "Filter product.", true)), args =>
            {
                bool? active = args["active"] != null ? (bool?)args["active"].Value<bool>() : null;
                string pid = args["product_id"]?.ToString();
                var coupons = StripeStore.ReadCouponsFiltered(active, pid);
                return new ToolCallResult { Text = coupons.Count + " coupons.", Structured = coupons };
            });

            Add("list_coupons", "List all coupon codes.", null, args =>
                new ToolCallResult { Text = "Coupons:\n" + StripeStore.SummarizeCoupons(), Structured = StripeStore.ReadCoupons() });

            Add("create_coupon", "Create a new discount coupon.",
                Props(P("code", "string", "Coupon code."), P("percent_off", "integer", "Discount %."),
                      P("product_id", "string", "Product.", true), P("campaign_id", "string", "Campaign.", true),
                      P("max_redemptions", "integer", "Max uses.", true)), args =>
            {
                var coupon = StripeStore.CreateCoupon(
                    args["code"]?.ToString() ?? "", args["percent_off"]?.Value<int>() ?? 10,
                    args["product_id"]?.ToString(), args["campaign_id"]?.ToString(),
                    args["max_redemptions"]?.Value<int>() ?? 100);
                return new ToolCallResult { Text = "Created coupon " + coupon.Code + ": " + coupon.PercentOff + "% off.", Structured = StripeStore.ReadCoupons() };
            });

            Add("deactivate_coupon", "Deactivate a coupon by code or id.", Props(P("code", "string", "Coupon code or id.")), args =>
            {
                var coupon = StripeStore.DeactivateCoupon(args["code"]?.ToString() ?? "");
                return new ToolCallResult { Text = "Deactivated coupon " + coupon.Code + ".", Structured = StripeStore.ReadCoupons() };
            });
        }

        // ── Newsletter ──

        private static void RegisterNewsletterTools()
        {
            Add("open_newsletter_dashboard", "Open the newsletter campaigns overview dashboard.", null, args =>
                new ToolCallResult { Text = "Campaigns:\n" + NewsletterStore.SummarizeCampaigns(), Structured = NewsletterStore.ReadCampaigns() });

            Add("list_campaigns", "List all newsletter campaigns with key metrics.", null, args =>
                new ToolCallResult { Text = "Campaigns:\n" + NewsletterStore.SummarizeCampaigns(), Structured = NewsletterStore.ReadCampaigns() });

            Add("get_campaign_report", "Get detailed report for a specific campaign.",
                Props(P("campaign", "string", "Campaign name or id.")), args =>
            {
                var c = NewsletterStore.FindCampaign(args["campaign"]?.ToString() ?? "");
                if (c == null) throw new Exception("Campaign not found.");
                return new ToolCallResult { Text = NewsletterStore.FormatCampaignReport(c), Structured = c };
            });

            Add("compare_campaigns", "Compare two campaigns side by side.",
                Props(P("left", "string", "First campaign."), P("right", "string", "Second campaign.")), args =>
            {
                var result = NewsletterStore.CompareCampaigns(args["left"]?.ToString() ?? "", args["right"]?.ToString() ?? "");
                return new ToolCallResult { Text = result.Summary, Structured = result };
            });
        }

        // ── Helpers ──

        private static void Add(string name, string desc, JObject parameters, Func<JObject, ToolCallResult> handler)
        {
            _tools.Add(new ToolDef { Name = name, Description = desc, Parameters = parameters ?? new JObject { ["type"] = "object", ["properties"] = new JObject() }, Handler = handler });
        }

        private static JObject Props(params JProperty[] props)
        {
            var obj = new JObject { ["type"] = "object" };
            var properties = new JObject();
            foreach (var p in props) properties.Add(p);
            obj["properties"] = properties;
            return obj;
        }

        private static JProperty P(string name, string type, string desc, bool optional = false)
        {
            var val = new JObject { ["type"] = type, ["description"] = desc };
            return new JProperty(name, val);
        }
    }
}
