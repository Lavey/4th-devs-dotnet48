using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace FourthDevs.VoiceAgent.Core
{
    /// <summary>
    /// Generates LiveKit-compatible JWT access tokens.
    /// The token format uses standard JWT with a "video" claim containing
    /// room grants (roomJoin, room, canPublish, canSubscribe, canPublishData).
    /// </summary>
    internal static class TokenGenerator
    {
        public static string GenerateToken(string apiKey, string apiSecret, string identity, string room)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("LiveKit API key is required.", "apiKey");
            if (string.IsNullOrWhiteSpace(apiSecret))
                throw new ArgumentException("LiveKit API secret is required.", "apiSecret");
            if (string.IsNullOrWhiteSpace(identity))
                throw new ArgumentException("Identity is required.", "identity");
            if (string.IsNullOrWhiteSpace(room))
                throw new ArgumentException("Room name is required.", "room");

            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(10);

            var videoGrant = new Dictionary<string, object>
            {
                ["roomJoin"] = true,
                ["room"] = room,
                ["canPublish"] = true,
                ["canSubscribe"] = true,
                ["canPublishData"] = true
            };

            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(apiSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, identity),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("video", JsonConvert.SerializeObject(videoGrant), JsonClaimValueTypes.Json)
            };

            var token = new JwtSecurityToken(
                issuer: apiKey,
                notBefore: now,
                expires: expires,
                claims: claims,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
