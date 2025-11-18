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

        public static void SetCurrentUser(UserDto? user, AuthToken? token)
        {
            CurrentUser = user;
            CurrentToken = token;
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
                        Username = username,
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
    }
}