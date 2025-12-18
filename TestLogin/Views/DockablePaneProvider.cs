using Autodesk.Revit.UI;

namespace TestLogin.Views
{
    // Minimal provider that hosts the DockableMainPane UserControl
    public class DockablePaneProvider : IDockablePaneProvider
    {
        public void SetupDockablePane(DockablePaneProviderData data)
        {
            var control = new DockableMainPane();
            data.FrameworkElement = control;

            // Optional: choose an initial dock position
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }
    }
}