using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using VTStudioToolBox.Helpers;

namespace VTStudioToolBox.Views
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isInitializing = true;

        public SettingsPage()
        {
            this.InitializeComponent();
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

            ThemeHelper.ThemeChanged += OnThemeChanged;
            _isInitializing = false;
        }

        private void OnThemeChanged()
        {
            // Rebuild contributions section with new theme brushes
            ContributionsStack.Children.Clear();
            BuildContributionsSection();
        }

        private void UpdateLanguage()
        {
            PageTitle.Text = LanguageHelper.GetString("SettingsTitle");
            PageSubtitle.Text = LanguageHelper.GetString("SettingsSubtitle");
            LanguageHeader.Text = LanguageHelper.GetString("LabelLanguage");
            ThemeHeader.Text = LanguageHelper.GetString("LabelTheme");
            ThemeDarkOption.Content = LanguageHelper.GetString("ThemeDark");
            ThemeLightOption.Content = LanguageHelper.GetString("ThemeLight");
            LogHeader.Text = LanguageHelper.GetString("SectionLog");
            LogLevelLabel.Text = LanguageHelper.GetString("LabelLogLevel");
            FeedbackButtonText.Text = LanguageHelper.GetString("ButtonFeedback");
            AboutHeader.Text = LanguageHelper.GetString("SectionAbout");
            WebsiteButtonText.Text = LanguageHelper.GetString("ButtonWebsite");
            GitHubButtonText.Text = LanguageHelper.GetString("ButtonGitHub");
            GPLButtonText.Text = LanguageHelper.GetString("ButtonGPL");
            RevokeEulaButton.Content = LanguageHelper.GetString("ButtonRevokeEULA");
            ContributorsHeader.Text = LanguageHelper.GetString("Contributors");
            ContributionsHeader.Text = LanguageHelper.GetString("SectionContributions");
            ContributionsIntro.Text = LanguageHelper.GetString("ContributionsIntro");
            ChangelogHeader.Text = LanguageHelper.GetString("SectionChangelog");

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
                catch { }
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
            if (ThemeHelper.CurrentTheme == Microsoft.UI.Xaml.ElementTheme.Dark)
            {
                ThemeComboBox.SelectedItem = ThemeDarkOption;
            }
            else
            {
                ThemeComboBox.SelectedItem = ThemeLightOption;
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (ThemeComboBox == null || ThemeComboBox.SelectedItem == null) return;

            if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (Enum.TryParse<Microsoft.UI.Xaml.ElementTheme>(tag, out var theme))
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
                    LanguageHelper.ApplyLanguage(tag);
                    LanguageHelper.RestartApp();
                }
            }
        }

        private async void FeedbackButton_Click(object sender, RoutedEventArgs e)
            => await OpenUrlAsync($"{Cfg.GithubRepo}/issues", LanguageHelper.GetString("ErrorCannotOpenFeedback"));

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

        private async Task OpenUrlAsync(string url, string? errorMessage = null)
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开链接失败: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"撤销 EULA 失败: {ex.Message}");
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
