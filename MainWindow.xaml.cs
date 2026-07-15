using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics;
using VTStudioToolBox.Helpers;
using VTStudioToolBox.Views;

namespace VTStudioToolBox
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, Type> _pageRoutes = new()
        {
            ["dashboard"] = typeof(DashboardPage),
            ["utilities"] = typeof(UtilitiesPage),
            ["network"] = typeof(NetworkPage),
            ["macos"] = typeof(MacOSPage),
            ["android"] = typeof(AndroidPage),
            ["settings"] = typeof(SettingsPage),
        };

        private bool _hasCheckedEula = false;

        public MainWindow()
        {
            this.InitializeComponent();

            SetupImmersiveTitleBar();
            TryApplyBackdropEffect();
            SetWindowSize();
            SetWindowIcon();

            NavView.SelectionChanged += OnNavigationSelectionChanged;
            this.Activated += OnWindowActivated;

            // Update language after NavView is fully loaded
            NavView.Loaded += (s, e) => UpdateLanguage();
        }

        private void UpdateLanguage()
        {
            NavDashboard.Content = LanguageHelper.GetString("NavDashboard");
            NavUtilities.Content = LanguageHelper.GetString("NavUtilities");
            NavNetwork.Content = LanguageHelper.GetString("NavNetwork");
            NavHackintosh.Content = LanguageHelper.GetString("NavHackintosh");
            NavAndroid.Content = LanguageHelper.GetString("NavAndroid");

            if (NavView.SettingsItem is NavigationViewItem settingsItem)
            {
                settingsItem.Content = LanguageHelper.GetString("NavSettings");
            }

            EulaText.Inlines.Clear();
            EulaText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = LanguageHelper.GetString("EulaPrompt") + " " });
            var hyperlink = new Microsoft.UI.Xaml.Documents.Hyperlink
            {
                NavigateUri = new Uri("https://toolboxeula.vtstudio.space")
            };
            hyperlink.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = LanguageHelper.GetString("LabelUserAgreement") });
            EulaText.Inlines.Add(hyperlink);

            BtnAgree.Content = LanguageHelper.GetString("EulaAgree");
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

                if (EulaHelper.HasUserAgreed())
                {
                    Logger.Info("MainWindow", "EULA already agreed, starting normal flow");
                    StartNormalAppFlow();
                }
                else
                {
                    Logger.Info("MainWindow", "EULA not agreed, showing overlay");
                    EulaOverlay.Visibility = Visibility.Visible;
                    NavView.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void BtnAgree_Click(object sender, RoutedEventArgs e)
        {
            Logger.Dev("MainWindow", "EULA agree button clicked");
            _ = EulaHelper.SetUserAgreedAsync();
            Logger.Info("MainWindow", "User agreed to EULA");

            StartNormalAppFlow();
        }

        private void StartNormalAppFlow()
        {
            Logger.Dev("MainWindow", "Starting normal app flow");
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

        private void SetWindowIcon()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow?.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"));
            }
            catch { }
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
                string displayName = pageKey switch
                {
                    "dashboard" => "Dashboard",
                    "utilities" => "Utilities",
                    "network" => "Network",
                    "macos" => "Hackintosh",
                    "android" => "Android",
                    "settings" => "Settings",
                    _ => pageKey
                };
                Logger.Info("MainWindow", $"Navigating to {displayName}");
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