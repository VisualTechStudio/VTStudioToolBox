using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using VTStudioToolBox.Helpers;
using VTStudioToolBox.Models;
using WinUIImage = Microsoft.UI.Xaml.Controls.Image;
using WinUIBrush = Microsoft.UI.Xaml.Media.Brush;

namespace VTStudioToolBox.Views
{
    public sealed partial class UtilitiesPage : Page
    {
        private static readonly List<ToolInfo> HardwareToolList = new()
        {
            new() { Name = "CPU-Z", Description = "ToolCPUDesc", ToolRelativePath = "cpuz_x64.exe", IconRelativePath = "cpuz_x64.exe" },
            new() { Name = "Core Temp", Description = "ToolCoreTempDesc", ToolRelativePath = "CoreTemp\\Core Temp x64.exe", IconRelativePath = "CoreTemp\\Core Temp x64.exe" },
            new() { Name = "AIDA64", Description = "ToolAIDA64Desc", ToolRelativePath = "AIDA64\\aida64.exe", IconRelativePath = "AIDA64\\aida64.exe" },
            new() { Name = "HWiNFO", Description = "ToolHWINFODesc", ToolRelativePath = "hwinfo\\HWiNFO64.exe", IconRelativePath = "hwinfo\\HWiNFO64.exe" },
            new() { Name = "GPU-Z", Description = "ToolGPUZDesc", ToolRelativePath = "GPUZ\\GPU-Z.exe", IconRelativePath = "GPUZ\\GPU-Z.exe" },
            new() { Name = "FurMark", Description = "ToolFurMarkDesc", ToolRelativePath = "FurMark\\FurMark.exe", IconRelativePath = "FurMark\\FurMark.exe" },
            new() { Name = "鲁大师", Description = "ToolLudashiDesc", ToolRelativePath = "ludashi.exe", IconRelativePath = "ludashi.exe" },
            new() { Name = "MonitorInfo", Description = "ToolMonitorInfoDesc", ToolRelativePath = "color\\monitorinfo.exe", IconRelativePath = "color\\monitorinfo.exe" },
        };

        private static readonly List<ToolInfo> DiskToolList = new()
        {
            new() { Name = "CrystalDiskMark", Description = "ToolCDMDesc", ToolRelativePath = "CrystalDiskMark\\DiskMark64S.exe", IconRelativePath = "CrystalDiskMark\\DiskMark64S.exe" },
            new() { Name = "CrystalDiskInfo", Description = "ToolCDIDesc", ToolRelativePath = "CrystalDiskInfo\\DiskInfo64S.exe", IconRelativePath = "CrystalDiskInfo\\DiskInfo64S.exe" },
            new() { Name = "DiskGenius", Description = "ToolDiskGeniusDesc", ToolRelativePath = "DiskGenius.exe", IconRelativePath = "DiskGenius.exe" },
            new() { Name = "SpaceSniffer", Description = "ToolSpaceSnifferDesc", ToolRelativePath = "SpaceSniffer\\SpaceSniffer.exe", IconRelativePath = "SpaceSniffer\\SpaceSniffer.exe" },
        };

        private static readonly List<ToolInfo> SystemToolList = new()
        {
            new() { Name = "Dism++", Description = "ToolDismppDesc", ToolRelativePath = "Dism++\\Dism++x64.exe", IconRelativePath = "Dism++\\Dism++x64.exe" },
            new() { Name = "Geek Uninstaller", Description = "ToolGeekDesc", ToolRelativePath = "Geek Uninstaller\\Geek Uninstaller.exe", IconRelativePath = "Geek Uninstaller\\Geek Uninstaller.exe" },
            new() { Name = "HEU KMS Activator", Description = "ToolHEUDesc", ToolRelativePath = "HEU_KMS_Activator_v63.2.0.exe", IconRelativePath = "HEU_KMS_Activator_v63.2.0.exe" },
        };

        public UtilitiesPage()
        {
            this.InitializeComponent();
            UpdateLanguage();
            this.Loaded += UtilitiesPage_Loaded;
            ThemeHelper.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            BuildToolList(HardwareTools, HardwareToolList);
            BuildToolList(DiskTools, DiskToolList);
            BuildToolList(SystemTools, SystemToolList);
        }

        private void UpdateLanguage()
        {
            PageTitle.Text = LanguageHelper.GetString("UtilitiesTitle");
            PageSubtitle.Text = LanguageHelper.GetString("UtilitiesSubtitle");
            HardwareHeader.Text = LanguageHelper.GetString("SectionHardware");
            DiskHeader.Text = LanguageHelper.GetString("SectionDisk");
            SystemHeader.Text = LanguageHelper.GetString("SectionSystem");
        }

        private void UtilitiesPage_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Dev("Utilities", $"Building tool lists: {HardwareToolList.Count} hardware, {DiskToolList.Count} disk, {SystemToolList.Count} system");
            BuildToolList(HardwareTools, HardwareToolList);
            BuildToolList(DiskTools, DiskToolList);
            BuildToolList(SystemTools, SystemToolList);
        }

        private void BuildToolList(Grid container, List<ToolInfo> tools)
        {
            string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            container.Children.Clear();
            container.RowDefinitions.Clear();
            container.ColumnDefinitions.Clear();

            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12, GridUnitType.Pixel) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12, GridUnitType.Pixel) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int cols = 3;
            int rows = (tools.Count + cols - 1) / cols;
            for (int i = 0; i < rows; i++)
                container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < tools.Count; i++)
            {
                int row = i / cols;
                int col = (i % cols) * 2;

                string toolPath = Path.Combine(appDirectory, "Tools", tools[i].ToolRelativePath);
                string toolName = tools[i].Name;

                var cardBg = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    ThemeHelper.CurrentTheme == Microsoft.UI.Xaml.ElementTheme.Light
                        ? Windows.UI.Color.FromArgb(255, 245, 245, 245)
                        : Windows.UI.Color.FromArgb(255, 45, 45, 45));
                var card = new Border
                {
                    Background = cardBg,
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 0, 12),
                    IsTapEnabled = true
                };
                card.Tapped += (s, e) => LaunchTool(toolPath, toolName);

                var stack = new StackPanel { Spacing = 8 };

                var icon = new WinUIImage
                {
                    Width = 28,
                    Height = 28,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                LoadIconFromPath(icon, Path.Combine(appDirectory, "Tools", tools[i].IconRelativePath));
                stack.Children.Add(icon);

                var isLight = ThemeHelper.CurrentTheme == Microsoft.UI.Xaml.ElementTheme.Light;
                var primaryBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    isLight ? Windows.UI.Color.FromArgb(255, 26, 26, 26) : Windows.UI.Color.FromArgb(255, 255, 255, 255));
                var secondaryBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    isLight ? Windows.UI.Color.FromArgb(255, 68, 68, 68) : Windows.UI.Color.FromArgb(255, 204, 204, 204));

                stack.Children.Add(new TextBlock
                {
                    Text = tools[i].Name,
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = primaryBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                stack.Children.Add(new TextBlock
                {
                    Text = LanguageHelper.GetString(tools[i].Description),
                    FontSize = 12,
                    Foreground = secondaryBrush,
                    TextWrapping = TextWrapping.Wrap,
                    MaxLines = 2
                });

                card.Child = stack;
                Grid.SetRow(card, row);
                Grid.SetColumn(card, col);
                container.Children.Add(card);
            }
        }

        private void LoadIconFromPath(WinUIImage imageControl, string toolPath)
        {
            try
            {
                if (File.Exists(toolPath))
                {
                    using var icon = Icon.ExtractAssociatedIcon(toolPath);
                    if (icon != null)
                    {
                        using var bitmap = icon.ToBitmap();
                        using var stream = new MemoryStream();
                        bitmap.Save(stream, ImageFormat.Png);
                        stream.Position = 0;
                        var bitmapImage = new BitmapImage();
                        bitmapImage.SetSource(stream.AsRandomAccessStream());
                        imageControl.Source = bitmapImage;
                    }
                }
            }
            catch { }
        }

        private async void LaunchTool(string toolPath, string toolName)
        {
            try
            {
                if (File.Exists(toolPath))
                {
                    Logger.Info("Utilities", $"Launching tool: {toolName} ({toolPath})");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = toolPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Logger.Warn("Utilities", $"Tool not found: {toolName} ({toolPath})");
                    var dialog = new ContentDialog
                    {
                        Title = LanguageHelper.GetString("DialogTitle_Error"),
                        Content = LanguageHelper.GetString("ErrorToolNotFound", toolName),
                        CloseButtonText = LanguageHelper.GetString("ButtonOK"),
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Utilities", $"Failed to launch {toolName}", ex);
                var dialog = new ContentDialog
                {
                    Title = LanguageHelper.GetString("DialogTitle_LaunchFailed"),
                    Content = LanguageHelper.GetString("ErrorToolLaunchFailed", toolName, ex.Message),
                    CloseButtonText = LanguageHelper.GetString("ButtonOK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
    }
}
