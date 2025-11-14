using System;

namespace TestLogin.Models
{
    public class AuthToken
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string TokenType { get; set; } = "Bearer";

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsValid => !string.IsNullOrEmpty(AccessToken) && !IsExpired;
    }
}