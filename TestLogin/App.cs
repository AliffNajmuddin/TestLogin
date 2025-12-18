using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace TestLogin
{
    public class App : IExternalApplication
    {
        // Stable GUID for the dockable pane
        internal static readonly DockablePaneId PaneId = new DockablePaneId(new Guid("B1A2C3D4-1234-4F56-8A9B-1C2D3E4F5A6B"));

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Register the dockable pane provider. The provider will create the UI (DockableMainPane).
                application.RegisterDockablePane(PaneId, "TestLogin", new Views.DockablePaneProvider());

                // Create ribbon tab (if not already present)
                const string tabName = "TestLogin";
                try
                {
                    application.CreateRibbonTab(tabName);
                }
                catch
                {
                    // Tab already exists — ignore
                }

                // Obtain or create the ribbon panel
                RibbonPanel panel = application.GetRibbonPanels(tabName)
                                             .FirstOrDefault(p => p.Name == "TestLogin");
                if (panel == null)
                {
                    panel = application.CreateRibbonPanel(tabName, "TestLogin");
                }

                // Add a button to open the dockable pane (idempotent)
                const string pushButtonInternalName = "OpenTestLogin";
                var existing = panel.GetItems().FirstOrDefault(i => i.Name == pushButtonInternalName);
                if (existing == null)
                {
                    var assemblyPath = typeof(App).Assembly.Location;
                    var buttonData = new PushButtonData(pushButtonInternalName, "Open Panel", assemblyPath, "TestLogin.Commands.StartupCommand")
                    {
                        ToolTip = "Open TestLogin dockable panel"
                    };

                    // Add the button and then attempt to set its icon from the add-in folder
                    var item = panel.AddItem(buttonData);
                    if (item is PushButton pushButton)
                    {
                        try
                        {
                            var assemblyFolder = Path.GetDirectoryName(assemblyPath) ?? string.Empty;
                            var iconPath = Path.Combine(assemblyFolder, "Resources", "Icons", "RibbonIcon16.png");

                            if (File.Exists(iconPath))
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.UriSource = new Uri(iconPath, UriKind.Absolute);
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.EndInit();
                                // Set both small & large images (Revit will scale as needed)
                                pushButton.Image = bmp;
                                pushButton.LargeImage = bmp;
                            }
                        }
                        catch
                        {
                            // ignore icon load failures so button still works
                        }
                    }
                }

                return Result.Succeeded;
            }
            catch
            {
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            // Nothing special to do here
            return Result.Succeeded;
        }
    }
}