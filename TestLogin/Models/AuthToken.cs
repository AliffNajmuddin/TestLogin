using System;
using System.Text.Json.Serialization;

namespace TestLogin.Models
{
    public class AuthToken
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        // New: API may return a welcome/message string alongside the token
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        public bool IsValid()
        {
            if (string.IsNullOrEmpty(Token)) return false;
            return ExpiresAt == null || ExpiresAt > DateTime.UtcNow;
        }

        public override string ToString() => Token ?? string.Empty;
    }
}