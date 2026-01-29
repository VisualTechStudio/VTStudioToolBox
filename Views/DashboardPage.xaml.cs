using Hardware.Info;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
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
                var cached = CacheManager.Get<SystemInfo>("SystemInfo");
                if (cached != null)
                {
                    UpdateUIWithSystemInfo(cached);
                    _ = Task.Run(async () =>
                    {
                        var fresh = await Task.Run(GetSystemInfo);
                        CacheManager.Set("SystemInfo", fresh, TimeSpan.FromMinutes(5));
                    });
                }
                else
                {
                    var info = await Task.Run(GetSystemInfo);
                    CacheManager.Set("SystemInfo", info, TimeSpan.FromMinutes(5));
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
            var hardwareInfo = new HardwareInfo();

            try
            {
                hardwareInfo.RefreshAll();

                info.ComputerName = Environment.MachineName;

                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
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

                using (var csSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject cs in csSearcher.Get())
                    {
                        info.Manufacturer = cs["Manufacturer"]?.ToString() ?? "未知";
                        info.Model = cs["Model"]?.ToString() ?? "未知";
                        break;
                    }
                }

                if (hardwareInfo.MotherboardList.Count > 0)
                {
                    var board = hardwareInfo.MotherboardList[0];
                    info.Motherboard = $"{board.Manufacturer ?? ""} {board.Product ?? "未知"}".Trim();
                }

                if (hardwareInfo.CpuList.Count > 0)
                {
                    var cpu = hardwareInfo.CpuList[0];
                    string name = CleanCpuName(cpu.Name ?? "未知");
                    string cores = cpu.NumberOfCores.ToString();
                    string threads = cpu.NumberOfLogicalProcessors.ToString();
                    string maxSpeed = cpu.MaxClockSpeed > 0 ? cpu.MaxClockSpeed.ToString() : "未知";

                    info.CPU = $"{name} ({cores}核心/{threads}线程 {maxSpeed}MHz)";
                }

                if (hardwareInfo.MemoryList.Count > 0)
                {
                    var ramModules = new List<string>();
                    long totalBytes = 0;
                    var speedList = new List<int>();
                    string ddrType = "未知";

                    var memoryGroups = new Dictionary<string, Dictionary<string, (int Count, long Capacity)>>();

                    foreach (var mem in hardwareInfo.MemoryList)
                    {
                        long capacity = (long)mem.Capacity;
                        totalBytes += capacity;

                        string brand = string.IsNullOrWhiteSpace(mem.Manufacturer) ? "未知" : mem.Manufacturer.Trim();
                        string part = string.IsNullOrWhiteSpace(mem.PartNumber) ? "未知颗粒" : mem.PartNumber.Trim();
                        int speed = (int)mem.Speed;

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

                    double totalGB = totalBytes / (1024.0 * 1024.0 * 1024.0);

                    string freqDisplay = "未知";
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
                    }

                    if (freqDisplay != "未知" && freqDisplay != "0MHz")
                    {
                        int mainFreq = int.Parse(freqDisplay.Split('M')[0]);
                        if (mainFreq >= 4800) ddrType = "DDR5";
                        else if (mainFreq >= 2133) ddrType = "DDR4";
                        else if (mainFreq >= 800) ddrType = "DDR3";
                        else if (mainFreq > 0) ddrType = "DDR2/早";
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
                if (hardwareInfo.VideoControllerList.Count > 0)
                {
                    var gpuList = new List<string>();

                    var driverDict = new Dictionary<string, (string Version, string Date)>(StringComparer.OrdinalIgnoreCase);

                    using (var driverSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPSignedDriver WHERE DeviceClass = 'DISPLAY'"))
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

                    foreach (var gpu in hardwareInfo.VideoControllerList)
                    {
                        string name = gpu.Name?.Trim() ?? "未知";

                        long vramBytes = (long)gpu.AdapterRAM;
                        string vramStr = "未知";
                        if (vramBytes > 0)
                        {
                            double gb = vramBytes / (1024.0 * 1024.0 * 1024.0);
                            vramStr = gb >= 1 ? $"{gb:F1}GB" : $"{(vramBytes / (1024.0 * 1024.0)):F0}MB";
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

                    info.GPU = FormatVerticalList(gpuList);
                }

                if (hardwareInfo.DriveList.Count > 0)
                {
                    var drives = new List<string>();
                    foreach (var drive in hardwareInfo.DriveList)
                    {
                        string model = drive.Model?.Trim() ?? "";
                        long sizeBytes = (long)drive.Size;

                        if (string.IsNullOrEmpty(model) || sizeBytes < 1024L * 1024 * 1024 * 10) continue;

                        string sizeStr = "未知";
                        if (sizeBytes > 0)
                        {
                            double tb = sizeBytes / (1024.0 * 1024.0 * 1024.0 * 1024.0);
                            sizeStr = tb >= 1 ? $"{tb:F1}TB" : $"{(sizeBytes / (1024.0 * 1024.0 * 1024.0)):F0}GB";
                        }

                        drives.Add($"{model} ({sizeStr})");
                    }
                    info.HDD = FormatVerticalList(drives);
                }

                if (hardwareInfo.NetworkAdapterList.Count > 0)
                {
                    var adapters = new List<string>();
                    foreach (var adapter in hardwareInfo.NetworkAdapterList)
                    {
                        string name = adapter.Name?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(name) &&
                            !name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("WAN Miniport") &&
                            !name.Contains("Bluetooth") &&
                            !name.Contains("蓝牙") &&
                            !name.Contains("Loopback") &&
                            !string.IsNullOrEmpty(adapter.MACAddress))
                        {
                            adapters.Add(name);
                        }
                    }
                    info.Network = FormatVerticalList(adapters);
                }

                if (hardwareInfo.SoundDeviceList.Count > 0)
                {
                    var audioDevices = new List<string>();
                    foreach (var sound in hardwareInfo.SoundDeviceList)
                    {
                        string name = sound.Name?.Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            audioDevices.Add(name);
                        }
                    }
                    info.Audio = FormatVerticalList(audioDevices);
                }

                info.Display = GetMonitorInfo();
            }
            catch (Exception ex)
            {
                info.OSInfo = $"获取系统信息时出错：{ex.Message}";
            }

            return info;
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