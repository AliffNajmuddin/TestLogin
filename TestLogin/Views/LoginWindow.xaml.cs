using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using TestLogin.Models;
using TestLogin.Services;

namespace TestLogin.Views
{
    public partial class LoginWindow : Window
    {
        public bool IsAuthenticated { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();

            // Try to pre-fill email from stored credentials (prefer new schema)
            var storedCredentials = LocalStorageService.LoadCredentials();
            if (storedCredentials != null)
            {
                UsernameTextBox.Text = storedCredentials.Email ?? storedCredentials.Username ?? string.Empty;
                RememberMeCheckBox.IsChecked = storedCredentials.RememberMe;
            }

            UsernameTextBox.Focus();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var email = UsernameTextBox.Text?.Trim() ?? string.Empty;
            var password = PasswordBox.Password ?? string.Empty;

            LoginButton.IsEnabled = false;
            // Disable other inputs if present
            RememberMeCheckBox.IsEnabled = false;
            UsernameTextBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var api = new ApiAuthService();
                var result = await api.AuthenticateAsync(email, password, cts.Token);

                // Apply bearer token for shared HttpClient usage
                ApiAuthService.SetBearerToken(result.AccessToken?.Token);

                // If API didn't include user in the auth response, fetch profile from /api/me
                var user = result.User;
                if (user == null)
                {
                    try
                    {
                        using var profileCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                        profileCts.CancelAfter(TimeSpan.FromSeconds(5));
                        user = await api.GetCurrentUserAsync(profileCts.Token);
                    }
                    catch { user = null; }
                }

                // Build stored credentials and persist securely (encrypt password via LocalStorageService)
                // Only persist Email/Username (no Name field)
                var creds = new StoredCredentials
                {
                    Email = user?.Email ?? result.User?.Email ?? email,
                    Username = result.User?.Username ?? email,
                    RememberMe = RememberMeCheckBox.IsChecked ?? false,
                    Token = result.AccessToken,
                    ExpiresAt = result.AccessToken?.ExpiresAt,
                    LastLogin = DateTime.UtcNow
                };
                LocalStorageService.SaveCredentials(creds, password); // pass the plain password so it's encrypted and stored

                // Ensure we always have a minimal UserDto for UI immediately after login (email used as display)
                var minimalUser = user ?? result.User ?? new UserDto
                {
                    Email = email,
                    Username = email,
                    FullName = email
                };

                // Notify AuthenticationService about the authenticated user using typed API
                AuthenticationService.SetCurrentUser(minimalUser, result.AccessToken);

                DialogResult = true;
                Close();
            }
            catch (ApiException apiEx)
            {
                MessageBox.Show(apiEx.Message, "Login failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Login timed out. Check your network and try again.", "Timeout", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoginButton.IsEnabled = true;
                RememberMeCheckBox.IsEnabled = true;
                UsernameTextBox.IsEnabled = true;
                PasswordBox.IsEnabled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async System.Threading.Tasks.Task AttemptLogin()
        {
            var email = UsernameTextBox.Text;
            var password = PasswordBox.Password;
            var rememberMe = RememberMeCheckBox.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter both email and password.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show loading state
            LoginButton.IsEnabled = false;
            LoginButton.Content = "Logging in...";

            // Use authentication service with async call (service accepts identifier param; we pass email)
            var success = await AuthenticationService.LoginAsync(email, password, rememberMe);

            if (success)
            {
                IsAuthenticated = true;
                DialogResult = true;
                Close();
            }
            else
            {
                // Reset UI
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Login";
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                CancelButton_Click(sender, e);
            }
        }
    }
}