using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        private bool _isJailbreaking = false;
        private bool _rootAchieved = false;
        private FrameworkElement[] _allViews = null!;

        // Kernel_version directory scan results: deviceName -> [kernelVersions]
        private readonly Dictionary<string, List<string>> _jbDeviceKernels = new();
        private string? _selectedSoPath;

        public AndroidPage()
        {
            this.InitializeComponent();
            _allViews = new FrameworkElement[] { RootView, JailbreakView, QuickActionsView };
            UpdateLanguage();
            ScanKernelVersions();

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _uiTimer.Tick += (s, e) => ApplyCache();

            this.Loaded += AndroidPage_Loaded;
            this.Unloaded += AndroidPage_Unloaded;
            ThemeHelper.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            _lastAppliedHash = "";
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

            var fadeIn = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, target);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

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
        private void OpenJailbreak(object sender, RoutedEventArgs e) => ShowView(JailbreakView);
        private void OpenQuickActions(object sender, RoutedEventArgs e) => ShowView(QuickActionsView);

        // ────────────────────── Language ──────────────────────

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

            // 首级入口
            JailbreakEntryTitle.Text = LanguageHelper.GetString("AndroidJailbreak");
            JailbreakEntryDesc.Text = LanguageHelper.GetString("AndroidJailbreakEntryDesc");
            QuickActionsEntryTitle.Text = LanguageHelper.GetString("AndroidQuickActions");
            QuickActionsEntryDesc.Text = LanguageHelper.GetString("AndroidQuickActionsEntryDesc");

            // 二级 - 越狱
            BackText.Text = LanguageHelper.GetString("ButtonBack");
            JailbreakPageTitle.Text = LanguageHelper.GetString("AndroidJailbreak");
            JailbreakSelectHeader.Text = LanguageHelper.GetString("AndroidJailbreakSelect");
            JailbreakDeviceLabel.Text = LanguageHelper.GetString("AndroidJailbreakDeviceLabel");
            JailbreakKernelLabel.Text = LanguageHelper.GetString("AndroidJailbreakKernelLabel");
            JailbreakHeader.Text = LanguageHelper.GetString("AndroidJailbreakExecute");
            BtnJailbreakText.Text = LanguageHelper.GetString("AndroidJailbreakRun");
            BtnKernelSUText.Text = LanguageHelper.GetString("AndroidKernelSULateLoad");

            // 二级 - 快捷操作
            QuickActionsPageTitle.Text = LanguageHelper.GetString("AndroidQuickActions");
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

        // ────────────────────── Kernel_version Scanner ──────────────────────

        private void ScanKernelVersions()
        {
            _jbDeviceKernels.Clear();
            string basePath = Path.Combine(AppContext.BaseDirectory, "Kernel_version");
            if (!Directory.Exists(basePath)) return;

            foreach (var deviceDir in Directory.GetDirectories(basePath))
            {
                string deviceName = Path.GetFileName(deviceDir);
                var kernels = new List<string>();
                foreach (var kernelDir in Directory.GetDirectories(deviceDir))
                {
                    string kernelName = Path.GetFileName(kernelDir);
                    if (File.Exists(Path.Combine(kernelDir, "preload.so")))
                        kernels.Add(kernelName);
                }
                if (kernels.Count > 0)
                {
                    kernels.Sort();
                    _jbDeviceKernels[deviceName] = kernels;
                }
            }

            // Populate device combo
            JailbreakDeviceCombo.Items.Clear();
            foreach (var device in _jbDeviceKernels.Keys.OrderBy(k => k))
            {
                JailbreakDeviceCombo.Items.Add(new ComboBoxItem { Content = device, Tag = device });
            }
            if (JailbreakDeviceCombo.Items.Count > 0)
                JailbreakDeviceCombo.SelectedIndex = 0;
        }

        private void JailbreakDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            JailbreakKernelCombo.Items.Clear();
            _selectedSoPath = null;

            if (JailbreakDeviceCombo.SelectedItem is ComboBoxItem item && item.Tag is string deviceName
                && _jbDeviceKernels.TryGetValue(deviceName, out var kernels))
            {
                foreach (var k in kernels)
                {
                    JailbreakKernelCombo.Items.Add(new ComboBoxItem { Content = k, Tag = k });
                }
                if (JailbreakKernelCombo.Items.Count > 0)
                    JailbreakKernelCombo.SelectedIndex = 0;
            }
            UpdateJailbreakButtonState();
        }

        private void JailbreakKernelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedSoPath = null;

            if (JailbreakDeviceCombo.SelectedItem is ComboBoxItem devItem && devItem.Tag is string deviceName
                && JailbreakKernelCombo.SelectedItem is ComboBoxItem kerItem && kerItem.Tag is string kernelName)
            {
                string candidate = Path.Combine(AppContext.BaseDirectory, "Kernel_version", deviceName, kernelName, "preload.so");
                if (File.Exists(candidate))
                    _selectedSoPath = candidate;
            }
            UpdateJailbreakButtonState();
        }

        private void UpdateJailbreakButtonState()
        {
            BtnJailbreak.IsEnabled = !_isJailbreaking
                && !string.IsNullOrEmpty(_selectedSerial)
                && _selectedSoPath != null;
        }

        // ────────────────────── Lifecycle ──────────────────────

        private void AndroidPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyCache();
            _uiTimer.Start();
        }

        private void AndroidPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _uiTimer.Stop();
            ThemeHelper.ThemeChanged -= OnThemeChanged;
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

            UpdateJailbreakButtonState();
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

            var adbPath = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
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
            string board = tasks[6].Result.exitCode == 0 ? tasks[6].Result.output.Trim() : "-";
            string abi = tasks[7].Result.exitCode == 0 ? tasks[7].Result.output.Trim() : "-";
            string kernel = tasks[8].Result.exitCode == 0 ? tasks[8].Result.output.Trim() : "-";
            string battery = tasks[9].Result.exitCode == 0 ? ParseBattery(tasks[9].Result.output) : "-";
            string resolution = tasks[10].Result.exitCode == 0 ? tasks[10].Result.output.Trim().Replace("Physical size: ", "") : "-";

            ValueDevice.Text = $"{mfr} {model} ({serial})";
            string kernelVer = kernel.Contains("Linux version") ? kernel.Split(' ')[2] : kernel;
            ValueKernel.Text = kernelVer;
            ValueHardware.Text = $"{board} ({abi})";
            ValueOS.Text = $"Android {ver} {build} (API {sdk})";
            ValueBattery.Text = battery;
            ValueResolution.Text = resolution;

            // Auto-select matching kernel in combo if available
            AutoSelectKernelCombo(kernelVer);
            UpdateJailbreakButtonState();
        }

        private void AutoSelectKernelCombo(string kernelVer)
        {
            if (string.IsNullOrEmpty(kernelVer) || kernelVer == "-") return;
            for (int i = 0; i < JailbreakKernelCombo.Items.Count; i++)
            {
                if (JailbreakKernelCombo.Items[i] is ComboBoxItem item && item.Tag is string tag && tag == kernelVer)
                {
                    JailbreakKernelCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        // ────────────────────── Vivo Jailbreak ──────────────────────

        private void AppendJailbreakLog(string msg)
        {
            JailbreakLog.Text += msg + "\n";
            JailbreakLogScroller.ChangeView(null, JailbreakLogScroller.ScrollableHeight, null);
        }

        private async void BtnJailbreak_Click(object sender, RoutedEventArgs e)
        {
            if (_isJailbreaking || string.IsNullOrEmpty(_selectedSerial) || _selectedSoPath == null) return;

            _isJailbreaking = true;
            _rootAchieved = false;
            _uiTimer.Stop();
            BtnJailbreak.IsEnabled = false;
            BtnKernelSU.IsEnabled = false;
            JailbreakLog.Text = "";

            var adbPath = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
            string serial = _selectedSerial;
            string soPath = _selectedSoPath;

            try
            {
                // Step 1: Push preload.so
                AppendJailbreakLog(LanguageHelper.GetString("AndroidJailbreakStepPush"));
                var (code, output) = await RunAdb(adbPath, $"-s {serial} push \"{soPath}\" /data/local/tmp/preload.so");
                if (code != 0) { AppendJailbreakLog("[!] " + LanguageHelper.GetString("AndroidJailbreakFailPush")); goto done; }

                // Step 2: SHA-256 verify
                AppendJailbreakLog(LanguageHelper.GetString("AndroidJailbreakStepSHA"));
                string localHash = ComputeSha256(soPath);
                (code, output) = await RunAdb(adbPath, $"-s {serial} shell sha256sum /data/local/tmp/preload.so");
                string remoteHash = output.Trim().Split(' ')[0];
                if (!string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase))
                {
                    AppendJailbreakLog("[!] SHA-256 mismatch");
                    AppendJailbreakLog($"    local:  {localHash}");
                    AppendJailbreakLog($"    remote: {remoteHash}");
                    goto done;
                }
                AppendJailbreakLog("[OK] SHA-256 matched");

                // Step 3: chmod
                AppendJailbreakLog(LanguageHelper.GetString("AndroidJailbreakStepChmod"));
                await RunAdb(adbPath, $"-s {serial} shell chmod 755 /data/local/tmp/preload.so");

                // Step 4: Reboot
                AppendJailbreakLog(LanguageHelper.GetString("AndroidJailbreakStepReboot"));
                await RunAdb(adbPath, $"-s {serial} reboot");

                // Step 5: Wait for device
                AppendJailbreakLog(LanguageHelper.GetString("AndroidJailbreakStepWait"));
                bool deviceBack = false;
                for (int i = 0; i < 60; i++)
                {
                    await Task.Delay(5000);
                    var (lsCode, lsOutput) = await RunAdb(adbPath, "devices");
                    if (lsOutput.Contains(serial) && lsOutput.Contains("device"))
                    {
                        deviceBack = true;
                        AppendJailbreakLog(LanguageHelper.GetString("AndroidJailbreakStepDeviceBack", i + 1));
                        break;
                    }
                }
                if (!deviceBack) { AppendJailbreakLog("[!] " + LanguageHelper.GetString("AndroidJailbreakFailTimeout")); goto done; }

                // Step 6: Run exploit (stream output line by line)
                AppendJailbreakLog(LanguageHelper.GetString("AndroidJailbreakStepExploit"));
                code = await RunAdbStreamed(adbPath, $"-s {serial} shell \"LD_PRELOAD=/data/local/tmp/preload.so /system/bin/id\"",
                    line => DispatcherQueue.TryEnqueue(() => AppendJailbreakLog(line)));

                // Step 7: Verify root
                AppendJailbreakLog(LanguageHelper.GetString("AndroidJailbreakStepVerify"));
                (code, output) = await RunAdb(adbPath, $"-s {serial} shell su -c id");
                if (output.Contains("uid=0(root)"))
                {
                    _rootAchieved = true;
                    AppendJailbreakLog("[OK] " + LanguageHelper.GetString("AndroidJailbreakSuccess"));
                    BtnKernelSU.IsEnabled = true;
                }
                else
                {
                    AppendJailbreakLog("[!] " + LanguageHelper.GetString("AndroidJailbreakFailRoot"));
                    AppendJailbreakLog(output.Trim());
                }
            }
            catch (Exception ex)
            {
                AppendJailbreakLog("[!] " + ex.Message);
                Logger.Error("Android", "Jailbreak failed", ex);
            }

        done:
            _isJailbreaking = false;
            _uiTimer.Start();
            UpdateJailbreakButtonState();
        }

        private async void BtnKernelSU_Click(object sender, RoutedEventArgs e)
        {
            if (!_rootAchieved || string.IsNullOrEmpty(_selectedSerial)) return;

            var adbPath = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
            string serial = _selectedSerial;

            AppendJailbreakLog(LanguageHelper.GetString("AndroidJailbreakStepKernelSU"));

            var (code, output) = await RunAdb(adbPath,
                $"-s {serial} shell \"su -c '$(find /data/app -name libksud.so | grep me.weishu.kernelsu | head -n 1) late-load --allow-shell --package-name me.weishu.kernelsu'\"");
            AppendJailbreakLog(output.Trim());

            if (output.Contains("late-load") || code == 0)
            {
                AppendJailbreakLog("[OK] " + LanguageHelper.GetString("AndroidJailbreakKernelSUDone"));
            }
            else
            {
                AppendJailbreakLog("[!] " + LanguageHelper.GetString("AndroidJailbreakKernelSUFail"));
            }
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        // ────────────────────── Quick Actions ──────────────────────

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
            await RunAdbCommandForDevice("pull /sdcard/screenshot.png");
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

        // ────────────────────── ADB Helpers ──────────────────────

        private async Task RunAdbCommandForDevice(string command)
        {
            var adbPath = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
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

        private async Task<int> RunAdbStreamed(string path, string args, Action<string> onLine)
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

                // Read stdout line by line
                while (!p.StandardOutput.EndOfStream)
                {
                    string? line = await p.StandardOutput.ReadLineAsync();
                    if (line != null) onLine(line);
                }

                // Drain stderr after stdout EOF
                string stderr = await p.StandardError.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(stderr)) onLine(stderr.Trim());

                await p.WaitForExitAsync();
                return p.ExitCode;
            }
            catch (Exception ex)
            {
                Logger.Error("Android", $"ADB streamed command failed: {args}", ex);
                onLine("[!] " + ex.Message);
                return -1;
            }
        }
    }
}
