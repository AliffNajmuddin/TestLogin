using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
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

        // Some backends (Laravel example) return access_token as a top-level string.
        // We don't add more properties here; AuthenticateAsync handles both shapes.
    }

    public class ApiException : Exception
    {
        public int StatusCode { get; }
        public ApiException(string message, int statusCode = 0) : base(message) => StatusCode = statusCode;
    }

    public sealed class ApiAuthService : IDisposable
    {
        private static readonly HttpClient s_httpClient;
        private static readonly string s_logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TestLogin");
        private static readonly string s_logPath = Path.Combine(s_logFolder, "auth_debug.log");

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

            try { Directory.CreateDirectory(s_logFolder); } catch { /* ignore */ }
        }

        private static void Log(string message)
        {
            try
            {
                var entry = $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}";
                File.AppendAllText(s_logPath, entry, Encoding.UTF8);
                Debug.WriteLine(message);
            }
            catch { /* ignore logging failures */ }
        }

        // New: log response headers to help diagnose 401 reasons
        private static void LogResponseHeaders(HttpResponseMessage resp)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Response headers:");
                foreach (var h in resp.Headers)
                    sb.AppendLine($"{h.Key}: {string.Join(", ", h.Value)}");
                if (resp.Content?.Headers != null)
                {
                    sb.AppendLine("Content headers:");
                    foreach (var h in resp.Content.Headers)
                        sb.AppendLine($"{h.Key}: {string.Join(", ", h.Value)}");
                }
                Log(sb.ToString());
            }
            catch { /* ignore logging failures */ }
        }

        public async Task<LoginResponse> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Username and password are required.");

            // Preferred JSON payload (send email + username)

            var payload = new
            {
                username = username,
                email = username,
                password = password
            };

            var serializeOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var json = JsonSerializer.Serialize(payload, serializeOptions);

            Log($"REQUEST -> POST /api/login payload: {json}");

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await s_http_client_post("api/login", content, cancellationToken).ConfigureAwait(false);
            var respContent = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            Log($"RESPONSE <- {(int)resp.StatusCode} {resp.ReasonPhrase}: {respContent}");
            LogResponseHeaders(resp);

            // If initial JSON attempt fails with 401, try form-urlencoded fallback
            if (!resp.IsSuccessStatusCode && resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                try
                {
                    Log("Attempting form-urlencoded fallback (email/password).");
                    using var form = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("email", username),
                        new KeyValuePair<string, string>("password", password)
                    });

                    using var resp2 = await s_httpClient.PostAsync("api/login", form, cancellationToken).ConfigureAwait(false);
                    var resp2Content = await resp2.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    Log($"RESPONSE-FORM <- {(int)resp2.StatusCode} {resp2.ReasonPhrase}: {resp2Content}");
                    LogResponseHeaders(resp2);

                    if (resp2.IsSuccessStatusCode)
                    {
                        // parse resp2Content using same detection logic below
                        var loginFromForm = TryParseLoginResponse(resp2Content);
                        if (loginFromForm != null)
                            return loginFromForm;

                        Log("Form fallback succeeded but response could not be parsed as auth token.");
                        throw new ApiException("Login succeeded but response could not be parsed.", (int)resp2.StatusCode);
                    }
                    else
                    {
                        // replace respContent with resp2Content for error reporting
                        respContent = resp2Content;
                    }
                }
                catch (Exception ex)
                {
                    Log($"FORM FALLBACK ERROR: {ex.Message}");
                    // continue to build original error message
                }
            }

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
                        else if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                            msg = e.GetString() ?? msg;
                        else if (doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                            msg = d.GetString() ?? msg;
                        else if (doc.RootElement.TryGetProperty("errors", out var errs))
                            msg = errs.ToString();
                    }
                }
                catch
                {
                    msg = $"{msg}. Raw response: {respContent}";
                }

                Log($"AUTH ERROR: {msg}");
                throw new ApiException(msg, (int)resp.StatusCode);
            }

            // success path: parse response (Laravel-style or richer shape)
            var parsed = TryParseLoginResponse(respContent);
            if (parsed == null)
            {
                var em = $"Invalid login response from server. Response body: {respContent}";
                Log(em);
                throw new ApiException(em);
            }

            return parsed;
        }

        // Centralized parsing logic reused for both JSON and form fallback responses.
        private static LoginResponse? TryParseLoginResponse(string respContent)
        {
            try
            {
                using var doc = JsonDocument.Parse(respContent);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    // Laravel-style: {"access_token":"...", "token_type":"Bearer", "message":"..."}
                    if (doc.RootElement.TryGetProperty("access_token", out var at) && at.ValueKind == JsonValueKind.String)
                    {
                        var tokenString = at.GetString();
                        // Try to read optional message field
                        string? message = null;
                        if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                        {
                            message = msg.GetString();
                        }

                        return new LoginResponse
                        {
                            AccessToken = new AuthToken { Token = tokenString, Message = message },
                            User = null
                        };
                    }

                    // Other shape: { accessToken: { token: "...", expiresAt: "..." }, user: { ... } }
                    // Try generic deserialization into LoginResponse
                }
            }
            catch
            {
                // ignore parse errors - fall through to generic deserialization
            }

            try
            {
                var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
                var loginResp = JsonSerializer.Deserialize<LoginResponse>(respContent, options);
                if (loginResp != null && loginResp.AccessToken != null && !string.IsNullOrWhiteSpace(loginResp.AccessToken.Token))
                    return loginResp;
            }
            catch
            {
                // ignore
            }

            return null;
        }

        public async Task<LoginResponse> AuthenticateWithGoogleAsync(string idToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                throw new ArgumentException("idToken is required", nameof(idToken));

            var payload = new { idToken = idToken };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Log($"REQUEST -> POST /api/login/google payload: {json}");

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await s_http_client_post("api/login/google", content, cancellationToken).ConfigureAwait(false);
            var respContent = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            Log($"RESPONSE <- {(int)resp.StatusCode} {resp.ReasonPhrase}: {respContent}");
            LogResponseHeaders(resp);

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
                        else if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                            msg = e.GetString() ?? msg;
                        else if (doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                            msg = d.GetString() ?? msg;
                        else if (doc.RootElement.TryGetProperty("errors", out var errs))
                            msg = errs.ToString();
                    }
                }
                catch
                {
                    msg = $"{msg}. Raw response: {respContent}";
                }

                Log($"GOOGLE AUTH ERROR: {msg}");
                throw new ApiException(msg, (int)resp.StatusCode);
            }

            var parsed = TryParseLoginResponse(respContent);
            if (parsed == null)
            {
                var em = $"Invalid login response from server. Response body: {respContent}";
                Log(em);
                throw new ApiException(em);
            }

            return parsed;
        }

        // small wrapper so we can centralize PostAsync calls in case you later need to add headers or logging
        private async Task<HttpResponseMessage> s_http_client_post(string relativeUrl, HttpContent content, CancellationToken cancellationToken)
        {
            return await s_httpClient.PostAsync(relativeUrl, content, cancellationToken).ConfigureAwait(false);
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


        // Fetch the canonical current user profile from the API.
        // Calls GET /api/user and returns a UserDto or null on failure.
        public async Task<UserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Use the shared HttpClient which should already have Authorization header set via SetBearerToken
                using var resp = await s_httpClient.GetAsync("api/user", cancellationToken).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    Log($"GetCurrentUserAsync returned {(int)resp.StatusCode} {resp.ReasonPhrase}");
                    return null;
                }

                var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    // Expecting shape like:
                    // { "id": 260, "name": "...", "email": "...", ... }
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        var user = new UserDto
                        {
                            Username = root.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null,
                            FullName = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null,
                            Email = root.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String ? em.GetString() : null,
                            Role = root.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null
                        };

                        return user;
                    }

                    // Fallback: attempt to deserialize into UserDto
                    var deserialized = JsonSerializer.Deserialize<UserDto>(json, opts);
                    return deserialized;
                }
                catch (Exception ex)
                {
                    Log($"GetCurrentUserAsync parse error: {ex.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"GetCurrentUserAsync error: {ex.Message}");
                return null;
            }
        }
    }
}