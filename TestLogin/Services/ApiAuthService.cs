using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TestLogin.Models;

namespace TestLogin.Services
{
    public sealed record LoginRequest(string Username, string Password);

    public sealed class LoginResponse
    {
        public AuthToken? AccessToken { get; init; }
        public UserDto? User { get; init; }
    }

    public class ApiException : Exception
    {
        public int StatusCode { get; }
        public ApiException(string message, int statusCode = 0) : base(message) => StatusCode = statusCode;
    }

    public sealed class ApiAuthService : IDisposable
    {
        private static readonly HttpClient s_httpClient;

        static ApiAuthService()
        {
            s_httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://app.mimar.tech/"),
                Timeout = TimeSpan.FromSeconds(30)
            };
            s_httpClient.DefaultRequestHeaders.Accept.Clear();
            s_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            s_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TestLoginAddin/1.0");
        }

        public async Task<LoginResponse> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Username and password are required.");

            var request = new LoginRequest(username, password);
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await s_httpClient.PostAsync("api/login", content, cancellationToken).ConfigureAwait(false);
            var respContent = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                string msg = $"Server returned {(int)resp.StatusCode} {resp.ReasonPhrase}";
                try
                {
                    using var doc = JsonDocument.Parse(respContent);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                            msg = m.GetString() ?? msg;
                    }
                }
                catch { /* ignore parse errors */ }

                throw new ApiException(msg, (int)resp.StatusCode);
            }

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
            var login = JsonSerializer.Deserialize<LoginResponse>(respContent, options);
            if (login == null || login.AccessToken == null || string.IsNullOrWhiteSpace(login.AccessToken.Token))
                throw new ApiException("Invalid login response from server.");

            return login;
        }

        public static void SetBearerToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                s_httpClient.DefaultRequestHeaders.Authorization = null;
                return;
            }

            s_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public static HttpClient SharedHttpClient => s_httpClient;

        public void Dispose()
        {
            // Do not dispose static HttpClient
        }
    }
}