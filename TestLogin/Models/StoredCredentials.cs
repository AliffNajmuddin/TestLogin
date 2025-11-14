using System;

namespace TestLogin.Models
{
    public class StoredCredentials
    {
        public string Username { get; set; }
        public string EncryptedPassword { get; set; }
        public AuthToken Token { get; set; }
        public DateTime LastLogin { get; set; }
        public bool RememberMe { get; set; }
    }
}