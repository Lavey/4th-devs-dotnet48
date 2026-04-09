using Newtonsoft.Json;

namespace FourthDevs.MultiAgentApi.Models
{
    // -------------------------------------------------------------------------
    // Account domain models
    // -------------------------------------------------------------------------

    internal class Account
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }
    }

    internal class ApiKey
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("account_id")]
        public string AccountId { get; set; }

        [JsonProperty("tenant_id")]
        public string TenantId { get; set; }

        [JsonProperty("key_prefix")]
        public string KeyPrefix { get; set; }

        [JsonProperty("key_hash")]
        public string KeyHash { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("scopes")]
        public string Scopes { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }

        [JsonProperty("last_used_at")]
        public string LastUsedAt { get; set; }

        [JsonProperty("revoked_at")]
        public string RevokedAt { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    internal class AuthSession
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("account_id")]
        public string AccountId { get; set; }

        [JsonProperty("tenant_id")]
        public string TenantId { get; set; }

        [JsonProperty("token_hash")]
        public string TokenHash { get; set; }

        [JsonProperty("ip_address")]
        public string IpAddress { get; set; }

        [JsonProperty("user_agent")]
        public string UserAgent { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    internal class PasswordCredential
    {
        [JsonProperty("account_id")]
        public string AccountId { get; set; }

        [JsonProperty("password_hash")]
        public string PasswordHash { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }
    }

    internal class Tenant
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }
    }

    internal class TenantMembership
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("tenant_id")]
        public string TenantId { get; set; }

        [JsonProperty("account_id")]
        public string AccountId { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }
}
