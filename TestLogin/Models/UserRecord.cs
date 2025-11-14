using System;

namespace TestLogin.Models
{
    // Simple model used for the mock user store (CSV)
    public class UserRecord
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // plain-text for mock data only
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
    }
}