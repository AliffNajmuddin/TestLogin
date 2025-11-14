using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using TestLogin.Models;  // Add this line
using TestLogin.Services; // for LocalStorageService

namespace TestLogin.Services
{
    public static class AuthenticationService
    {
        private static AuthToken _currentToken;
        private static UserInfo _currentUser;

        public static bool IsAuthenticated => _currentToken?.IsValid == true;
        public static UserInfo CurrentUser => _currentUser;
        public static AuthToken CurrentToken => _currentToken;

        // Events to notify about authentication state changes
        public static event EventHandler<UserInfo> UserLoggedIn;
        public static event EventHandler UserLoggedOut;

        public static async Task<bool> LoginAsync(string username, string password, bool rememberMe = false)
        {
            try
            {
                // First validate against mock user store (users.csv)
                var user = LocalStorageService.ValidateCredentials(username, password);
                if (user == null)
                {
                    MessageBox.Show("Invalid credentials. Please try again.", "Login Failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                // Simulate token creation (demo)
                await Task.Delay(500);
                var token = new AuthToken
                {
                    AccessToken = GenerateDemoToken(username),
                    RefreshToken = GenerateDemoRefreshToken(username),
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    TokenType = "Bearer"
                };

                if (token != null && token.IsValid)
                {
                    _currentToken = token;
                    _currentUser = user;

                    // Store credentials if remember me is checked
                    if (rememberMe)
                    {
                        var storedCredentials = new StoredCredentials
                        {
                            Username = username,
                            EncryptedPassword = password, // Will be encrypted by storage service
                            Token = token,
                            LastLogin = DateTime.UtcNow,
                            RememberMe = true
                        };
                        LocalStorageService.SaveCredentials(storedCredentials);
                    }

                    UserLoggedIn?.Invoke(null, _currentUser);
                    return true;
                }

                MessageBox.Show("Invalid credentials. Please try again.", "Login Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login error: {ex.Message}", "Authentication Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static bool TryAutoLogin()
        {
            try
            {
                var storedCredentials = LocalStorageService.LoadCredentials();

                if (storedCredentials?.Token?.IsValid == true && storedCredentials.RememberMe)
                {
                    _currentToken = storedCredentials.Token;
                    _currentUser = new UserInfo
                    {
                        Username = storedCredentials.Username,
                        Email = $"{storedCredentials.Username}@example.com",
                        FullName = $"{storedCredentials.Username} User",
                        Role = "User"
                    };

                    UserLoggedIn?.Invoke(null, _currentUser);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Auto-login failed: {ex.Message}", "Authentication Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        public static void Logout()
        {
            var wasAuthenticated = IsAuthenticated;
            var username = _currentUser?.Username;

            _currentToken = null;
            _currentUser = null;
            LocalStorageService.ClearCredentials();

            if (wasAuthenticated)
            {
                UserLoggedOut?.Invoke(null, EventArgs.Empty);
                MessageBox.Show($"Goodbye, {username}!\n\nYou have been successfully logged out.",
                    "Logged Out", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public static async Task<bool> RefreshTokenAsync()
        {
            if (_currentToken == null || string.IsNullOrEmpty(_currentToken.RefreshToken))
                return false;

            try
            {
                // Simulate token refresh - replace with your actual refresh endpoint
                var newToken = await RefreshTokenWithServer(_currentToken.RefreshToken);

                if (newToken?.IsValid == true)
                {
                    _currentToken = newToken;

                    // Update stored credentials
                    var storedCredentials = LocalStorageService.LoadCredentials();
                    if (storedCredentials != null)
                    {
                        storedCredentials.Token = newToken;
                        storedCredentials.LastLogin = DateTime.UtcNow;
                        LocalStorageService.SaveCredentials(storedCredentials);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Token refresh failed: {ex.Message}", "Authentication Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        private static async Task<AuthToken> RefreshTokenWithServer(string refreshToken)
        {
            // Simulate API delay
            await Task.Delay(500);

            // Extract username from refresh token (demo)
            if (refreshToken.StartsWith("refresh_"))
            {
                var username = refreshToken.Replace("refresh_", "").Split('_')[0];
                return new AuthToken
                {
                    AccessToken = GenerateDemoToken(username),
                    RefreshToken = refreshToken, // Same refresh token
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    TokenType = "Bearer"
                };
            }

            return null;
        }

        private static string GenerateDemoToken(string username)
        {
            return $"demo_token_{username}_{DateTime.UtcNow.Ticks}";
        }

        private static string GenerateDemoRefreshToken(string username)
        {
            return $"refresh_{username}_{Guid.NewGuid()}";
        }
    }
}