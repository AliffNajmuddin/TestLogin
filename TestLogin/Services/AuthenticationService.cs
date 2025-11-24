using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TestLogin.Models;

namespace TestLogin.Services
{
    public static class AuthenticationService
    {
        public static UserDto? CurrentUser { get; private set; }
        public static AuthToken? CurrentToken { get; private set; }
        public static bool IsAuthenticated { get; private set; }

        // Event raised when the user is explicitly logged out
        public static event EventHandler? UserLoggedOut;

        // Event raised when a user has been set (login completed / profile fetched)
        public static event EventHandler? UserLoggedIn;

        public static void SetCurrentUser(UserDto? user, AuthToken? token)
        {
            CurrentUser = user;
            CurrentToken = token;
            // Authenticated when we have a valid token (user may be null until profile is fetched)
            IsAuthenticated = token?.IsValid() ?? false;

            if (token != null)
            {
                try
                {
                    // Persist token securely
                    LocalStorageService.SaveToken(token);
                }
                catch
                {
                    // ignore persistence errors
                }
            }

            // Notify listeners when a concrete user object is available
            if (user != null)
            {
                try
                {
                    UserLoggedIn?.Invoke(null, EventArgs.Empty);
                }
                catch
                {
                    // swallow exceptions from subscribers
                }
            }
        }

        public static async Task<bool> LoginAsync(string username, string password, bool rememberMe)
        {
            try
            {
                using var api = new ApiAuthService();
                var resp = await api.AuthenticateAsync(username, password);

                ApiAuthService.SetBearerToken(resp.AccessToken?.Token);

                if (rememberMe)
                {
                    var creds = new StoredCredentials
                    {
                        // Only record email (no Name)
                        Email = username,
                        RememberMe = true,
                        Token = resp.AccessToken,
                        ExpiresAt = resp.AccessToken?.ExpiresAt,
                        LastLogin = DateTime.UtcNow
                    };
                    LocalStorageService.SaveCredentials(creds, password);
                }

                SetCurrentUser(resp.User, resp.AccessToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Try to restore authentication from persisted data.
        // Returns true if authentication is restored.
        public static bool TryAutoLogin()
        {
            try
            {
                // Prefer persisted token
                var token = LocalStorageService.LoadToken();
                if (token != null && token.IsValid())
                {
                    ApiAuthService.SetBearerToken(token.Token);
                    CurrentToken = token;
                    IsAuthenticated = true;

                    // Try to populate a minimal current user from stored credentials if available
                    var stored = LocalStorageService.LoadCredentials();
                    if (stored != null)
                    {
                        CurrentUser = new UserDto
                        {
                            Username = stored.Email ?? stored.Username,
                            FullName = stored.Email, // keep FullName in-memory as email for display consistency
                            Email = stored.Email
                        };

                        // If we had a minimal user, fire the login event so UI updates immediately
                        try { UserLoggedIn?.Invoke(null, EventArgs.Empty); } catch { }
                    }

                    return true;
                }

                // Fallback: if we have encrypted credentials, attempt to decrypt and re-login asynchronously (non-blocking)
                var creds = LocalStorageService.LoadCredentials();
                if (creds != null && creds.HasEncryptedPassword)
                {
                    // Fire-and-forget background attempt to avoid blocking startup/UI.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var loginId = !string.IsNullOrEmpty(creds.Email) ? creds.Email : creds.Username;
                            if (string.IsNullOrEmpty(loginId))
                                return;

                            var pw = LocalStorageService.GetDecryptedPassword(creds);
                            if (string.IsNullOrEmpty(pw))
                                return;

                            // Perform the login asynchronously; SetCurrentUser will update shared state when complete.
                            await LoginAsync(loginId, pw, creds.RememberMe);
                        }
                        catch
                        {
                            // Swallow — background attempt should not crash the host
                        }
                    });
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // Logout the current user, clear persisted secrets and notify listeners
        public static void Logout()
        {
            CurrentUser = null;
            CurrentToken = null;
            IsAuthenticated = false;

            try
            {
                // Remove bearer token from shared client
                ApiAuthService.SetBearerToken(null);
            }
            catch { }

            try
            {
                // Clear persisted secrets (credentials + token)
                LocalStorageService.ClearStoredSecrets();
            }
            catch { }

            try
            {
                UserLoggedOut?.Invoke(null, EventArgs.Empty);
            }
            catch { }
        }
    }
}