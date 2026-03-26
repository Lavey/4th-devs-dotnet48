using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Gmail.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Gmail.Gmail
{
    internal static class GmailAuth
    {
        private static readonly string TokenPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "workspace", "auth", "gmail-token.json");

        private static string ClientId =>
            ConfigurationManager.AppSettings["GOOGLE_CLIENT_ID"]?.Trim() ?? string.Empty;

        private static string ClientSecret =>
            ConfigurationManager.AppSettings["GOOGLE_CLIENT_SECRET"]?.Trim() ?? string.Empty;

        private static string RedirectUri =>
            ConfigurationManager.AppSettings["GOOGLE_REDIRECT_URI"]?.Trim()
            ?? "http://localhost:8080";

        private const string Scope = "https://www.googleapis.com/auth/gmail.modify";

        public static void RunAuthFlow()
        {
            if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
                throw new InvalidOperationException(
                    "GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET must be set in App.config.");

            string authUrl =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                "?client_id="     + Uri.EscapeDataString(ClientId) +
                "&redirect_uri="  + Uri.EscapeDataString(RedirectUri) +
                "&response_type=code" +
                "&scope="         + Uri.EscapeDataString(Scope) +
                "&access_type=offline" +
                "&prompt=consent";

            Console.WriteLine("Opening browser for Gmail OAuth consent...");
            Console.WriteLine("Auth URL: " + authUrl);

            OpenBrowser(authUrl);

            string code = WaitForAuthCode();
            if (string.IsNullOrEmpty(code))
                throw new InvalidOperationException("No authorization code received.");

            Console.WriteLine("Authorization code received. Exchanging for tokens...");

            GmailToken token = ExchangeCodeForTokenAsync(code).GetAwaiter().GetResult();
            SaveToken(token);

            Console.WriteLine("Token saved to: " + TokenPath);
        }

        public static GmailToken LoadToken()
        {
            if (!File.Exists(TokenPath))
                return null;

            string json = File.ReadAllText(TokenPath);
            GmailToken token;
            try { token = JsonConvert.DeserializeObject<GmailToken>(json); }
            catch { return null; }

            if (token == null) return null;

            if (token.IsExpired())
            {
                Console.WriteLine("[auth] Token expired, refreshing...");
                token = RefreshTokenAsync(token).GetAwaiter().GetResult();
                SaveToken(token);
            }

            return token;
        }

        public static string GetValidAccessToken()
        {
            GmailToken token = LoadToken();
            if (token == null)
                throw new InvalidOperationException(
                    "No Gmail token found. Run with 'auth' argument to authenticate.");
            return token.AccessToken;
        }

        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

        private static string WaitForAuthCode()
        {
            // Parse the port from the redirect URI
            Uri redirectUri = new Uri(RedirectUri);
            string prefix = RedirectUri.TrimEnd('/') + "/";

            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(prefix);
                listener.Start();

                Console.WriteLine("Waiting for OAuth callback on " + prefix + " ...");

                HttpListenerContext ctx = listener.GetContext();
                HttpListenerRequest req = ctx.Request;

                string responseHtml =
                    "<html><body><h2>Authentication complete. You may close this tab.</h2></body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                ctx.Response.ContentLength64 = buffer.Length;
                ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                ctx.Response.OutputStream.Close();

                string code = req.QueryString["code"];
                return code;
            }
        }

        private static async Task<GmailToken> ExchangeCodeForTokenAsync(string code)
        {
            using (var http = new HttpClient())
            {
                var body = new FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("code",          code),
                    new System.Collections.Generic.KeyValuePair<string, string>("client_id",     ClientId),
                    new System.Collections.Generic.KeyValuePair<string, string>("client_secret", ClientSecret),
                    new System.Collections.Generic.KeyValuePair<string, string>("redirect_uri",  RedirectUri),
                    new System.Collections.Generic.KeyValuePair<string, string>("grant_type",    "authorization_code")
                });

                using (var response = await http.PostAsync("https://oauth2.googleapis.com/token", body))
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Token exchange failed: " + json);

                    return ParseTokenResponse(json);
                }
            }
        }

        private static async Task<GmailToken> RefreshTokenAsync(GmailToken existing)
        {
            if (string.IsNullOrWhiteSpace(existing.RefreshToken))
                throw new InvalidOperationException(
                    "No refresh token available. Re-run auth flow.");

            using (var http = new HttpClient())
            {
                var body = new FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("refresh_token", existing.RefreshToken),
                    new System.Collections.Generic.KeyValuePair<string, string>("client_id",     ClientId),
                    new System.Collections.Generic.KeyValuePair<string, string>("client_secret", ClientSecret),
                    new System.Collections.Generic.KeyValuePair<string, string>("grant_type",    "refresh_token")
                });

                using (var response = await http.PostAsync("https://oauth2.googleapis.com/token", body))
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Token refresh failed: " + json);

                    GmailToken refreshed = ParseTokenResponse(json);
                    // Preserve the refresh token (Google may not re-issue it)
                    if (string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                        refreshed.RefreshToken = existing.RefreshToken;

                    return refreshed;
                }
            }
        }

        private static GmailToken ParseTokenResponse(string json)
        {
            JObject obj = JObject.Parse(json);
            int expiresIn = obj["expires_in"]?.Value<int>() ?? 3600;
            return new GmailToken
            {
                AccessToken  = obj["access_token"]?.ToString(),
                RefreshToken = obj["refresh_token"]?.ToString(),
                TokenType    = obj["token_type"]?.ToString() ?? "Bearer",
                ExpiresIn    = expiresIn,
                ExpiryTime   = DateTime.UtcNow.AddSeconds(expiresIn)
            };
        }

        private static void SaveToken(GmailToken token)
        {
            string dir = Path.GetDirectoryName(TokenPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(TokenPath, JsonConvert.SerializeObject(token, Formatting.Indented));
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // On Linux the above may not work; try xdg-open
                try { Process.Start("xdg-open", url); }
                catch { /* ignore */ }
            }
        }
    }
}
