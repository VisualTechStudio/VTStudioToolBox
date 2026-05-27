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
        public DashboardPage()
        {
            this.InitializeComponent();
            this.Loaded += DashboardPage_Loaded;
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateWelcomeMessage();
            await LoadSystemInfoWithCacheAsync();
        }

        private void UpdateWelcomeMessage()
        {
            string username = Environment.UserName;
            string greeting = GetGreetingByTime();
            WelcomeText.Text = $"{greeting}，{username}";
        }

        private string GetGreetingByTime()
        {
            int hour = DateTime.Now.Hour;
            if (hour >= 5 && hour < 9) return "早晨";
            if (hour >= 9 && hour < 12) return "上午好";
            if (hour >= 12 && hour < 14) return "中午好";
            if (hour >= 14 && hour < 18) return "下午好";
            if (hour >= 18 && hour < 22) return "晚上好";
            return "很晚了，早点睡";
        }

        private async Task LoadSystemInfoWithCacheAsync()
        {
            try
            {
                // 优先从文件缓存加载，实现秒开
                var fileCached = FileCacheManager.Get<SystemInfo>("SystemInfo");
                if (fileCached != null)
                {
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
                    // 没有缓存，显示加载中
                    var info = await Task.Run(GetSystemInfo);
                    FileCacheManager.Set("SystemInfo", info, TimeSpan.FromHours(24));
                    UpdateUIWithSystemInfo(info);
                }
            }
            catch
            {
                LoadingBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateUIWithSystemInfo(SystemInfo info)
        {
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
            SystemInfoText.Text = info.OSInfo;
            SystemVersionText.Text = info.Version;
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
                info.Display = "加载中...";
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
                info.OSInfo = $"获取系统信息时出错：{ex.Message}";
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
                        string caption = os["Caption"]?.ToString() ?? "未知";
                        string architecture = os["OSArchitecture"]?.ToString() ?? "64位";
                        string buildNumber = os["BuildNumber"]?.ToString() ?? "";
                        string version = os["Version"]?.ToString() ?? "";
                        string installDate = os["InstallDate"]?.ToString() ?? "";
                        string lastBootUpTime = os["LastBootUpTime"]?.ToString() ?? "";

                        string cleanCaption = caption.Replace("Microsoft", "").Trim();

                        if (architecture.Contains("64")) architecture = "X64";
                        else if (architecture.Contains("32")) architecture = "X86";

                        info.OSInfo = $"{cleanCaption} {architecture}";

                        string displayVersion = GetDisplayVersion(buildNumber);
                        info.Version = string.IsNullOrEmpty(displayVersion) ? version : $"{displayVersion} {version}";

                        info.InstallTime = FormatWmiDateTime(installDate);
                        info.BootTime = FormatUptime(lastBootUpTime);
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
                        info.Manufacturer = cs["Manufacturer"]?.ToString() ?? "未知";
                        info.Model = cs["Model"]?.ToString() ?? "未知";
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
                        string product = board["Product"]?.ToString()?.Trim() ?? "未知";
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
                        string name = CleanCpuName(cpu["Name"]?.ToString() ?? "未知");
                        string cores = cpu["NumberOfCores"]?.ToString() ?? "0";
                        string threads = cpu["NumberOfLogicalProcessors"]?.ToString() ?? "0";
                        string maxSpeed = cpu["MaxClockSpeed"]?.ToString() ?? "0";

                        if (int.TryParse(maxSpeed, out int mhz) && mhz > 0)
                        {
                            double ghz = mhz / 1000.0;
                            info.CPU = $"{name} ({cores}核心/{threads}线程 {ghz:F1}GHz)";
                        }
                        else
                        {
                            info.CPU = $"{name} ({cores}核心/{threads}线程)";
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

                        string brand = mem["Manufacturer"]?.ToString()?.Trim() ?? "未知";
                        string part = mem["PartNumber"]?.ToString()?.Trim() ?? "未知颗粒";
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
                    info.RAM = "未知";
                    return;
                }

                double totalGB = totalBytes / (1024.0 * 1024.0 * 1024.0);

                string freqDisplay = "未知";
                string ddrType = "未知";

                if (speedList.Count > 0)
                {
                    var mostCommonSpeed = speedList.GroupBy(x => x)
                                                   .OrderByDescending(g => g.Count())
                                                   .First()
                                                   .Key;
                    freqDisplay = $"{mostCommonSpeed}MHz";
                    if (speedList.Distinct().Count() > 1)
                    {
                        freqDisplay += " (混频)";
                    }

                    if (mostCommonSpeed >= 4800) ddrType = "DDR5";
                    else if (mostCommonSpeed >= 2133) ddrType = "DDR4";
                    else if (mostCommonSpeed >= 800) ddrType = "DDR3";
                    else if (mostCommonSpeed > 0) ddrType = "DDR2/早";
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

        private long GetVRAMFromRegistry(string gpuName)
        {
            try
            {
                string lowerName = gpuName.ToLower();

                long vram = 0;

                if (lowerName.Contains("nvidia"))
                {
                    vram = GetNvidiaVRAM();
                }
                else if (lowerName.Contains("amd") || lowerName.Contains("radeon"))
                {
                    vram = GetAMDVRAM();
                }

                if (vram > 0 && IsValidVRAMValue(vram))
                {
                    return vram;
                }
            }
            catch { }

            return 0;
        }

        private bool IsValidVRAMValue(long vramBytes)
        {
            double gb = vramBytes / (1024.0 * 1024.0 * 1024.0);
            return gb >= 1.0 && gb <= 24.0;
        }

        private long GetNvidiaVRAM()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                {
                    if (key == null) return 0;

                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        using (var subKey = key.OpenSubKey(subKeyName))
                        {
                            if (subKey == null) continue;

                            object vramObj = subKey.GetValue("HardwareInformation.MemorySize");
                            if (vramObj != null)
                            {
                                string vramStr = vramObj.ToString();
                                if (long.TryParse(vramStr, System.Globalization.NumberStyles.HexNumber, null, out long vram))
                                {
                                    return vram;
                                }
                                if (long.TryParse(vramStr, out long vram2))
                                {
                                    return vram2;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return 0;
        }

        private long GetAMDVRAM()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                {
                    if (key == null) return 0;

                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        using (var subKey = key.OpenSubKey(subKeyName))
                        {
                            if (subKey == null) continue;

                            object vramObj = subKey.GetValue("HardwareInformation.MemorySize");
                            if (vramObj != null)
                            {
                                string vramStr = vramObj.ToString();
                                if (long.TryParse(vramStr, System.Globalization.NumberStyles.HexNumber, null, out long vram))
                                {
                                    return vram;
                                }
                                if (long.TryParse(vramStr, out long vram2))
                                {
                                    return vram2;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return 0;
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
                        string name = gpu["Name"]?.ToString()?.Trim() ?? "未知";

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

                        string vramStr = "未知";
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
                        if (vramStr != "未知") displayName += $" ({vramStr})";

                        if (!string.IsNullOrEmpty(gpuDriver))
                        {
                            displayName += $"\n驱动: {gpuDriver}";
                            if (!string.IsNullOrEmpty(gpuDriverDate))
                                displayName += $" ({gpuDriverDate})";
                        }
                        else
                        {
                            displayName += "\n驱动: 未检测到";
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

                        string sizeStr = "未知";
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
                        if (!string.IsNullOrEmpty(name))
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
            if (string.IsNullOrEmpty(cpuName)) return "未知";
            int atIndex = cpuName.IndexOf('@');
            if (atIndex > 0) return cpuName.Substring(0, atIndex).Trim();
            return cpuName.Trim();
        }

        private string FormatWmiDateTime(string wmiDateTime)
        {
            try
            {
                if (string.IsNullOrEmpty(wmiDateTime)) return "未知";
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
                    return $"{dt:yyyy年MM月dd日 HH:mm:ss}";
                }
            }
            catch { }
            return wmiDateTime;
        }

        private string FormatUptime(string wmiDateTime)
        {
            try
            {
                if (string.IsNullOrEmpty(wmiDateTime)) return "未知";
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

                    return days > 0 ? $"{days}天{hours}小时{minutes}分钟{seconds}秒" : $"{hours}小时{minutes}分钟{seconds}秒";
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
            if (items == null || items.Count == 0) return "未知";
            if (items.Count == 1) return items[0];
            return string.Join("\n", items);
        }

        private string GetDisplayVersion(string buildNumber)
        {
            if (string.IsNullOrEmpty(buildNumber)) return "";

            if (!int.TryParse(buildNumber, out int build)) return "";

            return build switch
            {
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

        private class SystemInfo
        {
            public string Manufacturer { get; set; } = "";
            public string Motherboard { get; set; } = "";
            public string Model { get; set; } = "";
            public string CPU { get; set; } = "";
            public string RAM { get; set; } = "";
            public string GPU { get; set; } = "";
            public string HDD { get; set; } = "";
            public string Network { get; set; } = "";
            public string Audio { get; set; } = "";
            public string Display { get; set; } = "";

            public string ComputerName { get; set; } = "";
            public string OSInfo { get; set; } = "";
            public string Version { get; set; } = "";
            public string InstallTime { get; set; } = "";
            public string BootTime { get; set; } = "";
        }
    }
}