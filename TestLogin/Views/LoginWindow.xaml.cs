using System.Windows;
using System.Windows.Input;
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
            await AttemptLogin();
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