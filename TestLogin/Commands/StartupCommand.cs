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

                // If we are not authenticated, open the dockable pane and instruct the user to sign in there.
                if (!AuthenticationService.IsAuthenticated)
                {
                    TryShowPane(commandData);

                    // Inform user and abort the command; they should sign in via the panel and re-run the feature.
                    TaskDialog.Show("Login Required",
                        "Please sign in using the TestLogin panel (Ribbon → TestLogin → Open Panel). After signing in, re-run the command.");
                    return Result.Cancelled;
                }

                // We have a valid token; ensure CurrentUser is populated (existing restore logic)
                if (AuthenticationService.CurrentUser == null)
                {
                    var stored = LocalStorageService.LoadCredentials();
                    var needInteractiveLogin = true;

                    if (stored != null)
                    {
                        var identifier = !string.IsNullOrEmpty(stored.Email) ? stored.Email : stored.Username;
                        if (!string.IsNullOrEmpty(identifier) && stored.HasEncryptedPassword)
                        {
                            try
                            {
                                var pw = LocalStorageService.GetDecryptedPassword(stored);
                                if (!string.IsNullOrEmpty(pw))
                                {
                                    // Blocking call: keep startup synchronous so name is available before main UI
                                    var ok = AuthenticationService.LoginAsync(identifier, pw, stored.RememberMe).GetAwaiter().GetResult();

                                    if (ok)
                                    {
                                        if (AuthenticationService.CurrentUser == null)
                                        {
                                            try
                                            {
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
                                needInteractiveLogin = true;
                            }
                        }
                    }

                    if (needInteractiveLogin)
                    {
                        // Open dockable pane and instruct the user to sign in there
                        TryShowPane(commandData);

                        TaskDialog.Show("Login Required",
                            "Please sign in using the TestLogin panel (Ribbon → TestLogin → Open Panel). After signing in, re-run the command.");
                        return Result.Cancelled;
                    }
                }

                // At this point authentication is satisfied; show the panel (user can keep it open)
                TryShowPane(commandData);

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Error", $"An error occurred: {ex.Message}");
                return Result.Failed;
            }
        }

        private void TryShowPane(ExternalCommandData commandData)
        {
            try
            {
                var paneId = new DockablePaneId(new Guid("B1A2C3D4-1234-4F56-8A9B-1C2D3E4F5A6B"));
                var pane = commandData.Application.GetDockablePane(paneId);
                pane.Show();
            }
            catch
            {
                // Fallback: show DockableMainPane inside a modeless window
                try
                {
                    var revitWindowHandle = commandData.Application.MainWindowHandle;
                    var fallbackWindow = new Window
                    {
                        Title = "TestLogin",
                        Content = new DockableMainPane(),
                        Width = 380,
                        Height = 480,
                        WindowStartupLocation = WindowStartupLocation.Manual
                    };

                    // Give Revit the owner so window behaves correctly
                    var helper = new WindowInteropHelper(fallbackWindow) { Owner = revitWindowHandle };

                    // Show modelessly so Revit remains usable
                    fallbackWindow.Show();
                }
                catch
                {
                    // ignore - cannot show pane
                }
            }
        }
    }
}