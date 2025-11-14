using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TestLogin.Services;

namespace TestLogin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class LogoutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (AuthenticationService.IsAuthenticated)
            {
                var user = AuthenticationService.CurrentUser;
                AuthenticationService.Logout();

                TaskDialog.Show("Logged Out",
                    $"You have been successfully logged out.\n\nGoodbye, {user.FullName}!");
            }
            else
            {
                TaskDialog.Show("Info", "You are not currently logged in.");
            }

            return Result.Succeeded;
        }
    }
}