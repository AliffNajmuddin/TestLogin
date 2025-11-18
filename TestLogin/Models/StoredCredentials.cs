using System;

namespace TestLogin.Models
{
    public class StoredCredentials
    {
        // Non-sensitive data
        public string? Username { get; set; }
        public bool RememberMe { get; set; }

        // Encrypted password stored as base64 (protected with DPAPI)
        public string? EncryptedPasswordBase64 { get; set; }

        // Optional token/cached session info (now typed)
        public AuthToken? Token { get; set; }
        public DateTime? ExpiresAt { get; set; }

        // When the user last logged in
        public DateTime? LastLogin { get; set; }

        public bool HasEncryptedPassword => !string.IsNullOrEmpty(EncryptedPasswordBase64);
    }
}