using Autodesk.Revit.UI;
using System;
using System.Windows;
using System.Windows.Interop;
using TestLogin.Views;

namespace TestLogin.Services
{
    /// <summary>
    /// Small helper used by other add-ins to require an interactive login via the TestLogin panel.
    /// Call EnsureAuthenticated from the top of an IExternalCommand.Execute implementation.
    /// </summary>
    public static class AuthGuard
    {
        public static bool EnsureAuthenticated(ExternalCommandData commandData)
        {
            try
            {
                // Fast background restore attempt
                AuthenticationService.TryAutoLogin();
                if (AuthenticationService.IsAuthenticated)
                    return true;

                // Show the login dockable pane so the user can sign in
                try
                {
                    commandData.Application.GetDockablePane(App.PaneId).Show();
                }
                catch
                {
                    // Fallback: modeless window containing the same UI (same fallback as StartupCommand)
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

                        var helper = new WindowInteropHelper(fallbackWindow) { Owner = revitWindowHandle };
                        fallbackWindow.Show();
                    }
                    catch
                    {
                        // ignore - best effort only
                    }
                }

                TaskDialog.Show("Login Required",
                    "Please sign in using the TestLogin panel (Ribbon → TestLogin → Open Panel). After signing in, re-run the command.");

                return false;
            }
            catch
            {
                // If anything unexpected happens, fail closed (prevent action)
                TaskDialog.Show("Login Check Failed", "Unable to verify login state. Please open the TestLogin panel and sign in.");
                return false;
            }
        }
    }
}
