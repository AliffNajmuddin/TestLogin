using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TestLogin.Services;

namespace TestLogin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class StatusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (AuthenticationService.IsAuthenticated)
            {
                var user = AuthenticationService.CurrentUser;
                var token = AuthenticationService.CurrentToken ?? LocalStorageService.LoadToken();

                var userFull = user?.FullName ?? user?.Username ?? "Unknown";
                var username = user?.Username ?? "Unknown";
                var role = user?.Role ?? "Unknown";

                string expiresText = "Token information not available";
                if (token?.ExpiresAt is DateTime expires)
                {
                    try
                    {
                        expiresText = expires.ToLocalTime().ToString("f");
                    }
                    catch
                    {
                        expiresText = expires.ToString();
                    }
                }

                TaskDialog.Show("Authentication Status",
                    $"✅ Logged In\n\n" +
                    $"User: {userFull}\n" +
                    $"Username: {username}\n" +
                    $"Role: {role}\n" +
                    $"Token expires: {expiresText}");
            }
            else
            {
                TaskDialog.Show("Authentication Status",
                    "❌ Not Logged In\n\n" +
                    "Please use the 'Show Panel' button to login.");
            }

            return Result.Succeeded;
        }
    }
}