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

            // Try to pre-fill username from stored credentials
            var storedCredentials = LocalStorageService.LoadCredentials();
            if (storedCredentials != null)
            {
                UsernameTextBox.Text = storedCredentials.Username;
                RememberMeCheckBox.IsChecked = storedCredentials.RememberMe;
            }

            UsernameTextBox.Focus();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text?.Trim() ?? string.Empty;
            var password = PasswordBox.Password ?? string.Empty;

            LoginButton.IsEnabled = false;
            // Disable other inputs if present
            RememberMeCheckBox.IsEnabled = false;
            UsernameTextBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;

            try
            {
                using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(30));
                var api = new ApiAuthService();
                var result = await api.AuthenticateAsync(username, password, cts.Token);

                // Build stored credentials and persist securely (encrypt password via LocalStorageService)
                var creds = new StoredCredentials
                {
                    Username = username,
                    RememberMe = RememberMeCheckBox.IsChecked ?? false,
                    Token = result.AccessToken,
                    ExpiresAt = result.AccessToken?.ExpiresAt,
                    LastLogin = System.DateTime.UtcNow
                };
                LocalStorageService.SaveCredentials(creds); // Save credentials using the available overload

                // Apply bearer token for shared HttpClient usage
                ApiAuthService.SetBearerToken(result.AccessToken?.Token);

                // Notify AuthenticationService about the authenticated user using typed API
                AuthenticationService.SetCurrentUser(result.User, result.AccessToken);

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
            var username = UsernameTextBox.Text;
            var password = PasswordBox.Password;
            var rememberMe = RememberMeCheckBox.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter both username and password.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show loading state
            LoginButton.IsEnabled = false;
            LoginButton.Content = "Logging in...";

            // Use authentication service with async call
            var success = await AuthenticationService.LoginAsync(username, password, rememberMe);

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