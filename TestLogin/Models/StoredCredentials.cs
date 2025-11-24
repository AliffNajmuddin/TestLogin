using System;

namespace TestLogin.Models
{
    public class StoredCredentials
    {
        // Legacy username kept for backward compatibility
        public string? Username { get; set; }

        // New field: Email (keep only email; remove Name)
        public string? Email { get; set; }

        public bool RememberMe { get; set; }
        public AuthToken? Token { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastLogin { get; set; }

        // Encrypted password stored as base64 string (may be null)
        public string? EncryptedPasswordBase64 { get; set; }

        // Indicates we have an encrypted password on disk
        public bool HasEncryptedPassword => !string.IsNullOrEmpty(EncryptedPasswordBase64);
    }
}