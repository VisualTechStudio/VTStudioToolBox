using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;
using VTStudioToolBox.Helpers;

namespace VTStudioToolBox.Views
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isInitializing = true;
        private FrameworkElement[] _allViews = null!;

        public SettingsPage()
        {
            this.InitializeComponent();
            _allViews = new FrameworkElement[]
            {
                RootView, LanguageView, ThemeView, DashboardView,
                PrivacyView, ChangelogView, CreditsView
            };
            UpdateLanguage();

            VersionTextBlock.Text = LanguageHelper.GetString("VersionLabel", Cfg.AppVersion);
            LogTextBlock.Text = $"{ChangeLog.Log}";
            BuildContributionsSection();

            // Initialize log level
            int levelIndex = (int)Logger.MinLevel;
            LogLevelComboBox.SelectedIndex = levelIndex;

            // Initialize language selector
            InitLanguageSettings();

            // Initialize theme selector
            InitThemeSettings();

            // Initialize refresh interval
            InitRefreshInterval();

            ThemeHelper.ThemeChanged += OnThemeChanged;
            this.Unloaded += (_, _) => ThemeHelper.ThemeChanged -= OnThemeChanged;
            _isInitializing = false;
        }

        private void OnThemeChanged()
        {
            ContributionsStack.Children.Clear();
            BuildContributionsSection();
        }

        // ────────────────────── Navigation ──────────────────────

        private void ShowView(FrameworkElement target)
        {
            ShowViewAnimated(target, isForward: target != RootView);
        }

        private void ShowViewAnimated(FrameworkElement target, bool isForward)
        {
            foreach (var view in _allViews)
            {
                if (view != target)
                    view.Visibility = Visibility.Collapsed;
            }

            target.Visibility = Visibility.Visible;
            target.Opacity = 0;

            var transform = target.RenderTransform as TranslateTransform ?? new TranslateTransform();
            target.RenderTransform = transform;

            double slideFrom = isForward ? 60 : -60;
            transform.X = slideFrom;

            // Opacity animation
            var fadeIn = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, target);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

            // Slide animation
            var slide = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(slide, transform);
            Storyboard.SetTargetProperty(slide, "X");

            var sb = new Storyboard();
            sb.Children.Add(fadeIn);
            sb.Children.Add(slide);
            sb.Begin();
        }

        private void BackToRoot(object sender, RoutedEventArgs e) => ShowView(RootView);
        private void OpenLanguage(object sender, RoutedEventArgs e) => ShowView(LanguageView);
        private void OpenTheme(object sender, RoutedEventArgs e) => ShowView(ThemeView);
        private void OpenDashboard(object sender, RoutedEventArgs e) => ShowView(DashboardView);
        private void OpenPrivacy(object sender, RoutedEventArgs e) => ShowView(PrivacyView);
        private void OpenCredits(object sender, RoutedEventArgs e) => ShowView(CreditsView);
        private void OpenChangelog(object sender, RoutedEventArgs e) => ShowView(ChangelogView);

        // ────────────────────── Language ──────────────────────

        private void UpdateLanguage()
        {
            PageTitle.Text = LanguageHelper.GetString("SettingsTitle");
            PageSubtitle.Text = LanguageHelper.GetString("SettingsSubtitle");

            // 首级设置入口
            BackText.Text = LanguageHelper.GetString("ButtonBack");
            LangEntryTitle.Text = LanguageHelper.GetString("LabelLanguage");
            LangEntryDesc.Text = LanguageHelper.GetString("SettingsLangDesc");
            ThemeEntryTitle.Text = LanguageHelper.GetString("LabelTheme");
            ThemeEntryDesc.Text = LanguageHelper.GetString("SettingsThemeDesc");
            DashboardEntryTitle.Text = LanguageHelper.GetString("SectionDashboard");
            DashboardEntryDesc.Text = LanguageHelper.GetString("SettingsDashboardDesc");
            PrivacyEntryTitle.Text = LanguageHelper.GetString("SectionPrivacy");
            PrivacyEntryDesc.Text = LanguageHelper.GetString("SettingsPrivacyDesc");
            CreditsEntryTitle.Text = LanguageHelper.GetString("Contributors");
            CreditsEntryDesc.Text = LanguageHelper.GetString("SettingsCreditsDesc");
            ChangelogEntryTitle.Text = LanguageHelper.GetString("SectionChangelog");
            ChangelogEntryDesc.Text = LanguageHelper.GetString("SettingsChangelogDesc");

            // 二级页面标题
            LanguagePageTitle.Text = LanguageHelper.GetString("LabelLanguage");
            ThemePageTitle.Text = LanguageHelper.GetString("LabelTheme");
            DashboardPageTitle.Text = LanguageHelper.GetString("SectionDashboard");
            PrivacyPageTitle.Text = LanguageHelper.GetString("SectionPrivacy");
            CreditsPageTitle.Text = LanguageHelper.GetString("Contributors");
            ChangelogPageTitle.Text = LanguageHelper.GetString("SectionChangelog");

            // 主题选项
            ThemeSystemOption.Content = LanguageHelper.GetString("ThemeSystem");
            ThemeDarkOption.Content = LanguageHelper.GetString("ThemeDark");
            ThemeLightOption.Content = LanguageHelper.GetString("ThemeLight");

            // 隐私与日志
            AnalyticsToggle.Header = LanguageHelper.GetString("AnalyticsToggle");
            AnalyticsDescription.Text = LanguageHelper.GetString("AnalyticsDescription");
            LogHeader.Text = LanguageHelper.GetString("SectionLog");
            LogLevelLabel.Text = LanguageHelper.GetString("LabelLogLevel");
            FeedbackButtonText.Text = LanguageHelper.GetString("ButtonFeedback");

            // 关于
            WebsiteButtonText.Text = LanguageHelper.GetString("ButtonWebsite");
            GitHubButtonText.Text = LanguageHelper.GetString("ButtonGitHub");
            GPLButtonText.Text = LanguageHelper.GetString("ButtonGPL");
            RevokeEulaButton.Content = LanguageHelper.GetString("ButtonRevokeEULA");
            RestartWizardButtonText.Text = LanguageHelper.GetString("ButtonRestartWizard");
            ContributorsHeader.Text = LanguageHelper.GetString("Contributors");
            ContributionsHeader.Text = LanguageHelper.GetString("SectionContributions");
            ContributionsIntro.Text = LanguageHelper.GetString("ContributionsIntro");
            RefreshIntervalLabel.Text = LanguageHelper.GetString("LabelRefreshInterval");

            // Copyright and GPL
            CopyrightText.Inlines.Clear();
            CopyrightText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = LanguageHelper.GetString("LabelCopyright") });
            var hyperlink = new Microsoft.UI.Xaml.Documents.Hyperlink
            {
                NavigateUri = new Uri("https://vtstudio.space")
            };
            hyperlink.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "VisualTechStudio" });
            CopyrightText.Inlines.Add(hyperlink);
            CopyrightText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = ". " + LanguageHelper.GetString("LabelEulaBefore") + " " });
            var eulaLink = new Microsoft.UI.Xaml.Documents.Hyperlink();
            eulaLink.Click += async (s, args) =>
            {
                try
                {
                    string path = System.IO.Path.Combine(AppContext.BaseDirectory, "EULA.html");
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(path));
                }
                catch (Exception ex) { Logger.Warn("Settings", $"Failed to open EULA: {ex.Message}"); }
            };
            eulaLink.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = LanguageHelper.GetString("LabelUserAgreement") });
            CopyrightText.Inlines.Add(eulaLink);
            CopyrightText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "." });

            GPLStatement.Text = LanguageHelper.GetString("GPLStatement");

            // Contributor roles
            ContributorRole_Dwg.Text = LanguageHelper.GetString("ContributorRole_Dwg");
            ContributorRole_Xcy.Text = LanguageHelper.GetString("ContributorRole_Xcy");
            ContributorRole_Yue.Text = LanguageHelper.GetString("ContributorRole_Yue");
        }

        // ────────────────────── Init ──────────────────────

        private void InitLanguageSettings()
        {
            LanguageComboBox.Items.Clear();

            var languages = LanguageHelper.GetAvailableLanguages();
            string currentLang = LanguageHelper.CurrentLanguage;

            foreach (var lang in languages)
            {
                var item = new ComboBoxItem
                {
                    Content = lang.Value,
                    Tag = lang.Key
                };
                LanguageComboBox.Items.Add(item);

                if (lang.Key == currentLang)
                {
                    LanguageComboBox.SelectedItem = item;
                }
            }
        }

        private void InitThemeSettings()
        {
            if (ThemeHelper.IsFollowingSystem)
                ThemeComboBox.SelectedItem = ThemeSystemOption;
            else if (ThemeHelper.CurrentTheme == Microsoft.UI.Xaml.ElementTheme.Dark)
                ThemeComboBox.SelectedItem = ThemeDarkOption;
            else
                ThemeComboBox.SelectedItem = ThemeLightOption;
        }

        private void InitRefreshInterval()
        {
            int current = DashboardSettings.RefreshIntervalMs;
            foreach (ComboBoxItem item in RefreshIntervalComboBox.Items)
            {
                if (item.Tag is string tag && int.TryParse(tag, out int ms) && ms == current)
                {
                    RefreshIntervalComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        // ────────────────────── Settings Handlers ──────────────────────

        private async void RefreshIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (RefreshIntervalComboBox?.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int ms))
            {
                Logger.UserAction("RefreshIntervalChanged", $"{ms}ms");
                if (ms <= 500 && !DashboardSettings.SuppressHighRefreshWarning)
                {
                    var dialog = new ContentDialog
                    {
                        Title = LanguageHelper.GetString("HighRefreshWarningTitle"),
                        Content = LanguageHelper.GetString("HighRefreshWarningMessage"),
                        PrimaryButtonText = LanguageHelper.GetString("ButtonContinue"),
                        SecondaryButtonText = LanguageHelper.GetString("ButtonCancel"),
                        DefaultButton = ContentDialogButton.Secondary,
                        XamlRoot = this.XamlRoot
                    };

                    var checkBox = new CheckBox
                    {
                        Content = LanguageHelper.GetString("DontShowAgain"),
                        Margin = new Thickness(0, 12, 0, 0)
                    };
                    dialog.Content = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = LanguageHelper.GetString("HighRefreshWarningMessage"), TextWrapping = TextWrapping.Wrap },
                            checkBox
                        }
                    };

                    var result = await dialog.ShowAsync();
                    if (checkBox.IsChecked == true)
                        DashboardSettings.SetSuppressHighRefreshWarning(true);

                    if (result == ContentDialogResult.Primary)
                    {
                        DashboardSettings.SetRefreshInterval(ms);
                    }
                    else
                    {
                        _isInitializing = true;
                        InitRefreshInterval();
                        _isInitializing = false;
                        return;
                    }
                }
                else
                {
                    DashboardSettings.SetRefreshInterval(ms);
                }
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (ThemeComboBox == null || ThemeComboBox.SelectedItem == null) return;

            if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                Logger.UserAction("ThemeChanged", tag);
                if (tag == "System")
                {
                    ThemeHelper.ApplyFollowSystem();
                }
                else if (Enum.TryParse<Microsoft.UI.Xaml.ElementTheme>(tag, out var theme))
                {
                    ThemeHelper.ApplyTheme(theme);
                }
            }
        }

        private void LogLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (LogLevelComboBox == null || LogLevelComboBox.SelectedItem == null) return;
            if (LogLevelComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (int.TryParse(tag, out int level))
                {
                    Logger.UserAction("LogLevelChanged", ((LogLevel)level).ToString());
                    Logger.MinLevel = (LogLevel)level;
                }
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (LanguageComboBox == null || LanguageComboBox.SelectedItem == null) return;
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (tag != LanguageHelper.CurrentLanguage)
                {
                    Logger.UserAction("LanguageChanged", $"{LanguageHelper.CurrentLanguage} → {tag}");
                    LanguageHelper.ApplyLanguage(tag);
                    LanguageHelper.RestartApp();
                }
            }
        }

        private async void FeedbackButton_Click(object sender, RoutedEventArgs e)
            => await OpenUrlAsync($"{Cfg.GithubRepo}/issues", LanguageHelper.GetString("ErrorCannotOpenFeedback"));

        // ────────────────────── Contributions ──────────────────────

        private void BuildContributionsSection()
        {
            var contributions = new (string Developer, (string Name, bool IsOpenSource, string Url)[] Projects)[]
            {
                ("Microsoft", new[]
                {
                    ("Microsoft.Management.Infrastructure", true, "https://github.com/microsoft/omi"),
                    ("Microsoft.Windows.SDK.BuildTools", true, "https://github.com/microsoft/WindowsAppSDK"),
                    ("Microsoft.WindowsAppSDK", true, "https://github.com/microsoft/WindowsAppSDK"),
                    ("System.Drawing.Common", true, "https://github.com/dotnet/runtime"),
                    ("System.Management", true, "https://github.com/dotnet/runtime"),
                }),
                ("SharpDX Team", new[] { ("SharpDX", true, "https://github.com/sharpdx/SharpDX") }),
                ("CPUID", new[] { ("CPU-Z", false, "https://www.cpuid.com/softwares/cpu-z.html") }),
                ("鲁大师", new[] { ("鲁大师", false, "https://www.ludashi.com") }),
                ("Eassos", new[] { ("DiskGenius", false, "https://www.diskgenius.cn") }),
                ("zbezj", new[] { ("HEU KMS Activator", true, "https://github.com/zbezj/HEU_KMS_Activator") }),
                ("FinalWire", new[] { ("AIDA64", false, "https://www.aida64.com") }),
                ("ALCPU", new[] { ("Core Temp", false, "https://www.alcpu.com/coretemp") }),
                ("HWiNFO", new[] { ("HWiNFO", false, "https://www.hwinfo.com") }),
                ("TechPowerUp", new[] { ("GPU-Z", false, "https://www.techpowerup.com/gpuz") }),
                ("Geeks3D", new[] { ("FurMark", false, "https://www.geeks3d.com/20240109/furmark-gpu-benchmark/") }),
                ("hiyohiyo", new[]
                {
                    ("CrystalDiskMark", true, "https://github.com/hiyohiyo/CrystalDiskMark"),
                    ("CrystalDiskInfo", true, "https://github.com/hiyohiyo/CrystalDiskInfo"),
                }),
                ("Umberto", new[] { ("SpaceSniffer", false, "http://www.uderzo.it/space_sniffer") }),
                ("Acute Systems", new[] { ("TransMac", false, "https://www.acutesystems.com/dl_tmac.htm") }),
                ("OCLP-MOD", new[] { ("OpenCore Legacy Patcher MOD", true, "https://github.com/laobamac/OCLP-Mod") }),
                ("Chuyu Team", new[] { ("Dism++", true, "https://github.com/Chuyu-Team/Dism-Multi-Language") }),
                ("Thomas Koen", new[] { ("Geek Uninstaller", false, "https://geekuninstaller.com") }),
            };

            foreach (var entry in contributions)
            {
                if (entry.Projects.Length == 1)
                {
                    var p = entry.Projects[0];
                    ContributionsStack.Children.Add(CreateContribItem(entry.Developer, p.Name, p.IsOpenSource, p.Url, false));
                }
                else
                {
                    ContributionsStack.Children.Add(CreateDeveloperHeader(entry.Developer, entry.Projects.Length));
                    foreach (var proj in entry.Projects)
                    {
                        ContributionsStack.Children.Add(CreateContribItem(entry.Developer, proj.Name, proj.IsOpenSource, proj.Url, true));
                    }
                }
            }
        }

        private Border CreateDeveloperHeader(string developer, int count)
        {
            return new Border
            {
                Padding = new Thickness(4, 8, 4, 4),
                Child = new TextBlock
                {
                    Text = LanguageHelper.GetString("DeveloperProjectCount", developer, count),
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = ThemeHelper.GetBrush("DimTextBrush"),
                },
            };
        }

        private Border CreateContribItem(string developer, string project, bool isOpenSource, string url, bool indented)
        {
            string licenseTag = isOpenSource ? LanguageHelper.GetString("LicenseOpenSource") : LanguageHelper.GetString("LicenseClosedSource");
            var tagColor = isOpenSource ? Windows.UI.Color.FromArgb(255, 0x4C, 0xAF, 0x50) : Windows.UI.Color.FromArgb(255, 0xFF, 0x98, 0x00);
            double leftPad = indented ? 20 : 4;

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = project,
                                FontSize = 13,
                                Foreground = ThemeHelper.GetBrush("TertiaryTextBrush"),
                                VerticalAlignment = VerticalAlignment.Center,
                            },
                            new TextBlock
                            {
                                Text = indented ? "" : $"— {developer}",
                                FontSize = 12,
                                Foreground = ThemeHelper.GetBrush("DimTextBrush"),
                                VerticalAlignment = VerticalAlignment.Center,
                            },
                        },
                    },
                    new Border
                    {
                        Background = new SolidColorBrush(tagColor) { Opacity = 0.15 },
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(6, 1, 6, 1),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 8, 0),
                        Child = new TextBlock
                        {
                            Text = licenseTag,
                            FontSize = 10,
                            Foreground = new SolidColorBrush(tagColor),
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        },
                    },
                    new Button
                    {
                        Content = "\u2197",
                        FontSize = 12,
                        Padding = new Thickness(6, 2, 6, 2),
                        MinWidth = 28,
                        Tag = url,
                    },
                },
            };
            Grid.SetColumn(grid.Children[1] as FrameworkElement, 1);
            Grid.SetColumn(grid.Children[2] as FrameworkElement, 2);

            var btn = grid.Children[2] as Button;
            btn.Click += async (s, e) => await OpenUrlAsync(url);

            return new Border
            {
                Padding = new Thickness(leftPad, 6, 4, 6),
                Child = grid,
            };
        }

        // ────────────────────── URL Helpers ──────────────────────

        private async Task OpenUrlAsync(string url, string? errorMessage = null)
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                Logger.Warn("Settings", $"Failed to open URL: {ex.Message}");
                var dialog = new ContentDialog
                {
                    Title = LanguageHelper.GetString("DialogTitle_Failure"),
                    Content = errorMessage ?? LanguageHelper.GetString("ErrorCannotOpenLink"),
                    CloseButtonText = LanguageHelper.GetString("ButtonOK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private async void WebsiteButton_Click(object sender, RoutedEventArgs e)
            => await OpenUrlAsync(Cfg.Website, LanguageHelper.GetString("ErrorCannotOpenWebsite"));

        private async void GitHubButton_Click(object sender, RoutedEventArgs e)
            => await OpenUrlAsync(Cfg.GithubRepo, LanguageHelper.GetString("ErrorCannotOpenGitHub"));

        private async void GPLV3Button_Click(object sender, RoutedEventArgs e)
            => await OpenUrlAsync(Cfg.GPLV3, LanguageHelper.GetString("ErrorCannotOpenGPL"));

        private void RestartWizardButton_Click(object sender, RoutedEventArgs e)
        {
            string setupPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VTStudioToolBox", "setup_done.txt");
            try
            {
                if (System.IO.File.Exists(setupPath))
                    System.IO.File.Delete(setupPath);
                Logger.Info("Settings", "Wizard completion reset, restarting app");
            }
            catch (Exception ex)
            {
                Logger.Error("Settings", "Failed to reset wizard", ex);
            }

            Application.Current.Exit();
        }

        private async void RevokeEulaButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (EulaHelper.HasUserAgreed())
                {
                    Logger.Info("Settings", "User revoked EULA agreement");
                    await EulaHelper.RevokeAsync();

                    var dialog = new ContentDialog
                    {
                        Title = LanguageHelper.GetString("EulaRevokedTitle"),
                        Content = LanguageHelper.GetString("EulaRevokedContent"),
                        CloseButtonText = LanguageHelper.GetString("ButtonOK"),
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = LanguageHelper.GetString("DialogTitle_Prompt"),
                        Content = LanguageHelper.GetString("EulaNotFound"),
                        CloseButtonText = LanguageHelper.GetString("ButtonOK"),
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return;
                }

                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                Logger.Warn("Settings", $"Revoke EULA failed: {ex.Message}");
                var dialog = new ContentDialog
                {
                    Title = LanguageHelper.GetString("DialogTitle_Failure"),
                    Content = LanguageHelper.GetString("EulaRevokeError"),
                    CloseButtonText = LanguageHelper.GetString("ButtonOK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
    }
}
