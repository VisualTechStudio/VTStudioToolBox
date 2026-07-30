using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Win32;
using SharpDX;
using SharpDX.DXGI;
using VTStudioToolBox.Helpers;

namespace VTStudioToolBox.Views
{
    public sealed partial class DashboardPage : Page
    {
        private DispatcherTimer? _bootTimer;
        private DateTime? _bootTime;

        public DashboardPage()
        {
            this.InitializeComponent();
            UpdateLanguage();
            this.Loaded += DashboardPage_Loaded;
            this.Unloaded += DashboardPage_Unloaded;
        }

        private void UpdateLanguage()
        {
            PageTitle.Text = LanguageHelper.GetString("DashboardTitle");
            PageSubtitle.Text = LanguageHelper.GetString("DashboardSubtitle");
            LoadingText.Text = LanguageHelper.GetString("LoadingHardware");
            HardwareInfoHeader.Text = LanguageHelper.GetString("HardwareInfo");
            SystemInfoHeader.Text = LanguageHelper.GetString("SystemInfo");

            LabelManufacturer.Text = LanguageHelper.GetString("LabelManufacturer");
            LabelMotherboard.Text = LanguageHelper.GetString("LabelMotherboard");
            LabelModel.Text = LanguageHelper.GetString("LabelModel");
            LabelCPU.Text = LanguageHelper.GetString("LabelCPU");
            LabelRAM.Text = LanguageHelper.GetString("LabelRAM");
            LabelGPU.Text = LanguageHelper.GetString("LabelGPU");
            LabelDisk.Text = LanguageHelper.GetString("LabelDisk");
            LabelNIC.Text = LanguageHelper.GetString("LabelNIC");
            LabelAudio.Text = LanguageHelper.GetString("LabelAudio");
            LabelMonitor.Text = LanguageHelper.GetString("LabelMonitor");

            LabelName.Text = LanguageHelper.GetString("LabelName");
            LabelSystem.Text = LanguageHelper.GetString("LabelSystem");
            LabelInstallTime.Text = LanguageHelper.GetString("LabelInstallTime");
            LabelUptime.Text = LanguageHelper.GetString("LabelUptime");
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Dev("Dashboard", "Page loaded");
            UpdateWelcomeMessage();
            await LoadSystemInfoWithCacheAsync();
            StartBootTimer();
        }

        private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _bootTimer?.Stop();
            _bootTimer = null;
        }

        private void UpdateWelcomeMessage()
        {
            string displayName = GetDisplayName();
            string greeting = GetGreetingByTime();
            WelcomeText.Text = $"{greeting}，{displayName}";
        }

        private string GetDisplayName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT FullName FROM Win32_UserAccount WHERE Name = '{Environment.UserName}'");
                foreach (ManagementObject user in searcher.Get())
                {
                    string fullName = user["FullName"]?.ToString() ?? "";
                    // If FullName is empty, it's a local account - use folder name
                    if (string.IsNullOrWhiteSpace(fullName))
                        return Environment.UserName;
                    return fullName;
                }
            }
            catch { }
            return Environment.UserName;
        }

        private string GetGreetingByTime()
        {
            int hour = DateTime.Now.Hour;
            if (hour >= 5 && hour < 9) return LanguageHelper.GetString("GreetingEarlyMorning");
            if (hour >= 9 && hour < 12) return LanguageHelper.GetString("GreetingMorning");
            if (hour >= 12 && hour < 14) return LanguageHelper.GetString("GreetingNoon");
            if (hour >= 14 && hour < 18) return LanguageHelper.GetString("GreetingAfternoon");
            if (hour >= 18 && hour < 22) return LanguageHelper.GetString("GreetingEvening");
            return LanguageHelper.GetString("GreetingLateNight");
        }

        private void StartBootTimer()
        {
            _bootTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _bootTimer.Tick += (s, e) =>
            {
                if (_bootTime.HasValue)
                {
                    TimeSpan uptime = DateTime.Now - _bootTime.Value;
                    int days = (int)uptime.TotalDays;
                    int hours = uptime.Hours;
                    int minutes = uptime.Minutes;
                    int seconds = uptime.Seconds;
                    SystemBootTimeText.Text = days > 0
                        ? LanguageHelper.GetString("UptimeDaysHours", days, hours, minutes, seconds)
                        : LanguageHelper.GetString("UptimeHoursMinutes", hours, minutes, seconds);
                }
            };
            _bootTimer.Start();
        }

        private async Task LoadSystemInfoWithCacheAsync()
        {
            try
            {
                // 优先从文件缓存加载，实现秒开
                var fileCached = FileCacheManager.Get<SystemInfo>("SystemInfo");
                Logger.Dev("Dashboard", $"Cache result: {(fileCached == null ? "null" : $"Manuf={fileCached.Manufacturer}, CPU={fileCached.CPU?.Substring(0, Math.Min(20, fileCached.CPU?.Length ?? 0))}")}");
                if (fileCached != null)
                {
                    Logger.Info("Dashboard", "Loading system info from cache");
                    // 立即显示缓存数据
                    UpdateUIWithSystemInfo(fileCached);

                    // 后台静默刷新数据
                    _ = Task.Run(async () =>
                    {
                        var fresh = await Task.Run(GetSystemInfo);
                        FileCacheManager.Set("SystemInfo", fresh, TimeSpan.FromHours(24));

                        // 如果数据有变化，更新UI
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            UpdateUIWithSystemInfo(fresh);
                        });
                    });
                }
                else
                {
                    Logger.Info("Dashboard", "No cache found, querying system info");
                    // 没有缓存，显示加载中
                    var info = await Task.Run(GetSystemInfo);
                    FileCacheManager.Set("SystemInfo", info, TimeSpan.FromHours(24));
                    UpdateUIWithSystemInfo(info);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Dashboard", "Failed to load system info", ex);
                LoadingBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateUIWithSystemInfo(SystemInfo info)
        {
            Logger.Dev("Dashboard", $"UpdateUI: Manufacturer={info.Manufacturer}, CPU={info.CPU?.Substring(0, Math.Min(20, info.CPU?.Length ?? 0))}...");
            HardwareManufacturerText.Text = info.Manufacturer;
            MotherboardText.Text = info.Motherboard;
            HardwareModelText.Text = info.Model;
            HardwareCPUText.Text = info.CPU;
            RAMText.Text = info.RAM;
            HardwareGPUText.Text = info.GPU;
            HDDText.Text = info.HDD;
            NetworkText.Text = info.Network;
            AudioText.Text = info.Audio;
            DisplayText.Text = info.Display;

            SystemComputerNameText.Text = info.ComputerName;
            SystemInfoText.Text = $"{info.OSInfo} {info.Version}";
            SystemInstallTimeText.Text = info.InstallTime;
            SystemBootTimeText.Text = info.BootTime;

            LoadingBorder.Visibility = Visibility.Collapsed;
            HardwareInfoBorder.Visibility = Visibility.Visible;
            SystemInfoBorder.Visibility = Visibility.Visible;
        }

        private SystemInfo GetSystemInfo()
        {
            var info = new SystemInfo();

            try
            {
                info.ComputerName = Environment.MachineName;

                // 并行执行所有WMI查询
                var tasks = new List<Task>();

                tasks.Add(Task.Run(() => GetOSInfo(info)));
                tasks.Add(Task.Run(() => GetComputerSystemInfo(info)));
                tasks.Add(Task.Run(() => GetMotherboardInfo(info)));
                tasks.Add(Task.Run(() => GetCPUInfo(info)));
                tasks.Add(Task.Run(() => GetRAMInfo(info)));
                tasks.Add(Task.Run(() => GetGPUInfo(info)));
                tasks.Add(Task.Run(() => GetHDDInfo(info)));
                tasks.Add(Task.Run(() => GetNetworkInfo(info)));
                tasks.Add(Task.Run(() => GetAudioInfo(info)));

                Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10));

                // 显示器信息在后台加载
                info.Display = LanguageHelper.GetString("LoadingDots");
                _ = Task.Run(() =>
                {
                    var displayInfo = GetMonitorInfo();
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        DisplayText.Text = displayInfo;
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Dashboard", "Failed to get system info", ex);
                info.OSInfo = LanguageHelper.GetString("ErrorSystemInfo", ex.Message);
            }

            return info;
        }

        private void GetOSInfo(SystemInfo info)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption, OSArchitecture, BuildNumber, Version, InstallDate, LastBootUpTime FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject os in searcher.Get())
                    {
                        string caption = os["Caption"]?.ToString() ?? LanguageHelper.GetString("Unknown");
                        string architecture = os["OSArchitecture"]?.ToString() ?? LanguageHelper.GetString("Bit64");
                        string buildNumber = os["BuildNumber"]?.ToString() ?? "";
                        string version = os["Version"]?.ToString() ?? "";
                        string installDate = os["InstallDate"]?.ToString() ?? "";
                        string lastBootUpTime = os["LastBootUpTime"]?.ToString() ?? "";

                        // 获取完整版本号（含UBR修订号）
                        string ubr = "";
                        try
                        {
                            using var ubrKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                            if (ubrKey != null)
                            {
                                ubr = ubrKey.GetValue("UBR")?.ToString() ?? "";
                            }
                        }
                        catch { }
                        string fullVersion = string.IsNullOrEmpty(ubr) ? version : $"{buildNumber}.{ubr}";

                        string cleanCaption = caption.Replace("Microsoft", "").Trim();

                        if (architecture.Contains("64")) architecture = "X64";
                        else if (architecture.Contains("32")) architecture = "X86";

                        info.OSInfo = $"{cleanCaption} {architecture}";

                        string displayVersion = GetDisplayVersion(buildNumber);
                        info.Version = string.IsNullOrEmpty(displayVersion) ? fullVersion : $"{displayVersion} {fullVersion}";

                        info.InstallTime = FormatWmiDateTime(installDate);
                        info.BootTime = FormatUptime(lastBootUpTime);

                        // Store parsed boot time for real-time timer
                        if (!string.IsNullOrEmpty(lastBootUpTime) && lastBootUpTime.Length >= 14)
                        {
                            try
                            {
                                string datePart = lastBootUpTime.Substring(0, 14);
                                int y = int.Parse(datePart.Substring(0, 4));
                                int m = int.Parse(datePart.Substring(4, 2));
                                int d = int.Parse(datePart.Substring(6, 2));
                                int h = int.Parse(datePart.Substring(8, 2));
                                int mi = int.Parse(datePart.Substring(10, 2));
                                int s = int.Parse(datePart.Substring(12, 2));
                                _bootTime = new DateTime(y, m, d, h, mi, s);
                            }
                            catch { }
                        }
                        break;
                    }
                }
            }
            catch { }
        }

        private void GetComputerSystemInfo(SystemInfo info)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject cs in searcher.Get())
                    {
                        info.Manufacturer = cs["Manufacturer"]?.ToString() ?? LanguageHelper.GetString("Unknown");
                        info.Model = cs["Model"]?.ToString() ?? LanguageHelper.GetString("Unknown");
                        break;
                    }
                }
            }
            catch { }
        }

        private void GetMotherboardInfo(SystemInfo info)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject board in searcher.Get())
                    {
                        string manufacturer = board["Manufacturer"]?.ToString()?.Trim() ?? "";
                        string product = board["Product"]?.ToString()?.Trim() ?? LanguageHelper.GetString("Unknown");
                        info.Motherboard = $"{manufacturer} {product}".Trim();
                        break;
                    }
                }
            }
            catch { }
        }

        private void GetCPUInfo(SystemInfo info)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"))
                {
                    foreach (ManagementObject cpu in searcher.Get())
                    {
                        string rawName = cpu["Name"]?.ToString() ?? LanguageHelper.GetString("Unknown");
                        if (rawName.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) continue;

                        string name = CleanCpuName(rawName);
                        string cores = cpu["NumberOfCores"]?.ToString() ?? "0";
                        string threads = cpu["NumberOfLogicalProcessors"]?.ToString() ?? "0";
                        string maxSpeed = cpu["MaxClockSpeed"]?.ToString() ?? "0";

                        if (int.TryParse(maxSpeed, out int mhz) && mhz > 0)
                        {
                            double ghz = mhz / 1000.0;
                            info.CPU = $"{name} {LanguageHelper.GetString("CoresThreadsGHz", cores, threads, ghz)}";
                        }
                        else
                        {
                            info.CPU = $"{name} {LanguageHelper.GetString("CoresThreads", cores, threads)}";
                        }
                        break;
                    }
                }
            }
            catch { }
        }

        private void GetRAMInfo(SystemInfo info)
        {
            try
            {
                long totalBytes = 0;
                var speedList = new List<int>();
                var memoryGroups = new Dictionary<string, Dictionary<string, (int Count, long Capacity)>>();

                using (var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, Manufacturer, PartNumber FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject mem in searcher.Get())
                    {
                        long capacity = Convert.ToInt64(mem["Capacity"] ?? 0);
                        totalBytes += capacity;

                        string brand = mem["Manufacturer"]?.ToString()?.Trim() ?? LanguageHelper.GetString("Unknown");
                        string part = mem["PartNumber"]?.ToString()?.Trim() ?? LanguageHelper.GetString("UnknownRAMPart");
                        int speed = Convert.ToInt32(mem["Speed"] ?? 0);

                        if (!memoryGroups.ContainsKey(brand))
                        {
                            memoryGroups[brand] = new Dictionary<string, (int Count, long Capacity)>();
                        }

                        if (!memoryGroups[brand].ContainsKey(part))
                        {
                            memoryGroups[brand][part] = (1, capacity);
                        }
                        else
                        {
                            var current = memoryGroups[brand][part];
                            memoryGroups[brand][part] = (current.Count + 1, current.Capacity);
                        }

                        if (speed > 0) speedList.Add(speed);
                    }
                }

                if (totalBytes == 0)
                {
                    info.RAM = LanguageHelper.GetString("Unknown");
                    return;
                }

                double totalGB = totalBytes / (1024.0 * 1024.0 * 1024.0);

                string freqDisplay = LanguageHelper.GetString("Unknown");
                string ddrType = LanguageHelper.GetString("Unknown");

                if (speedList.Count > 0)
                {
                    var mostCommonSpeed = speedList.GroupBy(x => x)
                                                   .OrderByDescending(g => g.Count())
                                                   .First()
                                                   .Key;
                    freqDisplay = $"{mostCommonSpeed}MHz";
                    if (speedList.Distinct().Count() > 1)
                    {
                        freqDisplay += LanguageHelper.GetString("MixedFrequency");
                    }

                    if (mostCommonSpeed >= 4800) ddrType = "DDR5";
                    else if (mostCommonSpeed >= 2133) ddrType = "DDR4";
                    else if (mostCommonSpeed >= 800) ddrType = "DDR3";
                    else if (mostCommonSpeed > 0) ddrType = LanguageHelper.GetString("DDROld");
                }

                var ramDisplay = new List<string>();

                foreach (var brandGroup in memoryGroups)
                {
                    foreach (var partGroup in brandGroup.Value)
                    {
                        string partNumber = partGroup.Key;
                        int count = partGroup.Value.Count;
                        double gb = partGroup.Value.Capacity / (1024.0 * 1024.0 * 1024.0);

                        ramDisplay.Add($"{brandGroup.Key} ({count} x {gb:F0}GB [{partNumber}])");
                    }
                }

                string ramText = $"{totalGB:F2}GB {ddrType} {freqDisplay}";
                if (ramDisplay.Count > 0)
                {
                    ramText += "\n" + string.Join("\n", ramDisplay);
                }

                info.RAM = ramText;
            }
            catch { }
        }

        private bool IsValidVRAMValue(long vramBytes)
        {
            double gb = vramBytes / (1024.0 * 1024.0 * 1024.0);
            return gb >= 1.0 && gb <= 24.0;
        }

        private Dictionary<string, long> GetDirectXVRAM()
        {
            var vramDict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var factory = new SharpDX.DXGI.Factory1())
                {
                    int adapterCount = factory.GetAdapterCount();
                    for (int i = 0; i < adapterCount; i++)
                    {
                        using (var adapter = factory.GetAdapter1(i))
                        {
                            var desc = adapter.Description;
                            string name = desc.Description.ToLower();

                            if (name.Contains("intel"))
                            {
                                vramDict[desc.Description] = 128 * 1024 * 1024;
                            }
                            else
                            {
                                long vram = desc.DedicatedVideoMemory;
                                if (vram > 0 && IsValidVRAMValue(vram))
                                {
                                    vramDict[desc.Description] = vram;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return vramDict;
        }

        private void GetGPUInfo(SystemInfo info)
        {
            try
            {
                var gpuList = new List<string>();
                var driverDict = new Dictionary<string, (string Version, string Date)>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    using (var driverSearcher = new ManagementObjectSearcher("SELECT DeviceName, DriverVersion, DriverDate FROM Win32_PnPSignedDriver WHERE DeviceClass = 'DISPLAY'"))
                    {
                        foreach (ManagementObject driver in driverSearcher.Get())
                        {
                            string devName = driver["DeviceName"]?.ToString()?.Trim() ?? "";
                            string drvVer = driver["DriverVersion"]?.ToString() ?? "";
                            string drvDate = driver["DriverDate"]?.ToString() ?? "";

                            string formattedDate = "";
                            if (!string.IsNullOrEmpty(drvDate) && drvDate.Length >= 14)
                            {
                                string datePart = drvDate.Substring(0, 14);
                                int year = int.Parse(datePart.Substring(0, 4));
                                int month = int.Parse(datePart.Substring(4, 2));
                                int day = int.Parse(datePart.Substring(6, 2));
                                formattedDate = $"{year}/{month:D2}/{day:D2}";
                            }

                            if (!string.IsNullOrEmpty(drvVer) && !string.IsNullOrEmpty(devName))
                            {
                                driverDict[devName] = (drvVer, formattedDate);
                            }
                        }
                    }
                }
                catch { }

                var directXVRAMDict = GetDirectXVRAM();

                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (ManagementObject gpu in searcher.Get())
                    {
                        string name = gpu["Name"]?.ToString()?.Trim() ?? LanguageHelper.GetString("Unknown");

                        long vramBytes = Convert.ToInt64(gpu["AdapterRAM"] ?? 0);

                        foreach (var kv in directXVRAMDict)
                        {
                            string dxName = kv.Key.ToLower();
                            string wmiName = name.ToLower();

                            if (dxName.Contains("nvidia") && wmiName.Contains("nvidia") ||
                                dxName.Contains("amd") && wmiName.Contains("amd") ||
                                dxName.Contains("radeon") && wmiName.Contains("radeon") ||
                                dxName.Contains("geforce") && wmiName.Contains("geforce") ||
                                dxName.Contains("intel") && wmiName.Contains("intel"))
                            {
                                vramBytes = kv.Value;
                                break;
                            }
                        }

                        string vramStr = LanguageHelper.GetString("Unknown");
                        if (vramBytes > 0)
                        {
                            double gb = vramBytes / (1024.0 * 1024.0 * 1024.0);
                            vramStr = gb >= 1 ? $"{gb:F0}GB" : $"{vramBytes / (1024.0 * 1024.0):F0}MB";
                        }

                        string gpuDriver = "";
                        string gpuDriverDate = "";

                        foreach (var kv in driverDict)
                        {
                            string devNameLower = kv.Key.ToLower();
                            string gpuNameLower = name.ToLower();

                            if ((devNameLower.Contains("nvidia") && gpuNameLower.Contains("nvidia")) ||
                                (devNameLower.Contains("geforce") && gpuNameLower.Contains("geforce")) ||
                                (devNameLower.Contains("amd") && gpuNameLower.Contains("amd")) ||
                                (devNameLower.Contains("radeon") && gpuNameLower.Contains("radeon")))
                            {
                                gpuDriver = kv.Value.Version;
                                gpuDriverDate = kv.Value.Date;
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(gpuDriver) && driverDict.Count > 0)
                        {
                            var first = driverDict.First();
                            gpuDriver = first.Value.Version;
                            gpuDriverDate = first.Value.Date;
                        }

                        string displayName = name;
                        if (vramStr != LanguageHelper.GetString("Unknown")) displayName += $" ({vramStr})";

                        if (!string.IsNullOrEmpty(gpuDriver))
                        {
                            displayName += $"\n{LanguageHelper.GetString("DriverLabel", gpuDriver)}";
                            if (!string.IsNullOrEmpty(gpuDriverDate))
                                displayName += $" ({gpuDriverDate})";
                        }
                        else
                        {
                            displayName += $"\n{LanguageHelper.GetString("DriverNotDetected")}";
                        }

                        if (!name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Microsoft Basic Display", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Basic Display Adapter", StringComparison.OrdinalIgnoreCase))
                        {
                            gpuList.Add(displayName);
                        }
                    }
                }

                info.GPU = FormatVerticalList(gpuList);
            }
            catch { }
        }

        private void GetHDDInfo(SystemInfo info)
        {
            try
            {
                var drives = new List<string>();
                using (var searcher = new ManagementObjectSearcher("SELECT Model, Size FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject drive in searcher.Get())
                    {
                        string model = drive["Model"]?.ToString()?.Trim() ?? "";
                        long sizeBytes = Convert.ToInt64(drive["Size"] ?? 0);

                        if (string.IsNullOrEmpty(model) || sizeBytes < 1024L * 1024 * 1024 * 10) continue;
                        if (model.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) continue;

                        string sizeStr = LanguageHelper.GetString("Unknown");
                        if (sizeBytes > 0)
                        {
                            double tb = sizeBytes / (1024.0 * 1024.0 * 1024.0 * 1024.0);
                            sizeStr = tb >= 1 ? $"{tb:F1}TB" : $"{(sizeBytes / (1024.0 * 1024.0 * 1024.0)):F0}GB";
                        }

                        drives.Add($"{model} ({sizeStr})");
                    }
                }
                info.HDD = FormatVerticalList(drives);
            }
            catch { }
        }

        private void GetNetworkInfo(SystemInfo info)
        {
            try
            {
                var adapters = new List<string>();
                // 查询所有物理网卡，不限制连接状态
                using (var searcher = new ManagementObjectSearcher("SELECT Name, MACAddress, PhysicalAdapter FROM Win32_NetworkAdapter"))
                {
                    foreach (ManagementObject adapter in searcher.Get())
                    {
                        string name = adapter["Name"]?.ToString()?.Trim() ?? "";
                        string macAddress = adapter["MACAddress"]?.ToString() ?? "";
                        bool isPhysical = adapter["PhysicalAdapter"] != null && (bool)adapter["PhysicalAdapter"];

                        // 只显示物理网卡，排除虚拟设备和软件网卡
                        if (!string.IsNullOrEmpty(name) &&
                            isPhysical &&
                            !string.IsNullOrEmpty(macAddress) &&
                            !name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("蓝牙", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Loopback", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Teredo", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Pseudo", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("6to4", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("ISATAP", StringComparison.OrdinalIgnoreCase))
                        {
                            adapters.Add(name);
                        }
                    }
                }
                info.Network = FormatVerticalList(adapters);
            }
            catch { }
        }

        private void GetAudioInfo(SystemInfo info)
        {
            try
            {
                var audioDevices = new List<string>();
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_SoundDevice"))
                {
                    foreach (ManagementObject sound in searcher.Get())
                    {
                        string name = sound["Name"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(name) &&
                            !name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                        {
                            audioDevices.Add(name);
                        }
                    }
                }
                info.Audio = FormatVerticalList(audioDevices);
            }
            catch { }
        }

        private string GetMonitorInfo()
        {
            var displays = new List<string>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Service = 'monitor'"))
                {
                    foreach (ManagementObject monitor in searcher.Get())
                    {
                        string name = monitor["Name"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(name))
                        {
                            string cleaned = ProcessMonitorName(name);
                            if (!string.IsNullOrEmpty(cleaned)) displays.Add(cleaned);
                        }
                    }
                }

                if (displays.Count == 0)
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DesktopMonitor"))
                    {
                        foreach (ManagementObject monitor in searcher.Get())
                        {
                            string name = monitor["Name"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(name))
                            {
                                string cleaned = ProcessMonitorName(name);
                                if (!string.IsNullOrEmpty(cleaned)) displays.Add(cleaned);
                            }
                        }
                    }
                }
            }
            catch { }

            return FormatVerticalList(displays);
        }

        private string CleanCpuName(string cpuName)
        {
            if (string.IsNullOrEmpty(cpuName)) return LanguageHelper.GetString("Unknown");
            int atIndex = cpuName.IndexOf('@');
            if (atIndex > 0) return cpuName.Substring(0, atIndex).Trim();
            return cpuName.Trim();
        }

        private string FormatWmiDateTime(string wmiDateTime)
        {
            try
            {
                if (string.IsNullOrEmpty(wmiDateTime)) return LanguageHelper.GetString("Unknown");
                if (wmiDateTime.Length >= 14)
                {
                    string datePart = wmiDateTime.Substring(0, 14);
                    int year = int.Parse(datePart.Substring(0, 4));
                    int month = int.Parse(datePart.Substring(4, 2));
                    int day = int.Parse(datePart.Substring(6, 2));
                    int hour = int.Parse(datePart.Substring(8, 2));
                    int minute = int.Parse(datePart.Substring(10, 2));
                    int second = int.Parse(datePart.Substring(12, 2));

                    DateTime dt = new DateTime(year, month, day, hour, minute, second);
                    return LanguageHelper.GetString("DateFormat", dt);
                }
            }
            catch { }
            return wmiDateTime;
        }

        private string FormatUptime(string wmiDateTime)
        {
            try
            {
                if (string.IsNullOrEmpty(wmiDateTime)) return LanguageHelper.GetString("Unknown");
                if (wmiDateTime.Length >= 14)
                {
                    string datePart = wmiDateTime.Substring(0, 14);
                    int year = int.Parse(datePart.Substring(0, 4));
                    int month = int.Parse(datePart.Substring(4, 2));
                    int day = int.Parse(datePart.Substring(6, 2));
                    int hour = int.Parse(datePart.Substring(8, 2));
                    int minute = int.Parse(datePart.Substring(10, 2));
                    int second = int.Parse(datePart.Substring(12, 2));

                    DateTime boot = new DateTime(year, month, day, hour, minute, second);
                    TimeSpan uptime = DateTime.Now - boot;

                    int days = (int)uptime.TotalDays;
                    int hours = uptime.Hours;
                    int minutes = uptime.Minutes;
                    int seconds = uptime.Seconds;

                    return days > 0 ? LanguageHelper.GetString("UptimeDaysHours", days, hours, minutes, seconds) : LanguageHelper.GetString("UptimeHoursMinutes", hours, minutes, seconds);
                }
            }
            catch { }
            return wmiDateTime;
        }

        private string ProcessMonitorName(string monitorName)
        {
            if (string.IsNullOrEmpty(monitorName)) return "";

            string processed = monitorName.Trim();

            if (processed.Contains("(") && processed.Contains(")"))
            {
                int start = processed.LastIndexOf("(");
                int end = processed.LastIndexOf(")");

                if (start >= 0 && end > start)
                {
                    string inside = processed.Substring(start + 1, end - start - 1).Trim();
                    if (!IsGenericName(inside) && !string.IsNullOrEmpty(inside)) return inside;
                }

                processed = processed.Substring(0, start).Trim();
            }

            if (IsGenericName(processed)) return "";

            return processed;
        }

        private bool IsGenericName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;

            string lower = name.ToLower();

            string[] keywords = { "generic", "通用", "默认", "default", "monitor", "监视器", "display", "显示器", "plug", "play", "即插即用" };

            foreach (string kw in keywords)
            {
                if (lower.Contains(kw)) return true;
            }

            bool hasLetter = false, hasNumber = false;
            foreach (char c in name)
            {
                if (char.IsLetter(c)) hasLetter = true;
                if (char.IsDigit(c)) hasNumber = true;
                if (hasLetter && hasNumber) return false;
            }

            return name.Length < 4;
        }

        private string FormatVerticalList(List<string> items)
        {
            if (items == null || items.Count == 0) return LanguageHelper.GetString("Unknown");
            if (items.Count == 1) return items[0];
            return string.Join("\n", items);
        }

        private string GetDisplayVersion(string buildNumber)
        {
            if (string.IsNullOrEmpty(buildNumber)) return "";

            if (!int.TryParse(buildNumber, out int build)) return "";

            return build switch
            {
                >= 28000 => "26H1",
                >= 26000 => "25H2",
                >= 25300 => "25H1",
                >= 22621 => "23H2",
                >= 22600 => "23H1",
                >= 22000 => "22H2",
                >= 20348 => "21H2",
                >= 19045 => "22H2",
                >= 19044 => "21H2",
                >= 19043 => "21H1",
                >= 19042 => "20H2",
                >= 19041 => "2004",
                >= 18363 => "1909",
                >= 18362 => "1903",
                >= 17763 => "1809",
                >= 17134 => "1803",
                >= 16299 => "1709",
                >= 15063 => "1703",
                >= 14393 => "1607",
                >= 10586 => "1511",
                >= 10240 => "1507",
                _ => ""
            };
        }
    }
}

