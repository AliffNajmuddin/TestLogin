using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TestLogin.Services;
using TestLogin.Views;
using System;
using System.Windows;
using System.Windows.Interop;
using System.Threading;

namespace TestLogin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class StartupCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Get Revit's main window handle
                IntPtr revitWindowHandle = commandData.Application.MainWindowHandle;

                // Try auto-login first (fast, non-blocking primary check)
                AuthenticationService.TryAutoLogin();

                // If not authenticated at all, prompt user to login
                if (!AuthenticationService.IsAuthenticated)
                {
                    var loginWindow = new LoginWindow();
                    SetWindowOwner(loginWindow, revitWindowHandle);
                    var loginResult = loginWindow.ShowDialog(); // modal

                    if (!loginResult.HasValue || !loginResult.Value)
                    {
                        TaskDialog.Show("Login Required", "You must be logged in to use this feature.");
                        return Result.Cancelled;
                    }
                }
                else
                {
                    // We have a valid token, but ensure we have a populated CurrentUser (name/email)
                    if (AuthenticationService.CurrentUser == null)
                    {
                        // Try to restore a full profile synchronously using stored credentials (blocking)
                        var stored = LocalStorageService.LoadCredentials();
                        var needInteractiveLogin = true;

                        if (stored != null)
                        {
                            // prefer email in new schema, otherwise legacy username
                            var identifier = !string.IsNullOrEmpty(stored.Email) ? stored.Email : stored.Username;
                            if (!string.IsNullOrEmpty(identifier) && stored.HasEncryptedPassword)
                            {
                                try
                                {
                                    var pw = LocalStorageService.GetDecryptedPassword(stored);
                                    if (!string.IsNullOrEmpty(pw))
                                    {
                                        // Blocking call: keep startup synchronous so name is available before main window
                                        var ok = AuthenticationService.LoginAsync(identifier, pw, stored.RememberMe).GetAwaiter().GetResult();

                                        // If login succeeded but CurrentUser still null, try fetching profile using the token
                                        if (ok)
                                        {
                                            if (AuthenticationService.CurrentUser == null)
                                            {
                                                try
                                                {
                                                    // Ensure bearer token is set on shared client
                                                    ApiAuthService.SetBearerToken(AuthenticationService.CurrentToken?.Token);
                                                    using var api = new ApiAuthService();
                                                    using var profileCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                                    var user = api.GetCurrentUserAsync(profileCts.Token).GetAwaiter().GetResult();
                                                    if (user != null)
                                                    {
                                                        AuthenticationService.SetCurrentUser(user, AuthenticationService.CurrentToken);
                                                        needInteractiveLogin = false;
                                                    }
                                                }
                                                catch
                                                {
                                                    // ignore and fall back to interactive login
                                                    needInteractiveLogin = true;
                                                }
                                            }
                                            else
                                            {
                                                needInteractiveLogin = false;
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // fall back to interactive login below
                                    needInteractiveLogin = true;
                                }
                            }
                        }

                        if (needInteractiveLogin)
                        {
                            var loginWindow = new LoginWindow();
                            SetWindowOwner(loginWindow, revitWindowHandle);
                            var loginResult = loginWindow.ShowDialog(); // modal

                            if (!loginResult.HasValue || !loginResult.Value)
                            {
                                TaskDialog.Show("Login Required", "You must be logged in to use this feature.");
                                return Result.Cancelled;
                            }
                        }
                    }
                }

                // At this point we should be authenticated and have user info loaded.
                var mainWindow = new MainWindow(commandData.Application);
                SetWindowOwner(mainWindow, revitWindowHandle);
                mainWindow.Show(); // Modeless - allows Revit interaction

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Error", $"An error occurred: {ex.Message}");
                return Result.Failed;
            }
        }

        private void SetWindowOwner(Window window, IntPtr parentHandle)
        {
            var helper = new WindowInteropHelper(window);
            helper.Owner = parentHandle;
        }
    }
}