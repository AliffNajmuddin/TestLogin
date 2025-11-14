using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TestLogin.Models;
using TestLogin.Services;

namespace TestLogin.Views
{
    public partial class MainWindow : Window
    {
        private UIApplication _revitApp;
        private UIDocument _uiDoc;
        private bool _isMonitoringSelection = false;
        private bool _isClosing = false;
        private bool _isDarkMode = false;
        private List<ElementId> _previousSelection = new List<ElementId>();
        public ObservableCollection<RevitElement> SelectedElements { get; set; }

        public MainWindow(UIApplication revitApp)
        {
            _revitApp = revitApp;
            _uiDoc = revitApp.ActiveUIDocument;
            SelectedElements = new ObservableCollection<RevitElement>();

            // Initialize UI elements before accessing any controls
            InitializeComponent();

            // Load settings now that controls exist
            LoadSettings();

            // Apply theme after initialization and settings loaded
            ApplyTheme();

            ElementsListView.ItemsSource = SelectedElements;
            LoadUserInfo();
            StartSelectionMonitoring();

            // Update when authentication state changes
            AuthenticationService.UserLoggedOut += OnUserLoggedOut;

            // Handle element selection in list view - Fixed: Use System.Windows.Controls namespace
            ElementsListView.SelectionChanged += OnElementsListViewSelectionChanged;
        }

        private void LoadSettings()
        {
            var settings = LocalStorageService.LoadSettings() ?? new AppSettings();

            _isDarkMode = settings.DarkMode;

            // Only access UI controls if they've been initialized
            if (StayOnTopCheckBox != null)
            {
                StayOnTopCheckBox.IsChecked = settings.StayOnTop;
                this.Topmost = StayOnTopCheckBox.IsChecked ?? false;
            }
        }

        private void SaveSettings()
        {
            var settings = new AppSettings
            {
                DarkMode = _isDarkMode,
                StayOnTop = StayOnTopCheckBox?.IsChecked ?? false
            };
            LocalStorageService.SaveSettings(settings);
        }

        private void ApplyTheme()
        {
            if (_isDarkMode)
            {
                // Apply dark theme
                MainGrid.Background = System.Windows.Media.Brushes.Black;
                ContentGrid.Background = System.Windows.Media.Brushes.Black;
                HeaderBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30));
                FooterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30));
                SelectionHeader.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30));
                RightPanel.Background = System.Windows.Media.Brushes.Black;
                LeftPanel.Background = System.Windows.Media.Brushes.Black;

                // Set group box backgrounds
                SetGroupBoxBackground(WelcomeGroup, 0x2D, 0x2D, 0x30);
                SetGroupBoxBackground(ActionsGroup, 0x2D, 0x2D, 0x30);
                SetGroupBoxBackground(StatusGroup, 0x2D, 0x2D, 0x30);
                SetGroupBoxBackground(StatsGroup, 0x2D, 0x2D, 0x30);
                SetGroupBoxBackground(ElementsGroup, 0x2D, 0x2D, 0x30);
                SetGroupBoxBackground(DetailsGroup, 0x2D, 0x2D, 0x30);
                SetGroupBoxBackground(ToolsGroup, 0x2D, 0x2D, 0x30);

                // Set text colors to white
                SetTextForeground(HeaderText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(UserInfoText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(WelcomeText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(StatusText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(TokenInfoText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(SelectionCountText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(SelectionCategoriesText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(SelectionUpdateText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(ElementDetailsText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(SelectionMonitorText, 0xFF, 0xFF, 0xFF);

                // Set scroll viewer backgrounds
                LeftPanel.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
                RightPanel.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));

                ThemeToggleButton.Content = "☀️ Light";
            }
            else
            {
                // Apply light theme (default)
                MainGrid.Background = System.Windows.Media.Brushes.White;
                ContentGrid.Background = System.Windows.Media.Brushes.White;
                HeaderBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30));
                FooterBorder.Background = System.Windows.Media.Brushes.LightGray;
                SelectionHeader.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30));
                RightPanel.Background = System.Windows.Media.Brushes.White;
                LeftPanel.Background = System.Windows.Media.Brushes.White;

                // Reset to light theme colors
                SetGroupBoxBackground(WelcomeGroup, 0xF0, 0xF0, 0xF0);
                SetGroupBoxBackground(ActionsGroup, 0xF0, 0xF0, 0xF0);
                SetGroupBoxBackground(StatusGroup, 0xF0, 0xF0, 0xF0);
                SetGroupBoxBackground(StatsGroup, 0xF0, 0xF0, 0xF0);
                SetGroupBoxBackground(ElementsGroup, 0xF0, 0xF0, 0xF0);
                SetGroupBoxBackground(DetailsGroup, 0xF0, 0xF0, 0xF0);
                SetGroupBoxBackground(ToolsGroup, 0xF0, 0xF0, 0xF0);

                // Set text colors to black
                SetTextForeground(HeaderText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(UserInfoText, 0xFF, 0xFF, 0xFF);
                SetTextForeground(WelcomeText, 0x00, 0x00, 0x00);
                SetTextForeground(StatusText, 0x00, 0x00, 0x00);
                SetTextForeground(TokenInfoText, 0x80, 0x80, 0x80);
                SetTextForeground(SelectionCountText, 0x00, 0x00, 0x00);
                SetTextForeground(SelectionCategoriesText, 0x00, 0x00, 0x00);
                SetTextForeground(SelectionUpdateText, 0x00, 0x00, 0x00);
                SetTextForeground(ElementDetailsText, 0x00, 0x00, 0x00);
                SetTextForeground(SelectionMonitorText, 0x00, 0x00, 0x00);

                // Set scroll viewer backgrounds
                LeftPanel.Background = System.Windows.Media.Brushes.White;
                RightPanel.Background = System.Windows.Media.Brushes.White;

                ThemeToggleButton.Content = "🌙 Dark";
            }
        }

        private void SetGroupBoxBackground(GroupBox groupBox, byte r, byte g, byte b)
        {
            groupBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            groupBox.Foreground = _isDarkMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Black;
            groupBox.BorderBrush = _isDarkMode ?
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)) :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0xCC));
        }

        private void SetTextForeground(TextBlock textBlock, byte r, byte g, byte b)
        {
            textBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ApplyTheme();
            SaveSettings();
        }

        private void OnUserLoggedOut(object sender, EventArgs e)
        {
            if (!_isClosing)
            {
                Dispatcher.Invoke(() =>
                {
                    _isClosing = true;
                    Close();
                });
            }
        }

        // Fixed: Explicitly use System.Windows.Controls.SelectionChangedEventArgs
        private void OnElementsListViewSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ElementsListView.SelectedItem is RevitElement selectedElement)
            {
                ShowElementDetails(selectedElement);
            }
        }

        private void StartSelectionMonitoring()
        {
            if (_revitApp != null && !_isMonitoringSelection)
            {
                try
                {
                    _revitApp.Idling += OnIdling;
                    _isMonitoringSelection = true;
                    UpdateSelectionInfo();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting selection monitoring: {ex.Message}");
                }
            }
        }

        private void StopSelectionMonitoring()
        {
            if (_revitApp != null && _isMonitoringSelection)
            {
                try
                {
                    // Check if Revit is still in a valid state before removing event handler
                    if (_revitApp.ActiveUIDocument?.Document?.IsValidObject == true)
                    {
                        _revitApp.Idling -= OnIdling;
                    }
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    // Revit is not in API context - event handler will be auto-cleaned up
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error stopping selection monitoring: {ex.Message}");
                }
                finally
                {
                    _isMonitoringSelection = false;
                }
            }
        }

        private void OnIdling(object sender, IdlingEventArgs e)
        {
            // Check if window is still open and Revit is available
            if (_isClosing || _revitApp == null || _uiDoc == null)
                return;

            try
            {
                // Check for selection changes during idle time
                var currentSelection = _uiDoc.Selection.GetElementIds().ToList();

                // Compare with previous selection to avoid unnecessary updates
                if (!currentSelection.SequenceEqual(_previousSelection))
                {
                    _previousSelection = currentSelection;
                    Dispatcher.Invoke(() => UpdateSelectionInfo());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Idling event: {ex.Message}");
            }
        }

        private void UpdateSelectionInfo()
        {
            try
            {
                // Check if Revit objects are still valid
                if (_uiDoc?.Document?.IsValidObject != true)
                    return;

                SelectedElements.Clear();

                var selectedIds = _uiDoc.Selection.GetElementIds();
                SelectionCountText.Text = $"{selectedIds.Count} element(s) selected";

                if (selectedIds.Count > 0)
                {
                    var categories = new List<string>();
                    foreach (var id in selectedIds)
                    {
                        var element = _uiDoc.Document.GetElement(id);
                        if (element != null && element.IsValidObject)
                        {
                            var categoryName = element.Category?.Name ?? "No Category";
                            if (!categories.Contains(categoryName))
                                categories.Add(categoryName);

                            var revitElement = ConvertToRevitElement(element);
                            SelectedElements.Add(revitElement);
                        }
                    }

                    SelectionCategoriesText.Text = $"Categories: {string.Join(", ", categories)}";
                }
                else
                {
                    SelectionCategoriesText.Text = "Categories: None";
                }

                SelectionUpdateText.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                SelectionCountText.Text = "Error reading selection";
                SelectionCategoriesText.Text = $"Error: {ex.Message}";
            }
        }

        private RevitElement ConvertToRevitElement(Element element)
        {
            var revitElement = new RevitElement
            {
                Id = element.Id.ToString(),
                Category = element.Category?.Name ?? "No Category",
                Name = element.Name ?? "Unnamed",
                UniqueId = element.UniqueId
            };

            // Try to get family and type information
            if (element is FamilyInstance familyInstance)
            {
                revitElement.FamilyName = familyInstance.Symbol?.FamilyName ?? "N/A";
                revitElement.TypeName = familyInstance.Name ?? "N/A";
            }

            // Get level information
            revitElement.Level = GetElementLevel(element);

            // Try to get geometric properties
            try
            {
                var options = new Options();
                var geometry = element.get_Geometry(options);
                if (geometry != null)
                {
                    foreach (var geomObj in geometry)
                    {
                        if (geomObj is Solid solid)
                        {
                            revitElement.Volume = solid.Volume;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Ignore geometry errors
            }

            return revitElement;
        }

        private string GetElementLevel(Element element)
        {
            try
            {
                // Method 1: Check if element has LevelId property
                if (element.LevelId != ElementId.InvalidElementId)
                {
                    var level = _uiDoc.Document.GetElement(element.LevelId) as Level;
                    return level?.Name ?? "No Level";
                }

                // Method 2: Try to get level from parameters
                var levelParam = element.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                if (levelParam != null && levelParam.AsElementId() != ElementId.InvalidElementId)
                {
                    var level = _uiDoc.Document.GetElement(levelParam.AsElementId()) as Level;
                    return level?.Name ?? "No Level";
                }

                // Method 3: Try other common level parameters
                var levelParam2 = element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                if (levelParam2 != null && levelParam2.AsElementId() != ElementId.InvalidElementId)
                {
                    var level = _uiDoc.Document.GetElement(levelParam2.AsElementId()) as Level;
                    return level?.Name ?? "No Level";
                }

                // Method 4: Look for any parameter containing "Level"
                foreach (Parameter param in element.Parameters)
                {
                    if (param.Definition.Name.ToLower().Contains("level") &&
                        param.StorageType == StorageType.ElementId)
                    {
                        var levelId = param.AsElementId();
                        if (levelId != ElementId.InvalidElementId)
                        {
                            var level = _uiDoc.Document.GetElement(levelId) as Level;
                            return level?.Name ?? "No Level";
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return "No Level";
        }

        private void ShowElementDetails(RevitElement element)
        {
            var details = $"Element Details:\n\n" +
                         $"ID: {element.Id}\n" +
                         $"Category: {element.Category}\n" +
                         $"Name: {element.Name}\n" +
                         $"Family: {element.FamilyName ?? "N/A"}\n" +
                         $"Type: {element.TypeName ?? "N/A"}\n" +
                         $"Level: {element.Level ?? "N/A"}\n" +
                         $"Volume: {element.Volume:0.00} cubic units\n" +
                         $"Unique ID: {element.UniqueId}\n";

            ElementDetailsText.Text = details;
        }

        private void LoadUserInfo()
        {
            if (AuthenticationService.IsAuthenticated && AuthenticationService.CurrentUser != null)
            {
                var user = AuthenticationService.CurrentUser;
                var token = AuthenticationService.CurrentToken;

                UserInfoText.Text = $"Logged in as: {user.Username}";
                WelcomeText.Text = $"Welcome, {user.FullName}!\n\nSelect elements in Revit to see their information in real-time.";
                TokenInfoText.Text = $"Token expires: {token.ExpiresAt.ToLocalTime():f}";
            }
        }

        private async void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteActionAsync("Refreshing user data...", async () =>
            {
                await Task.Delay(1000);
                LoadUserInfo();
                StatusText.Text = "User data refreshed!";
            });
        }

        private void ViewProfile_Click(object sender, RoutedEventArgs e)
        {
            if (AuthenticationService.IsAuthenticated)
            {
                var user = AuthenticationService.CurrentUser;
                MessageBox.Show(
                    $"User Profile:\n\nName: {user.FullName}\nEmail: {user.Email}\nRole: {user.Role}",
                    "User Profile",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private async void CheckAllElements_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteActionAsync("Analyzing project elements...", async () =>
            {
                await Task.Run(() =>
                {
                    var doc = _uiDoc.Document;
                    var collector = new FilteredElementCollector(doc);
                    var allElements = collector.WhereElementIsNotElementType().ToElements();

                    var categoryCount = allElements
                        .Select(el => el.Category?.Name)
                        .Where(name => name != null)
                        .Distinct()
                        .Count();

                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = $"Project contains {allElements.Count} elements across {categoryCount} categories";
                    });
                });
            });
        }

        private void RefreshSelection_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectionInfo();
            StatusText.Text = "Selection refreshed!";
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _uiDoc.Selection.SetElementIds(new List<ElementId>());
                StatusText.Text = "Selection cleared!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error clearing selection: {ex.Message}";
            }
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to logout?",
                "Confirm Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                AuthenticationService.Logout();
                Close();
            }
        }

        private void StayOnTopCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            this.Topmost = StayOnTopCheckBox.IsChecked ?? false;
            SaveSettings(); // Save when changed
        }

        private async Task ExecuteActionAsync(string statusMessage, Func<Task> action)
        {
            StatusText.Text = statusMessage;
            ActionProgress.Visibility = System.Windows.Visibility.Visible;
            ActionProgress.IsIndeterminate = true;

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"An error occurred: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ActionProgress.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;

            // Save settings when closing
            SaveSettings();

            // Clean up event handlers
            AuthenticationService.UserLoggedOut -= OnUserLoggedOut;

            if (ElementsListView != null)
                ElementsListView.SelectionChanged -= OnElementsListViewSelectionChanged;

            StopSelectionMonitoring();

            base.OnClosed(e);
        }
    }
}