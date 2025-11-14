using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TestLogin.Services;
using TestLogin.Views;
using System.Windows;
using System.Windows.Interop;

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

                // Try auto-login first
                if (!AuthenticationService.IsAuthenticated)
                {
                    if (!AuthenticationService.TryAutoLogin())
                    {
                        // Auto-login failed, show login window
                        var loginWindow = new LoginWindow();
                        SetWindowOwner(loginWindow, revitWindowHandle);

                        var loginResult = loginWindow.ShowDialog(); // Keep login modal

                        if (!loginResult.HasValue || !loginResult.Value || !loginWindow.IsAuthenticated)
                        {
                            // User cancelled login or authentication failed
                            TaskDialog.Show("Login Required",
                                "You must be logged in to use this feature.");
                            return Result.Cancelled;
                        }
                    }
                }

                // User is authenticated, show main window as MODELESS
                var mainWindow = new MainWindow(commandData.Application); // FIXED: pass commandData.Application
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