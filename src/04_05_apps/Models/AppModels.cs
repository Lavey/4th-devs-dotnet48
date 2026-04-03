using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.McpApps.Models
{
    // ── Todo ──

    public class TodoItem
    {
        [JsonProperty("id")]   public string Id   { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("done")] public bool   Done { get; set; }
    }

    public class TodosState
    {
        [JsonProperty("items")]     public List<TodoItem> Items     { get; set; } = new List<TodoItem>();
        [JsonProperty("updatedAt")] public string         UpdatedAt { get; set; }
    }

    // ── Stripe ──

    public class Product
    {
        [JsonProperty("id")]          public string       Id          { get; set; }
        [JsonProperty("name")]        public string       Name        { get; set; }
        [JsonProperty("description")] public string       Description { get; set; }
        [JsonProperty("price")]       public long         Price       { get; set; }
        [JsonProperty("currency")]    public string       Currency    { get; set; }
        [JsonProperty("interval")]    public string       Interval    { get; set; }
        [JsonProperty("active")]      public bool         Active      { get; set; }
        [JsonProperty("features")]    public List<string> Features    { get; set; } = new List<string>();
        [JsonProperty("createdAt")]   public string       CreatedAt   { get; set; }
    }

    public class Coupon
    {
        [JsonProperty("id")]              public string Id              { get; set; }
        [JsonProperty("code")]            public string Code            { get; set; }
        [JsonProperty("percentOff")]      public int    PercentOff      { get; set; }
        [JsonProperty("productId")]       public string ProductId       { get; set; }
        [JsonProperty("campaignId")]      public string CampaignId      { get; set; }
        [JsonProperty("maxRedemptions")]  public int    MaxRedemptions  { get; set; }
        [JsonProperty("timesRedeemed")]   public int    TimesRedeemed   { get; set; }
        [JsonProperty("active")]          public bool   Active          { get; set; }
        [JsonProperty("expiresAt")]       public string ExpiresAt       { get; set; }
        [JsonProperty("createdAt")]       public string CreatedAt       { get; set; }
    }

    public class DailySales
    {
        [JsonProperty("date")]         public string           Date        { get; set; }
        [JsonProperty("prod_starter")] public SalesEntry       ProdStarter { get; set; }
        [JsonProperty("prod_growth")]  public SalesEntry       ProdGrowth  { get; set; }
    }

    public class SalesEntry
    {
        [JsonProperty("sales")]   public int  Sales   { get; set; }
        [JsonProperty("revenue")] public long Revenue { get; set; }
    }

    public class SalesPeriod
    {
        [JsonProperty("from")] public string From { get; set; }
        [JsonProperty("to")]   public string To   { get; set; }
    }

    public class SalesData
    {
        [JsonProperty("generatedAt")] public string          GeneratedAt { get; set; }
        [JsonProperty("period")]      public SalesPeriod     Period      { get; set; }
        [JsonProperty("daily")]       public List<DailySales> Daily      { get; set; } = new List<DailySales>();
        [JsonProperty("totals")]      public Dictionary<string, SalesEntry> Totals { get; set; } = new Dictionary<string, SalesEntry>();
    }

    // ── Newsletter ──

    public class Campaign
    {
        [JsonProperty("id")]               public string Id               { get; set; }
        [JsonProperty("name")]             public string Name             { get; set; }
        [JsonProperty("subject")]          public string Subject          { get; set; }
        [JsonProperty("status")]           public string Status           { get; set; }
        [JsonProperty("sentAt")]           public string SentAt           { get; set; }
        [JsonProperty("scheduledAt")]      public string ScheduledAt      { get; set; }
        [JsonProperty("audience")]         public int    Audience         { get; set; }
        [JsonProperty("delivered")]        public int    Delivered        { get; set; }
        [JsonProperty("opened")]           public int    Opened           { get; set; }
        [JsonProperty("clicked")]          public int    Clicked          { get; set; }
        [JsonProperty("conversions")]      public int    Conversions      { get; set; }
        [JsonProperty("revenue")]          public long   Revenue          { get; set; }
        [JsonProperty("couponCode")]       public string CouponCode       { get; set; }
        [JsonProperty("productHighlight")] public string ProductHighlight { get; set; }
        [JsonProperty("summary")]          public string Summary          { get; set; }
    }

    public class CampaignComparison
    {
        [JsonProperty("left")]    public Campaign Left    { get; set; }
        [JsonProperty("right")]   public Campaign Right   { get; set; }
        [JsonProperty("summary")] public string   Summary { get; set; }
    }

    // ── Agent turn result ──

    public class ToolExecution
    {
        [JsonProperty("toolName")]   public string ToolName   { get; set; }
        [JsonProperty("toolArgs")]   public object ToolArgs   { get; set; }
        [JsonProperty("toolResult")] public object ToolResult { get; set; }
    }

    public class AgentTurnResult
    {
        [JsonProperty("text")]           public string              Text           { get; set; }
        [JsonProperty("toolExecutions")] public List<ToolExecution> ToolExecutions { get; set; } = new List<ToolExecution>();
        [JsonProperty("mode")]           public string              Mode           { get; set; }
    }
}
