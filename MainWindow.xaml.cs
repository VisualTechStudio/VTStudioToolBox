using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics;
using VTStudioToolBox.Helpers;
using VTStudioToolBox.Models;
using VTStudioToolBox.ViewModels;
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

        public UserViewModel UserVM { get; }

        public MainWindow()
        {
            UserVM = App.Services.GetService(typeof(UserViewModel)) as UserViewModel
                     ?? throw new InvalidOperationException("UserViewModel not registered.");
            this.InitializeComponent();

            SetupImmersiveTitleBar();
            TryApplyBackdropEffect();
            SetWindowSize();
            SetWindowIcon();

            NavView.SelectionChanged += OnNavigationSelectionChanged;
            this.Activated += OnWindowActivated;
            ThemeHelper.ThemeChanged += OnThemeChanged;

            // Update language after NavView is fully loaded
            NavView.Loaded += (s, e) => UpdateLanguage();

            // Update avatar UI when user state changes
            UserVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(UserViewModel.IsLoggedIn) or nameof(UserViewModel.AvatarUrl))
                    UpdateAvatarVisual();
            };

            // Restore cached login state to UI
            UpdateAvatarVisual();
        }

        private string _currentPageKey = "dashboard";

        private void OnThemeChanged()
        {
            NavView.RequestedTheme = ThemeHelper.CurrentTheme;
            NavigateTo(_currentPageKey);
            UpdateTitleBarButtons();
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
            NavView.RequestedTheme = ThemeHelper.CurrentTheme;
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

            UpdateTitleBarButtons();
        }

        private void UpdateTitleBarButtons()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                if (appWindow?.TitleBar is not { } titleBar) return;

                bool isDark = ThemeHelper.CurrentTheme == ElementTheme.Dark;
                titleBar.ButtonForegroundColor = isDark ? Colors.White : Colors.Black;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverForegroundColor = isDark ? Colors.White : Colors.Black;
                titleBar.ButtonHoverBackgroundColor = isDark
                    ? Windows.UI.Color.FromArgb(40, 255, 255, 255)
                    : Windows.UI.Color.FromArgb(40, 0, 0, 0);
                titleBar.ButtonPressedForegroundColor = isDark ? Colors.White : Colors.Black;
                titleBar.ButtonPressedBackgroundColor = isDark
                    ? Windows.UI.Color.FromArgb(30, 255, 255, 255)
                    : Windows.UI.Color.FromArgb(30, 0, 0, 0);
            }
            catch { }
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
                SystemBackdrop = new MicaBackdrop();
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
                _currentPageKey = pageKey;
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

        // ── Avatar / Auth UI ──

        private Flyout? _loginFlyout;
        private Flyout? _profileFlyout;

        private void BtnAvatar_Click(object sender, RoutedEventArgs e)
        {
            if (UserVM.IsLoggedIn)
                ShowProfileFlyout();
            else
                ShowLoginFlyout();
        }

        private void ShowLoginFlyout()
        {
            if (_loginFlyout is null)
            {
                _loginFlyout = new Flyout { Placement = FlyoutPlacementMode.Right };

                var panel = new StackPanel { Width = 220, Spacing = 8, RequestedTheme = ThemeHelper.CurrentTheme };

                bool isDarkLogin = ThemeHelper.CurrentTheme == ElementTheme.Dark;
                panel.Children.Add(new TextBlock
                {
                    Text = LanguageHelper.GetString("AuthSignInWith"),
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = isDarkLogin
                        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
                        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 26, 26, 26)),
                    Margin = new Thickness(0, 0, 0, 8)
                });

                AddProviderButton(panel, "GitHub", "\xF0E1");
                AddProviderButton(panel, "Microsoft", "\xF259");
                AddProviderButton(panel, "Steam", "\xF190");

                _loginFlyout.Content = panel;
            }

            _loginFlyout.ShowAt(BtnAvatar);
        }

        private void AddProviderButton(StackPanel panel, string name, string glyph)
        {
            var btn = new Button
            {
                Tag = name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 10, 12, 10)
            };

            var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            inner.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16 });
            inner.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
            btn.Content = inner;

            btn.Click += async (_, _) =>
            {
                _loginFlyout?.Hide();
                if (Enum.TryParse<AuthProvider>(name, out var provider))
                {
                    try
                    {
                        Logger.Info("MainWindow", $"Starting login for {provider}...");
                        await UserVM.LoginAsync(provider);
                        UpdateAvatarVisual();
                        Logger.Info("MainWindow", $"Login completed for {provider}, IsLoggedIn={UserVM.IsLoggedIn}");

                        if (UserVM.IsLoggedIn)
                            ShowProfileFlyout();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Login failed for {provider}", ex);
                        var errorDlg = new ContentDialog
                        {
                            Title = $"Login Failed - {provider}",
                            Content = $"{ex.GetType().Name}:\n{ex.Message}",
                            CloseButtonText = "OK",
                            XamlRoot = this.Content.XamlRoot
                        };
                        _ = errorDlg.ShowAsync();
                    }
                }
            };

            panel.Children.Add(btn);
        }

        private void ShowProfileFlyout()
        {
            // Recreate each time to reflect current user data
            _profileFlyout = new Flyout { Placement = FlyoutPlacementMode.Right };

            var panel = new StackPanel { Width = 260, Spacing = 12, RequestedTheme = ThemeHelper.CurrentTheme };

            // Resolve brushes from themed resource dictionaries
            bool isDark = ThemeHelper.CurrentTheme == ElementTheme.Dark;
            var primaryBrush = isDark
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 26, 26, 26));
            var secondaryBrush = isDark
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 204, 204, 204))
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 68, 68, 68));
            var dividerBrush = isDark
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 68, 68, 68))
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 224, 224, 224));

            // Header: avatar + info
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            var avatarEllipse = new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 48, Height = 48 };
            var avatarImg = LoadAvatarImage(UserVM.AvatarUrl);
            if (avatarImg != null)
                avatarEllipse.Fill = new ImageBrush { ImageSource = avatarImg };
            header.Children.Add(avatarEllipse);

            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
            infoPanel.Children.Add(new TextBlock
            {
                Text = UserVM.DisplayName,
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = primaryBrush
            });
            infoPanel.Children.Add(new TextBlock
            {
                Text = $"{UserVM.Provider} \u00B7 ID: {UserVM.UserId}",
                FontSize = 12,
                Foreground = secondaryBrush
            });
            header.Children.Add(infoPanel);
            panel.Children.Add(header);

            // Separator
            panel.Children.Add(new Border
            {
                Height = 1,
                Background = dividerBrush,
                Margin = new Thickness(0, 4, 0, 4)
            });

            // Logout button
            var logoutBtn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(12, 8, 12, 8)
            };
            var logoutInner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            logoutInner.Children.Add(new FontIcon { Glyph = "\xE7E8", FontSize = 14 });
            logoutInner.Children.Add(new TextBlock { Text = LanguageHelper.GetString("AuthSignOut"), VerticalAlignment = VerticalAlignment.Center });
            logoutBtn.Content = logoutInner;

            logoutBtn.Click += async (_, _) =>
            {
                _profileFlyout?.Hide();
                await UserVM.LogoutAsync();
                UpdateAvatarVisual();
            };
            panel.Children.Add(logoutBtn);

            _profileFlyout.Content = panel;
            _profileFlyout.ShowAt(BtnAvatar);
        }

        private void UpdateAvatarVisual()
        {
            bool loggedIn = UserVM.IsLoggedIn;
            bool hasAvatar = !string.IsNullOrEmpty(UserVM.AvatarUrl);

            if (loggedIn && hasAvatar)
            {
                IconAvatarFallback.Visibility = Visibility.Collapsed;
                AvatarImage.Visibility = Visibility.Visible;
                AvatarBrush.ImageSource = LoadAvatarImage(UserVM.AvatarUrl);
            }
            else
            {
                IconAvatarFallback.Visibility = Visibility.Visible;
                AvatarImage.Visibility = Visibility.Collapsed;
            }

            TxtDisplayName.Text = loggedIn
                ? UserVM.DisplayName
                : LanguageHelper.GetString("AuthNotLoggedIn");

            ToolTipService.SetToolTip(BtnAvatar,
                loggedIn ? UserVM.DisplayName : LanguageHelper.GetString("AuthSignIn"));
        }

        private static ImageSource? LoadAvatarImage(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    // data:image/jpeg;base64,... → decode to BitmapImage
                    int comma = url.IndexOf(',');
                    string base64 = url[(comma + 1)..];
                    byte[] bytes = Convert.FromBase64String(base64);
                    var bitmap = new BitmapImage();
                    var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    stream.AsStreamForWrite().Write(bytes, 0, bytes.Length);
                    stream.Seek(0);
                    bitmap.SetSource(stream);
                    return bitmap;
                }
                return new BitmapImage(new Uri(url));
            }
            catch { return null; }
        }
    }
}