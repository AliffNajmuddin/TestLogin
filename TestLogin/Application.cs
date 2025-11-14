using Nice3point.Revit.Toolkit.External;
using System.Resources;
using TestLogin.Commands;

namespace TestLogin
{
    [UsedImplicitly]
    public class Application : ExternalApplication
    {
        public override void OnStartup()
        {
            CreateRibbon();
        }

        private void CreateRibbon()
        {
            var panel = Application.CreatePanel("Commands", "TestLogin");

            // Main execution button
            panel.AddPushButton<StartupCommand>("Plugin")
                .SetImage("C:\\Users\\najna\\source\\repos\\TestLogin\\TestLogin\\Resources\\Icons\\RibbonIcon16.png")
                .SetLargeImage("C:\\Users\\najna\\source\\repos\\TestLogin\\TestLogin\\Resources\\Icons\\RibbonIcon32.png")
                .SetToolTip("Login and open the main interface");

            // Logout button
            panel.AddPushButton<LogoutCommand>("Logout")
                .SetImage("C:\\Users\\najna\\source\\repos\\TestLogin\\TestLogin\\Resources\\Icons\\Logout16.png")
                .SetLargeImage("C:\\Users\\najna\\source\\repos\\TestLogin\\TestLogin\\Resources\\Icons\\Logout32.png")
                .SetToolTip("Logout from the application");

            // Status button
            panel.AddPushButton<StatusCommand>("Status")
                .SetToolTip("Check current authentication status");
        }
    }
}