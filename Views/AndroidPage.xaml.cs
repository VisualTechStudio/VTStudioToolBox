using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VTStudioToolBox.Helpers;

namespace VTStudioToolBox.Views
{
    public sealed partial class AndroidPage : Page
    {
        private string _selectedSerial = "";
        private string _lastSelectedState = "";
        private readonly DispatcherTimer _uiTimer;
        private bool _isRecording = false;

        public AndroidPage()
        {
            this.InitializeComponent();
            UpdateLanguage();

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _uiTimer.Tick += (s, e) => ApplyCache();

            this.Loaded += AndroidPage_Loaded;
            this.Unloaded += AndroidPage_Unloaded;
            ThemeHelper.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            // Force rebuild device list with new theme brushes
            _lastAppliedHash = "";
        }

        private void UpdateLanguage()
        {
            PageTitle.Text = LanguageHelper.GetString("AndroidTitle");
            PageSubtitle.Text = LanguageHelper.GetString("AndroidSubtitle");
            AdbStatusHeader.Text = LanguageHelper.GetString("AndroidAdbStatus");
            DevicesHeader.Text = LanguageHelper.GetString("AndroidDevices");
            NoDevicesText.Text = LanguageHelper.GetString("AndroidNoDevices");
            DeviceInfoHeader.Text = LanguageHelper.GetString("AndroidDeviceInfo");
            LabelDevice.Text = LanguageHelper.GetString("LabelDevice");
            LabelKernel.Text = LanguageHelper.GetString("LabelKernel");
            LabelHardware.Text = LanguageHelper.GetString("LabelHardware");
            LabelOS.Text = LanguageHelper.GetString("LabelOS");
            LabelBattery.Text = LanguageHelper.GetString("LabelBattery");
            LabelResolution.Text = LanguageHelper.GetString("AndroidResolution");
            ActionsHeader.Text = LanguageHelper.GetString("AndroidQuickActions");
            RebootSectionHeader.Text = LanguageHelper.GetString("AndroidRebootSection");
            ScreenSectionHeader.Text = LanguageHelper.GetString("AndroidScreenSection");
            BtnRebootText.Text = LanguageHelper.GetString("AndroidRebootSystem");
            BtnRebootBootloaderText.Text = LanguageHelper.GetString("AndroidRebootBootloader");
            BtnRebootFastbootdText.Text = LanguageHelper.GetString("AndroidRebootFastbootd");
            BtnRebootRecoveryText.Text = LanguageHelper.GetString("AndroidRebootRecovery");
            BtnScreenshotText.Text = LanguageHelper.GetString("AndroidScreenshot");
            BtnScreenRecordText.Text = LanguageHelper.GetString("AndroidScreenRecord");
        }

        private void AndroidPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyCache();
            _uiTimer.Start();
        }

        private void AndroidPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _uiTimer.Stop();
        }

        private string _lastAppliedHash = "";

        private void ApplyCache()
        {
            string hash = AdbCache.Status + "|" + string.Join("|", AdbCache.Devices.Select(d => d.Serial + ":" + d.State));
            if (hash == _lastAppliedHash) return;
            _lastAppliedHash = hash;

            // ADB status
            if (AdbCache.Status == "Ready")
            {
                AdbStatusText.Text = LanguageHelper.GetString("AndroidAdbReady");
                AdbPidText.Text = AdbCache.PidText;
                AdbVersionText.Text = AdbCache.Version;
            }
            else
            {
                AdbStatusText.Text = AdbCache.Status == "ADB not found"
                    ? LanguageHelper.GetString("AndroidAdbNotFound")
                    : LanguageHelper.GetString("AndroidAdbError");
                AdbPidText.Text = "";
                AdbVersionText.Text = "";
            }

            // Device list
            var devices = AdbCache.Devices;
            NoDevicesText.Visibility = devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            bool selectedStillConnected = false;
            string selectedState = "";
            foreach (var d in devices)
            {
                if (d.Serial == _selectedSerial) { selectedStillConnected = true; selectedState = d.State; break; }
            }

            var panel = new StackPanel { Spacing = 0 };
            foreach (var info in devices)
                panel.Children.Add(BuildDeviceRow(info));
            DeviceListHost.Content = panel;

            // Auto-select first device or refresh on state change
            if (devices.Count > 0 && (!selectedStillConnected || string.IsNullOrEmpty(_selectedSerial)))
            {
                _selectedSerial = devices[0].Serial;
                _lastSelectedState = devices[0].State;
                _ = ShowDeviceInfo(_selectedSerial);
            }
            else if (selectedStillConnected && _lastSelectedState != selectedState)
            {
                _lastSelectedState = selectedState;
                _ = ShowDeviceInfo(_selectedSerial);
            }
            else if (devices.Count == 0)
            {
                _selectedSerial = "";
                _lastSelectedState = "";
                ClearDeviceInfo();
            }
        }

        private UIElement BuildDeviceRow(DeviceInfo info)
        {
            var (manufacturer, model, conn) = AdbCache.DeviceDetails.TryGetValue(info.Serial, out var cached)
                ? cached
                : ("", info.Product ?? info.Device ?? info.Serial, "USB");

            string mainLabel = manufacturer + " " + model;
            string subLabel = $"{conn}  ·  {info.Serial}";

            var grid = new Grid
            {
                Padding = new Thickness(0, 4, 0, 4),
                Margin = new Thickness(0, 0, 0, 1)
            };

            var infoPanel = new StackPanel();
            infoPanel.Children.Add(new TextBlock
            {
                Text = mainLabel,
                FontSize = 14,
                Foreground = ThemeHelper.GetBrush("PrimaryTextBrush")
            });
            infoPanel.Children.Add(new TextBlock
            {
                Text = subLabel,
                FontSize = 12,
                Foreground = ThemeHelper.GetBrush("SecondaryTextBrush"),
                Margin = new Thickness(0, 2, 0, 0)
            });
            grid.Children.Add(infoPanel);

            string serial = info.Serial;
            var tapArea = new Grid
            {
                Background = new SolidColorBrush(Colors.Transparent),
                CornerRadius = new CornerRadius(4)
            };
            tapArea.Children.Add(grid);
            tapArea.Tapped += async (s, e) =>
            {
                _selectedSerial = serial;
                _lastSelectedState = info.State;
                await ShowDeviceInfo(serial);
            };

            return tapArea;
        }

        private void SetOverlays(bool visible)
        {
            var vis = visible ? Visibility.Visible : Visibility.Collapsed;
            DeviceOverlay.Visibility = vis;
            ActionsOverlay.Visibility = vis;
        }

        private void ClearDeviceInfo()
        {
            SetOverlays(true);
            ValueDevice.Text = "--";
            ValueKernel.Text = "--";
            ValueHardware.Text = "--";
            ValueOS.Text = "--";
            ValueBattery.Text = "--";
            ValueResolution.Text = "--";
        }

        private async Task ShowDeviceInfo(string serial)
        {
            SetOverlays(false);

            var adbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
            var tasks = new List<Task<(int exitCode, string output)>>();
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell getprop ro.product.manufacturer"));
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell getprop ro.product.model"));
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell getprop ro.build.version.release"));
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell getprop ro.build.version.sdk"));
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell getprop ro.build.display.id"));
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell getprop ro.product.platform"));
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell getprop ro.product.board"));
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell getprop ro.product.cpu.abi"));
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell cat /proc/version"));
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell dumpsys battery"));
            tasks.Add(RunAdb(adbPath, $"-s {serial} shell wm size"));
            await Task.WhenAll(tasks);

            string mfr = tasks[0].Result.exitCode == 0 ? tasks[0].Result.output.Trim() : "-";
            string model = tasks[1].Result.exitCode == 0 ? tasks[1].Result.output.Trim() : "-";
            string ver = tasks[2].Result.exitCode == 0 ? tasks[2].Result.output.Trim() : "-";
            string sdk = tasks[3].Result.exitCode == 0 ? tasks[3].Result.output.Trim() : "-";
            string build = tasks[4].Result.exitCode == 0 ? tasks[4].Result.output.Trim() : "-";
            string platform = tasks[5].Result.exitCode == 0 ? tasks[5].Result.output.Trim() : "-";
            string board = tasks[6].Result.exitCode == 0 ? tasks[6].Result.output.Trim() : "-";
            string abi = tasks[7].Result.exitCode == 0 ? tasks[7].Result.output.Trim() : "-";
            string kernel = tasks[8].Result.exitCode == 0 ? tasks[8].Result.output.Trim() : "-";
            string battery = tasks[9].Result.exitCode == 0 ? ParseBattery(tasks[9].Result.output) : "-";
            string resolution = tasks[10].Result.exitCode == 0 ? tasks[10].Result.output.Trim().Replace("Physical size: ", "") : "-";

            // 设备:*制造商 *型号 (*序列号)
            ValueDevice.Text = $"{mfr} {model} ({serial})";
            // 内核:*内核版本 (only version)
            string kernelVer = kernel.Contains("Linux version") ? kernel.Split(' ')[2] : kernel;
            ValueKernel.Text = kernelVer;
            // 硬件:*主板 (*ABI)
            ValueHardware.Text = $"{board} ({abi})";
            // OS:Android *版本号 *构建号 (API *API)
            ValueOS.Text = $"Android {ver} {build} (API {sdk})";
            // 电量:*电量
            ValueBattery.Text = battery;
            // 分辨率:*分辨率
            ValueResolution.Text = resolution;
        }

        private async void BtnReboot_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedSerial)) return;
            await RunAdbCommandForDevice("reboot");
        }

        private async void BtnRebootRecovery_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedSerial)) return;
            await RunAdbCommandForDevice("reboot recovery");
        }

        private async void BtnRebootBootloader_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedSerial)) return;
            await RunAdbCommandForDevice("reboot bootloader");
        }

        private async void BtnRebootFastbootd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedSerial)) return;
            await RunAdbCommandForDevice("reboot fastboot");
        }

        private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedSerial)) return;
            await RunAdbCommandForDevice("shell screencap -p /sdcard/screenshot.png");
            await RunAdbCommandForDevice($"pull /sdcard/screenshot.png");
            await RunAdbCommandForDevice("shell rm /sdcard/screenshot.png");
        }

        private async void BtnScreenRecord_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedSerial)) return;
            if (_isRecording)
            {
                await RunAdbCommandForDevice("shell pkill -INT screenrecord");
                _isRecording = false;
                BtnScreenRecordText.Text = LanguageHelper.GetString("AndroidScreenRecord");
                ScreenRecordIcon.Glyph = "\uE714";
            }
            else
            {
                _ = RunAdbCommandForDevice("shell screenrecord /sdcard/recording.mp4");
                _isRecording = true;
                BtnScreenRecordText.Text = LanguageHelper.GetString("AndroidStopRecord");
                ScreenRecordIcon.Glyph = "\uE71A";
            }
        }

        private async Task RunAdbCommandForDevice(string command)
        {
            var adbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
            await RunAdb(adbPath, $"-s {_selectedSerial} {command}");
        }

        private string ParseBattery(string output)
        {
            foreach (var line in output.Split('\n'))
                if (line.Contains("level:"))
                { var p = line.Split(':'); if (p.Length >= 2) return p[1].Trim() + "%"; }
            return "-";
        }

        private async Task<(int exitCode, string output)> RunAdb(string path, string args)
        {
            try
            {
                using var p = new System.Diagnostics.Process();
                p.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path, Arguments = args,
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                p.Start();
                var stdout = await p.StandardOutput.ReadToEndAsync();
                var stderr = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();
                return (p.ExitCode, string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}");
            }
            catch (Exception ex)
            {
                Logger.Error("Android", $"ADB command failed: {args}", ex);
                return (-1, ex.Message);
            }
        }
    }
}
