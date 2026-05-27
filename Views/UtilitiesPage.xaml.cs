using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;

namespace VTStudioToolBox.Views
{
    public sealed partial class UtilitiesPage : Page
    {
        public UtilitiesPage()
        {
            this.InitializeComponent();
            this.Loaded += UtilitiesPage_Loaded;
        }

        private void UtilitiesPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAllIcons();
        }

        private void LoadAllIcons()
        {
            LoadIcon(CpuZIcon, "cpuz_x64.exe");
            LoadIcon(CoreTempIcon, "CoreTemp", "Core Temp x64.exe");
            LoadIcon(AIDA64Icon, "AIDA64", "aida64.exe");
            LoadIcon(HWiNFOIcon, "hwinfo", "HWiNFO64.exe");
            LoadIcon(GPUZIcon, "GPUZ", "GPU-Z.exe");
            LoadIcon(FurMarkIcon, "FurMark", "FurMark.exe");
            LoadIcon(LuDaShiIcon, "ludashi.exe");
            LoadIcon(MonitorInfoIcon, "color", "monitorinfo.exe");

            LoadIcon(DiskMarkIcon, "CrystalDiskMark", "DiskMark64S.exe");
            LoadIcon(DiskInfoIcon, "CrystalDiskInfo", "DiskInfo64S.exe");
            LoadIcon(DiskGeniusIcon, "DiskGenius.exe");
            LoadIcon(SpaceSnifferIcon, "SpaceSniffer", "SpaceSniffer.exe");

            LoadIcon(DismIcon, "Dism++", "Dism++x64.exe");
            LoadIcon(GeekIcon, "Geek Uninstaller", "Geek Uninstaller.exe");
            LoadIcon(KMSIcon, "HEU_KMS_Activator_v63.2.0.exe");
        }

        private void LoadIcon(Microsoft.UI.Xaml.Controls.Image imageControl, string exeName)
        {
            string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            string toolPath = Path.Combine(appDirectory, "Tools", exeName);
            LoadIconFromPath(imageControl, toolPath);
        }

        private void LoadIcon(Microsoft.UI.Xaml.Controls.Image imageControl, string folder, string exeName)
        {
            string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            string toolPath = Path.Combine(appDirectory, "Tools", folder, exeName);
            LoadIconFromPath(imageControl, toolPath);
        }

        private void LoadIconFromPath(Microsoft.UI.Xaml.Controls.Image imageControl, string toolPath)
        {
            try
            {
                if (File.Exists(toolPath))
                {
                    var icon = Icon.ExtractAssociatedIcon(toolPath);
                    if (icon != null)
                    {
                        var bitmap = icon.ToBitmap();
                        using (var stream = new MemoryStream())
                        {
                            bitmap.Save(stream, ImageFormat.Png);
                            stream.Position = 0;
                            var bitmapImage = new BitmapImage();
                            bitmapImage.SetSource(stream.AsRandomAccessStream());
                            imageControl.Source = bitmapImage;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private string GetToolPath(string relativePath)
        {
            string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            return Path.Combine(appDirectory, "Tools", relativePath);
        }

        private async void LaunchTool(string toolPath, string toolName)
        {
            try
            {
                if (File.Exists(toolPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = toolPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = $"找不到 {toolName}，请确保工具文件存在。",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "启动失败",
                    Content = $"启动 {toolName} 时出错：{ex.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private void BtnLaunchCpuZ_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("cpuz_x64.exe"), "CPU-Z");
        }

        private void BtnLaunchCoreTemp_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("CoreTemp\\Core Temp x64.exe"), "Core Temp");
        }

        private void BtnLaunchAIDA64_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("AIDA64\\aida64.exe"), "AIDA64");
        }

        private void BtnLaunchHWiNFO_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("hwinfo\\HWiNFO64.exe"), "HWiNFO");
        }

        private void BtnLaunchGPUZ_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("GPUZ\\GPU-Z.exe"), "GPU-Z");
        }

        private void BtnLaunchFurMark_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("FurMark\\FurMark.exe"), "FurMark");
        }

        private void BtnLaunchLuDaShi_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("ludashi.exe"), "鲁大师");
        }

        private void BtnLaunchMonitorInfo_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("color\\monitorinfo.exe"), "MonitorInfo");
        }

        private void BtnLaunchDiskMark_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("CrystalDiskMark\\DiskMark64S.exe"), "CrystalDiskMark");
        }

        private void BtnLaunchDiskInfo_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("CrystalDiskInfo\\DiskInfo64S.exe"), "CrystalDiskInfo");
        }

        private void BtnLaunchDiskGenius_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("DiskGenius.exe"), "DiskGenius");
        }

        private void BtnLaunchSpaceSniffer_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("SpaceSniffer\\SpaceSniffer.exe"), "SpaceSniffer");
        }

        private void BtnLaunchDism_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("Dism++\\Dism++x64.exe"), "Dism++");
        }

        private void BtnLaunchGeek_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("Geek Uninstaller\\Geek Uninstaller.exe"), "Geek Uninstaller");
        }

        private void BtnLaunchKMS_Click(object sender, RoutedEventArgs e)
        {
            LaunchTool(GetToolPath("HEU_KMS_Activator_v63.2.0.exe"), "HEU KMS Activator");
        }
    }
}
