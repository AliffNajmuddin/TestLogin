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
                var token = AuthenticationService.CurrentToken;

                TaskDialog.Show("Authentication Status",
                    $"✅ Logged In\n\n" +
                    $"User: {user.FullName}\n" +
                    $"Username: {user.Username}\n" +
                    $"Role: {user.Role}\n" +
                    $"Token expires: {token.ExpiresAt.ToLocalTime():f}");
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