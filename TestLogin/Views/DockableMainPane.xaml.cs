using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using TestLogin.Services;
using TestLogin.Models;

namespace TestLogin.Views
{
    public partial class DockableMainPane : UserControl
    {
        public DockableMainPane()
        {
            InitializeComponent();

            // Wire events
            AuthenticationService.UserLoggedIn += OnAuthChanged;
            AuthenticationService.UserLoggedOut += OnAuthChanged;

            LoginButton.Click += LoginButton_Click;
            LogoutButton.Click += LogoutButton_Click;


            // Watermark handlers
            UsernameTextBox.TextChanged += (_, __) => UpdateUsernameWatermark();
            PasswordBox.PasswordChanged += (_, __) => UpdatePasswordWatermark();

            Loaded += (_, __) =>
            {
                UpdateStatus();
                UpdateUsernameWatermark();
                UpdatePasswordWatermark();
            };
        }

        private void UpdateUsernameWatermark()
        {
            UsernameWatermark.Visibility = string.IsNullOrEmpty(UsernameTextBox.Text)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void UpdatePasswordWatermark()
        {
            PasswordWatermark.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void OnAuthChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(UpdateStatus);
        }

        private void UpdateStatus()
        {
            if (AuthenticationService.IsAuthenticated)
            {
                var user = AuthenticationService.CurrentUser;
                var token = AuthenticationService.CurrentToken ?? LocalStorageService.LoadToken();

                var name = user?.FullName ?? user?.Email ?? user?.Username ?? "User";
                var expires = token?.ExpiresAt.HasValue == true ? token!.ExpiresAt!.Value.ToLocalTime().ToString("f") : string.Empty;

                StatusTextBlock.Text = $"Signed in as {name}";
                ExpiresTextBlock.Text = string.IsNullOrEmpty(expires) ? string.Empty : $"Token expires: {expires}";

                var apiMessage = token?.Message;
                if (!string.IsNullOrWhiteSpace(apiMessage))
                {
                    MessageTextBlock.Text = apiMessage.Trim();
                    MessageBorder.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    MessageTextBlock.Text = string.Empty;
                    MessageBorder.Visibility = System.Windows.Visibility.Collapsed;
                }

                // Hide login fields when signed in (collapse form)
                LoginFormBorder.Visibility = System.Windows.Visibility.Collapsed;

                LoginButton.IsEnabled = false;
                LogoutButton.IsEnabled = true;
            }
            else
            {
                StatusTextBlock.Text = "Not authenticated.";
                ExpiresTextBlock.Text = string.Empty;
                MessageTextBlock.Text = string.Empty;
                MessageBorder.Visibility = System.Windows.Visibility.Collapsed;

                // Show login form when not signed in
                LoginFormBorder.Visibility = System.Windows.Visibility.Visible;

                LoginButton.IsEnabled = true;
                LogoutButton.IsEnabled = false;
            }

            // Clear transient UI
            LoginErrorText.Visibility = System.Windows.Visibility.Collapsed;
            LoginProgress.Visibility = System.Windows.Visibility.Collapsed;
        }

        private async void LoginButton_Click(object? sender, RoutedEventArgs e)
        {
            LoginErrorText.Text = string.Empty;
            LoginErrorText.Visibility = System.Windows.Visibility.Collapsed;
            LoginProgress.Visibility = System.Windows.Visibility.Visible;

            // Disable inputs while auth in progress
            LoginButton.IsEnabled = false;
            UsernameTextBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;
            RememberMeCheckBox.IsEnabled = false;


            var email = UsernameTextBox.Text?.Trim() ?? string.Empty;
            var password = PasswordBox.Password ?? string.Empty;
            var remember = RememberMeCheckBox.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                LoginErrorText.Text = "Please enter both email and password.";
                LoginErrorText.Visibility = System.Windows.Visibility.Visible;
                LoginProgress.Visibility = System.Windows.Visibility.Collapsed;
                LoginButton.IsEnabled = true;
                UsernameTextBox.IsEnabled = true;
                PasswordBox.IsEnabled = true;
                RememberMeCheckBox.IsEnabled = true;

                return;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var api = new ApiAuthService();
                var result = await api.AuthenticateAsync(email, password, cts.Token);

                // Apply bearer token for shared HttpClient usage
                ApiAuthService.SetBearerToken(result.AccessToken?.Token);

                // If API didn't include user, try GET /api/user
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

                // Persist credentials if requested
                var creds = new StoredCredentials
                {
                    Email = user?.Email ?? result.User?.Email ?? email,
                    Username = result.User?.Username ?? email,
                    RememberMe = remember,
                    Token = result.AccessToken,
                    ExpiresAt = result.AccessToken?.ExpiresAt,
                    LastLogin = DateTime.UtcNow
                };

                // Save password when remembering
                LocalStorageService.SaveCredentials(creds, remember ? password : null);
                LocalStorageService.SaveToken(result.AccessToken);

                // Ensure UI and shared state updated
                var minimalUser = user ?? result.User ?? new UserDto
                {
                    Email = email,
                    Username = email,
                    FullName = email
                };

                AuthenticationService.SetCurrentUser(minimalUser, result.AccessToken);

                // Update panel to show message/expiry
                UpdateStatus();
            }
            catch (ApiException apiEx)
            {
                LoginErrorText.Text = apiEx.Message;
                LoginErrorText.Visibility = System.Windows.Visibility.Visible;
            }
            catch (OperationCanceledException)
            {
                LoginErrorText.Text = "Login timed out. Check network and try again.";
                LoginErrorText.Visibility = System.Windows.Visibility.Visible;
            }
            catch (Exception ex)
            {
                LoginErrorText.Text = $"Unexpected error: {ex.Message}";
                LoginErrorText.Visibility = System.Windows.Visibility.Visible;
            }
            finally
            {
                LoginProgress.Visibility = System.Windows.Visibility.Collapsed;
                // Re-enable inputs if still not authenticated
                if (!AuthenticationService.IsAuthenticated)
                {
                    LoginButton.IsEnabled = true;
                    UsernameTextBox.IsEnabled = true;
                    PasswordBox.IsEnabled = true;
                    RememberMeCheckBox.IsEnabled = true;
                }


                // Update watermarks after any change
                UpdateUsernameWatermark();
                UpdatePasswordWatermark();
            }
        }

        private void LogoutButton_Click(object? sender, RoutedEventArgs e)
        {
            AuthenticationService.Logout();
            LocalStorageService.ClearStoredSecrets();
            UpdateStatus();

            UpdateUsernameWatermark();
            UpdatePasswordWatermark();
        }
    }
}