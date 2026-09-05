using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        private bool _hasCheckedWizard = false;
        private int _wizardStep = 0; // 1-based: 1=Language, 2=Theme, 3=PawnIO
        private string _selectedLanguage = "en-US";
        private string _selectedTheme = "System";

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

            if (!_hasCheckedWizard)
            {
                _hasCheckedWizard = true;

                if (!SetupWizardHelper.HasCompleted())
                {
                    Logger.Info("MainWindow", "Setup wizard not completed, showing wizard");
                    ShowSetupWizard();
                    return;
                }
            }

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
            SetupWizardOverlay.Visibility = Visibility.Collapsed;
            EulaOverlay.Visibility = Visibility.Collapsed;
            NavView.Visibility = Visibility.Visible;
            NavView.RequestedTheme = ThemeHelper.CurrentTheme;
            NavigateTo("dashboard");
        }

        // ── Setup Wizard ──

        private bool _wizardAnimating = false;

        private async void ShowSetupWizard()
        {
            try
            {
                NavView.Visibility = Visibility.Collapsed;

                WizardVersionText.Text = $"v{Cfg.AppVersion}";
                WizardCopyrightText.Text = "© 2016-2026 VTStudio · GNU GPLv3";
                _selectedLanguage = DetectSystemLanguage();
                LanguageHelper.ApplyLanguage(_selectedLanguage);
                _wizardStep = 0; // no previous step
                ShowWizardStep(1, animate: false);

                SetupWizardOverlay.Visibility = Visibility.Visible;
                SetupWizardOverlay.Opacity = 0;

                var fadeIn = new DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(350),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(fadeIn, SetupWizardOverlay);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                var sb = new Storyboard();
                sb.Children.Add(fadeIn);
                sb.Begin();
            }
            catch (Exception ex) { Logger.Error("MainWindow", "ShowSetupWizard failed", ex); }
        }

        private string DetectSystemLanguage()
        {
            var available = LanguageHelper.GetAvailableLanguages();
            string sysLang = CultureInfo.CurrentUICulture.Name; // e.g. "zh-CN", "en-US", "ja"

            // Exact match
            if (available.ContainsKey(sysLang))
                return sysLang;

            // Match by prefix (e.g. "zh" matches "zh-CN")
            string prefix = sysLang.Split('-')[0];
            foreach (var key in available.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return key;
            }

            // Default to English
            return "en-US";
        }

        private async void ShowWizardStep(int step, bool animate = true)
        {
            try
            {
                if (_wizardAnimating) return;

                int oldStep = _wizardStep;
                bool forward = step > oldStep;

                // Update content
                switch (step)
                {
                    case 1:
                        WizardTitle.Text = LanguageHelper.GetString("WizardWelcomeTitle");
                        WizardSubtitle.Text = LanguageHelper.GetString("WizardWelcomeSubtitle");
                        WizardSubtitle.Visibility = Visibility.Visible;
                        BtnWizardBack.Visibility = Visibility.Collapsed;
                        BtnWizardNext.Visibility = Visibility.Visible;
                        BtnWizardNext.Content = "Next →";
                        break;
                    case 2:
                        WizardTitle.Text = "Language / 语言";
                        WizardSubtitle.Text = "Select your preferred language";
                        WizardSubtitle.Visibility = Visibility.Visible;
                        InitLanguageStep();
                        BtnWizardBack.Visibility = Visibility.Visible;
                        BtnWizardBack.Content = "← Back";
                        BtnWizardNext.Visibility = Visibility.Visible;
                        BtnWizardNext.Content = "Next →";
                        break;
                    case 3:
                        WizardTitle.Text = LanguageHelper.GetString("WizardThemeTitle");
                        WizardSubtitle.Text = LanguageHelper.GetString("WizardThemeSubtitle");
                        WizardSubtitle.Visibility = Visibility.Visible;
                        InitThemeStep();
                        BtnWizardBack.Visibility = Visibility.Visible;
                        BtnWizardBack.Content = LanguageHelper.GetString("WizardBack");
                        BtnWizardNext.Visibility = Visibility.Visible;
                        BtnWizardNext.Content = LanguageHelper.GetString("WizardNext");
                        break;
                    case 4:
                        WizardTitle.Text = LanguageHelper.GetString("WizardDriverTitle");
                        WizardSubtitle.Text = LanguageHelper.GetString("WizardDriverSubtitle");
                        WizardSubtitle.Visibility = Visibility.Visible;
                        InitPawnIOStep();
                        BtnWizardBack.Visibility = Visibility.Visible;
                        BtnWizardBack.Content = LanguageHelper.GetString("WizardBack");
                        BtnWizardNext.Visibility = Visibility.Collapsed;
                        break;
                }

                UpdateStepDots(step);

                if (animate && oldStep > 0 && oldStep != step)
                {
                    _wizardAnimating = true;
                    await AnimateStepTransition(oldStep, step, forward);
                    _wizardAnimating = false;
                }
                else
                {
                    GetStepContent(step).Visibility = Visibility.Visible;
                }

                _wizardStep = step;
            }
            catch (Exception ex) { Logger.Error("MainWindow", "ShowWizardStep failed", ex); }
        }

        private FrameworkElement GetStepContent(int step) => step switch
        {
            1 => WizardStep1Content,
            2 => WizardStep2Content,
            3 => WizardStep3Content,
            4 => WizardStep4Content,
            _ => WizardStep1Content
        };

        private void UpdateStepDots(int activeStep)
        {
            var activeBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            var inactiveBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];

            Dot1.Background = activeStep >= 1 ? activeBrush : inactiveBrush;
            Dot2.Background = activeStep >= 2 ? activeBrush : inactiveBrush;
            Dot3.Background = activeStep >= 3 ? activeBrush : inactiveBrush;
            Dot4.Background = activeStep >= 4 ? activeBrush : inactiveBrush;
        }

        private async Task AnimateStepTransition(int fromStep, int toStep, bool forward)
        {
            var from = GetStepContent(fromStep);
            var to = GetStepContent(toStep);

            double slideOut = forward ? -40 : 40;
            double slideIn = forward ? 40 : -40;

            if (from.RenderTransform is not TranslateTransform)
                from.RenderTransform = new TranslateTransform();
            if (to.RenderTransform is not TranslateTransform)
                to.RenderTransform = new TranslateTransform();

            ((TranslateTransform)to.RenderTransform).X = slideIn;
            to.Opacity = 0;
            to.Visibility = Visibility.Visible;

            // Animate FROM out
            var sbOut = new Storyboard();

            var outOpacity = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(outOpacity, from);
            Storyboard.SetTargetProperty(outOpacity, "Opacity");
            sbOut.Children.Add(outOpacity);

            var outTranslate = new DoubleAnimation
            {
                To = slideOut,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(outTranslate, from.RenderTransform);
            Storyboard.SetTargetProperty(outTranslate, "X");
            sbOut.Children.Add(outTranslate);

            var tcs = new TaskCompletionSource();
            sbOut.Completed += (_, _) =>
            {
                from.Visibility = Visibility.Collapsed;
                from.Opacity = 1;
                ((TranslateTransform)from.RenderTransform).X = 0;

                // Animate TO in
                var sbIn = new Storyboard();

                var inOpacity = new DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(inOpacity, to);
                Storyboard.SetTargetProperty(inOpacity, "Opacity");
                sbIn.Children.Add(inOpacity);

                var inTranslate = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(inTranslate, to.RenderTransform);
                Storyboard.SetTargetProperty(inTranslate, "X");
                sbIn.Children.Add(inTranslate);

                sbIn.Completed += (_, _) => tcs.SetResult();
                sbIn.Begin();
            };
            sbOut.Begin();

            await tcs.Task;
        }

        private void InitLanguageStep()
        {
            LanguageLabel.Text = "Language / 语言";
            var available = LanguageHelper.GetAvailableLanguages();
            LanguageComboBox.Items.Clear();

            foreach (var kv in available)
            {
                var item = new ComboBoxItem
                {
                    Content = kv.Value,
                    Tag = kv.Key
                };
                LanguageComboBox.Items.Add(item);
                if (kv.Key == _selectedLanguage)
                    LanguageComboBox.SelectedItem = item;
            }

            // Fallback: add en-US if not present
            if (LanguageComboBox.SelectedItem == null && LanguageComboBox.Items.Count > 0)
                LanguageComboBox.SelectedIndex = 0;

            LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string lang)
            {
                _selectedLanguage = lang;
                LanguageHelper.ApplyLanguage(lang);
            }
        }

        private void InitThemeStep()
        {
            ThemeSystemLabel.Text = LanguageHelper.GetString("WizardThemeFollowSystem");
            ThemeLightLabel.Text = LanguageHelper.GetString("WizardThemeLight");
            ThemeDarkLabel.Text = LanguageHelper.GetString("WizardThemeDark");
            UpdateThemeCardVisuals();
        }

        private void ThemeCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is string theme)
            {
                _selectedTheme = theme;
                UpdateThemeCardVisuals();
            }
        }

        private void UpdateThemeCardVisuals()
        {
            var accent = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            var defaultBorder = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

            ThemeCardSystem.BorderBrush = _selectedTheme == "System" ? accent : defaultBorder;
            ThemeCardLight.BorderBrush = _selectedTheme == "Light" ? accent : defaultBorder;
            ThemeCardDark.BorderBrush = _selectedTheme == "Dark" ? accent : defaultBorder;

            // Scale effect on selected card
            double selectedScale = 1.0;
            double normalScale = 1.0;
            ThemeCardSystem.RenderTransform = new ScaleTransform { ScaleX = normalScale, ScaleY = normalScale };
            ThemeCardLight.RenderTransform = new ScaleTransform { ScaleX = normalScale, ScaleY = normalScale };
            ThemeCardDark.RenderTransform = new ScaleTransform { ScaleX = normalScale, ScaleY = normalScale };
        }

        private bool IsPawnIOInstalled()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Name FROM Win32_SystemDriver WHERE Name = 'PawnIO'");
                foreach (var obj in searcher.Get())
                {
                    obj.Dispose();
                    return true;
                }
            }
            catch (Exception ex) { Logger.Warn("MainWindow", $"IsPawnIOInstalled query failed: {ex.Message}"); }
            return false;
        }

        private void InitPawnIOStep()
        {
            if (IsPawnIOInstalled())
            {
                PawnIOStepIcon.Glyph = "\xE73E"; // CheckMark
                PawnIOStepIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                PawnIODescription.Text = LanguageHelper.GetString("WizardPawnIODescInstalled");
                BtnInstallPawnIO.Content = LanguageHelper.GetString("WizardPawnIOWebsite");
                BtnSkipPawnIO.Text = LanguageHelper.GetString("WizardPawnIOFinished");
                // Re-wire click to open website instead of installing
                BtnInstallPawnIO.Click -= BtnInstallPawnIO_Click;
                BtnInstallPawnIO.Click += BtnPawnIOWebsite_Click;
            }
            else
            {
                PawnIOStepIcon.Glyph = "\xE72E"; // Lock
                PawnIOStepIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
                PawnIODescription.Text = LanguageHelper.GetString("WizardPawnIODesc");
                BtnInstallPawnIO.Content = LanguageHelper.GetString("WizardPawnIOInstall");
                BtnSkipPawnIO.Text = LanguageHelper.GetString("WizardPawnIOSkip");
                // Wire click to install handler
                BtnInstallPawnIO.Click -= BtnPawnIOWebsite_Click;
                BtnInstallPawnIO.Click -= BtnInstallPawnIO_Click;
                BtnInstallPawnIO.Click += BtnInstallPawnIO_Click;
            }
        }

        private void BtnWizardBack_Click(object sender, RoutedEventArgs e)
        {
            if (_wizardStep > 1)
                ShowWizardStep(_wizardStep - 1);
        }

        private void BtnWizardNext_Click(object sender, RoutedEventArgs e)
        {
            if (_wizardStep == 1)
            {
                ShowWizardStep(2);
            }
            else if (_wizardStep == 2)
            {
                LanguageHelper.ApplyLanguage(_selectedLanguage);
                ShowWizardStep(3);
            }
            else if (_wizardStep == 3)
            {
                ApplyWizardTheme();
                ShowWizardStep(4);
            }
        }

        private void ApplyWizardTheme()
        {
            switch (_selectedTheme)
            {
                case "System":
                    ThemeHelper.ApplyFollowSystem();
                    break;
                case "Light":
                    ThemeHelper.ApplyTheme(ElementTheme.Light);
                    break;
                case "Dark":
                    ThemeHelper.ApplyTheme(ElementTheme.Dark);
                    break;
            }
        }

        private async void BtnInstallPawnIO_Click(object sender, RoutedEventArgs e)
        {
            string pawnioPath = Path.Combine(AppContext.BaseDirectory, "Tools", "PawnIO", "install.bat");
            if (File.Exists(pawnioPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pawnioPath,
                        UseShellExecute = true,
                        Verb = "runas"
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error("MainWindow", "Failed to launch PawnIO installer", ex);
                }
            }
            else
            {
                var dlg = new ContentDialog
                {
                    Title = LanguageHelper.GetString("WizardPawnIONotFound"),
                    Content = LanguageHelper.GetString("WizardPawnIONotFoundDesc"),
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dlg.ShowAsync();
            }
            CompleteWizard();
        }

        private void BtnSkipPawnIO_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CompleteWizard();
        }

        private async void BtnPawnIOWebsite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://pawnio.eu/"));
            }
            catch (Exception ex) { Logger.Warn("MainWindow", $"Failed to open PawnIO website: {ex.Message}"); }
            CompleteWizard();
        }

        private void CompleteWizard()
        {
            SetupWizardHelper.MarkCompleted();
            Logger.Info("MainWindow", "Setup wizard completed");

            if (EulaHelper.HasUserAgreed())
            {
                StartNormalAppFlow();
            }
            else
            {
                SetupWizardOverlay.Visibility = Visibility.Collapsed;
                _hasCheckedEula = true;
                EulaOverlay.Visibility = Visibility.Visible;
                UpdateLanguage();
            }
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
            catch (Exception ex) { Logger.Warn("MainWindow", $"UpdateTitleBarButtons failed: {ex.Message}"); }
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
            catch (Exception ex) { Logger.Warn("MainWindow", $"SetWindowIcon failed: {ex.Message}"); }
        }

        private void TryApplyBackdropEffect()
        {
            try
            {
                SystemBackdrop = new MicaBackdrop();
            }
            catch (Exception ex) { Logger.Warn("MainWindow", $"TryApplyBackdropEffect failed: {ex.Message}"); }
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
            catch (Exception ex) { Logger.Warn("MainWindow", $"SetWindowSize failed: {ex.Message}"); }
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
                    using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    stream.AsStreamForWrite().Write(bytes, 0, bytes.Length);
                    stream.Seek(0);
                    bitmap.SetSource(stream);
                    return bitmap;
                }
                return new BitmapImage(new Uri(url));
            }
            catch (Exception ex) { Logger.Warn("MainWindow", $"LoadAvatarImage failed: {ex.Message}"); return null; }
        }
    }
}