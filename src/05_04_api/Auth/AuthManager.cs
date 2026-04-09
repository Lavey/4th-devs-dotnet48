using System;
using System.Security.Cryptography;
using System.Text;
using FourthDevs.MultiAgentApi.Db;
using FourthDevs.MultiAgentApi.Models;

namespace FourthDevs.MultiAgentApi.Auth
{
    /// <summary>
    /// Handles API key validation, session management, and password hashing.
    /// Supports both API key auth (Bearer sk_local_...) and session cookie auth.
    /// In dev_headers mode, trusts X-Account-Id and X-Tenant-Id headers directly.
    /// </summary>
    internal sealed class AuthManager
    {
        private readonly DatabaseManager _db;
        private readonly string _authMode;

        internal AuthManager(DatabaseManager db, string authMode)
        {
            _db = db;
            _authMode = authMode ?? "dev_headers";
        }

        /// <summary>
        /// Resolves the current user context from the request.
        /// Returns null if authentication fails.
        /// </summary>
        internal AuthContext Authenticate(string authorizationHeader, string cookieHeader,
            string xAccountId, string xTenantId)
        {
            // Dev headers mode: trust headers directly
            if (_authMode == "dev_headers")
            {
                string accountId = !string.IsNullOrWhiteSpace(xAccountId) ? xAccountId : "acc_seed_admin";
                string tenantId = !string.IsNullOrWhiteSpace(xTenantId) ? xTenantId : "ten_seed_default";

                var account = _db.GetAccountById(accountId);
                if (account == null) return null;

                return new AuthContext
                {
                    AccountId = accountId,
                    TenantId = tenantId,
                    Account = account
                };
            }

            // API key auth: Bearer sk_...
            if (!string.IsNullOrWhiteSpace(authorizationHeader) &&
                authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                string token = authorizationHeader.Substring(7).Trim();
                if (token.StartsWith("sk_", StringComparison.Ordinal))
                {
                    return AuthenticateApiKey(token, xTenantId);
                }

                // Session token auth
                return AuthenticateSessionToken(token, xTenantId);
            }

            // Cookie-based session auth
            if (!string.IsNullOrWhiteSpace(cookieHeader))
            {
                string sessionToken = ExtractCookieValue(cookieHeader, "session_token");
                if (!string.IsNullOrWhiteSpace(sessionToken))
                {
                    return AuthenticateSessionToken(sessionToken, xTenantId);
                }
            }

            return null;
        }

        private AuthContext AuthenticateApiKey(string apiKey, string xTenantId)
        {
            string keyHash = HashToken(apiKey);
            var key = _db.GetApiKeyByHash(keyHash);
            if (key == null) return null;

            // Check expiration
            if (!string.IsNullOrWhiteSpace(key.ExpiresAt))
            {
                DateTime expires;
                if (DateTime.TryParse(key.ExpiresAt, out expires) && expires < DateTime.UtcNow)
                    return null;
            }

            var account = _db.GetAccountById(key.AccountId);
            if (account == null) return null;

            string tenantId = !string.IsNullOrWhiteSpace(xTenantId) ? xTenantId : key.TenantId;

            _db.UpdateApiKeyLastUsed(key.Id);

            return new AuthContext
            {
                AccountId = key.AccountId,
                TenantId = tenantId,
                Account = account
            };
        }

        private AuthContext AuthenticateSessionToken(string token, string xTenantId)
        {
            string tokenHash = HashToken(token);
            var session = _db.GetAuthSessionByToken(tokenHash);
            if (session == null) return null;

            // Check expiration
            DateTime expires;
            if (DateTime.TryParse(session.ExpiresAt, out expires) && expires < DateTime.UtcNow)
            {
                _db.DeleteAuthSession(session.Id);
                return null;
            }

            var account = _db.GetAccountById(session.AccountId);
            if (account == null) return null;

            string tenantId = !string.IsNullOrWhiteSpace(xTenantId) ? xTenantId : session.TenantId;

            return new AuthContext
            {
                AccountId = session.AccountId,
                TenantId = tenantId,
                Account = account,
                SessionId = session.Id
            };
        }

        /// <summary>
        /// Creates a new auth session and returns the token.
        /// </summary>
        internal string CreateSession(string accountId, string tenantId, string ipAddress, string userAgent)
        {
            string token = "sess_" + Guid.NewGuid().ToString("N");
            string tokenHash = HashToken(token);
            string now = DatabaseManager.UtcNow();
            string expiresAt = DateTime.UtcNow.AddDays(30).ToString("o");

            var session = new AuthSession
            {
                Id = Guid.NewGuid().ToString("N"),
                AccountId = accountId,
                TenantId = tenantId,
                TokenHash = tokenHash,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                ExpiresAt = expiresAt,
                CreatedAt = now
            };

            _db.InsertAuthSession(session);
            return token;
        }

        /// <summary>
        /// Validates password against stored credential.
        /// </summary>
        internal bool ValidatePassword(string accountId, string password)
        {
            var credential = _db.GetPasswordCredential(accountId);
            if (credential == null) return false;
            return VerifyPassword(password, credential.PasswordHash);
        }

        /// <summary>
        /// Hash a password using PBKDF2 (Rfc2898DeriveBytes).
        /// Format: iterations:saltBase64:hashBase64
        /// </summary>
        public static string HashPassword(string password)
        {
            int iterations = 10000;
            byte[] salt = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                byte[] hash = pbkdf2.GetBytes(32);
                return string.Format("{0}:{1}:{2}",
                    iterations,
                    Convert.ToBase64String(salt),
                    Convert.ToBase64String(hash));
            }
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            string[] parts = storedHash.Split(':');
            if (parts.Length != 3) return false;

            int iterations;
            if (!int.TryParse(parts[0], out iterations)) return false;

            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expectedHash = Convert.FromBase64String(parts[2]);

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                byte[] actualHash = pbkdf2.GetBytes(32);
                return SlowEquals(expectedHash, actualHash);
            }
        }

        /// <summary>
        /// Hash a token (API key or session token) using SHA-256.
        /// </summary>
        public static string HashToken(string token)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
                return Convert.ToBase64String(hash);
            }
        }

        private static bool SlowEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static string ExtractCookieValue(string cookieHeader, string name)
        {
            if (string.IsNullOrWhiteSpace(cookieHeader)) return null;
            string prefix = name + "=";
            foreach (string part in cookieHeader.Split(';'))
            {
                string trimmed = part.Trim();
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring(prefix.Length);
                }
            }
            return null;
        }
    }

    internal class AuthContext
    {
        public string AccountId { get; set; }
        public string TenantId { get; set; }
        public Account Account { get; set; }
        public string SessionId { get; set; }
    }
}
