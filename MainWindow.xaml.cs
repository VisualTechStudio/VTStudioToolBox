using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using VTStudioToolBox.Views;

namespace VTStudioToolBox
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, Type> _pageRoutes = new()
        {
            ["dashboard"] = typeof(DashboardPage),
            ["settings"] = typeof(SettingsPage),
        };

        private const string EULA_FOLDER_NAME = "VTStudioToolBox";
        private const string EULA_FILE_NAME = "Eula.txt";
        private const string AGREED_CONTENT = "true";

        private bool _hasCheckedEula = false;

        public MainWindow()
        {
            this.InitializeComponent();

            SetupImmersiveTitleBar();
            TryApplyBackdropEffect();
            SetWindowSize();

            NavView.SelectionChanged += OnNavigationSelectionChanged;
            this.Activated += OnWindowActivated;
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState is not (WindowActivationState.CodeActivated or WindowActivationState.PointerActivated))
                return;

            this.Activated -= OnWindowActivated;

            SetWindowSize();

            if (!_hasCheckedEula)
            {
                _hasCheckedEula = true;

                if (HasUserAgreedToEula())
                {
                    StartNormalAppFlow();
                }
                else
                {
                    EulaOverlay.Visibility = Visibility.Visible;
                    NavView.Visibility = Visibility.Collapsed;
                }
            }
        }

        private bool HasUserAgreedToEula()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string folderPath = Path.Combine(localAppData, EULA_FOLDER_NAME);
                string filePath = Path.Combine(folderPath, EULA_FILE_NAME);

                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    return content.Trim().Equals(AGREED_CONTENT, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        private void BtnAgree_Click(object sender, RoutedEventArgs e)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string folderPath = Path.Combine(localAppData, EULA_FOLDER_NAME);
                    Directory.CreateDirectory(folderPath);
                    string filePath = Path.Combine(folderPath, EULA_FILE_NAME);
                    File.WriteAllText(filePath, AGREED_CONTENT);
                }
                catch { }
            });

            StartNormalAppFlow();
        }

        private void StartNormalAppFlow()
        {
            EulaOverlay.Visibility = Visibility.Collapsed;
            NavView.Visibility = Visibility.Visible;
            NavigateTo("dashboard");
        }

        private void SetupImmersiveTitleBar()
        {
            ExtendsContentIntoTitleBar = true;

            SetTitleBar(new Grid
            {
                Height = 32,
                Background = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Top
            });
        }

        private void TryApplyBackdropEffect()
        {
            try
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }
            catch { }
        }

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        private void SetWindowSize()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow == null) return;

                int dpi = GetDpiForWindow(hwnd);
                float scale = dpi > 0 ? dpi / 96f : 1f;

                const double desiredWidth = 1280;
                const double desiredHeight = 800;

                int width = (int)Math.Round(desiredWidth * scale);
                int height = (int)Math.Round(desiredHeight * scale);

                appWindow.Resize(new SizeInt32(width, height));

                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    int x = workArea.X + (workArea.Width - width) / 2;
                    int y = workArea.Y + (workArea.Height - height) / 2;
                    appWindow.Move(new PointInt32(x, y));
                }

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = true;
                    presenter.IsMaximizable = true;
                    presenter.IsMinimizable = true;
                }
            }
            catch { }
        }

        private void NavigateTo(string pageKey)
        {
            if (_pageRoutes.TryGetValue(pageKey, out var pageType))
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem == NavView.SettingsItem)
            {
                NavigateTo("settings");
                return;
            }

            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag && _pageRoutes.ContainsKey(tag))
            {
                NavigateTo(tag);
            }
        }
    }
}