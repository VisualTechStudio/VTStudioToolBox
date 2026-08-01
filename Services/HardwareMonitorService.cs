using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using LibreHardwareMonitor.Hardware;

#nullable enable

namespace VTStudioToolBox.Services
{
    public class HardwareMonitorService : IDisposable
    {
        private Computer? _computer;
        private bool _disposed;

        public void Open()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };
            _computer.Open();
        }

        public void Update()
        {
            if (_computer == null) return;
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var sub in hw.SubHardware)
                {
                    sub.Update();
                }
            }
        }

        public string DumpAllSensors()
        {
            if (_computer == null) return "(no computer)";
            var sb = new System.Text.StringBuilder();
            foreach (var hw in _computer.Hardware)
            {
                sb.AppendLine($"[{hw.HardwareType}] {hw.Name}");
                foreach (var sensor in hw.Sensors)
                {
                    sb.AppendLine($"  {sensor.SensorType}: {sensor.Name} = {sensor.Value}");
                }
                foreach (var sub in hw.SubHardware)
                {
                    sb.AppendLine($"  [Sub] {sub.Name}");
                    foreach (var sensor in sub.Sensors)
                    {
                        sb.AppendLine($"    {sensor.SensorType}: {sensor.Name} = {sensor.Value}");
                    }
                }
            }
            return sb.ToString();
        }

        public CpuSensorData GetCpuData()
        {
            var data = new CpuSensorData();
            if (_computer == null) return data;

            var vidValues = new HashSet<double>();

            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Cpu) continue;
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.Value == null || sensor.Value == 0) continue;
                    var name = sensor.Name;
                    switch (sensor.SensorType)
                    {
                        case SensorType.Clock when data.Frequency == "--" &&
                            (name.Contains("Average", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Effective", StringComparison.OrdinalIgnoreCase)):
                            data.Frequency = $"{sensor.Value:F0} MHz";
                            break;
                        case SensorType.Load when
                            name == "CPU Total":
                            data.Usage = $"{sensor.Value:F0}%";
                            break;
                        case SensorType.Temperature when sensor.Value > 0 &&
                            (name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("CCD", StringComparison.OrdinalIgnoreCase)):
                            if (data.Temperature == "--")
                                data.Temperature = $"{sensor.Value:F0}°C";
                            break;
                        case SensorType.Voltage when sensor.Value > 0:
                            if (name.Contains("Vcore", StringComparison.OrdinalIgnoreCase) &&
                                !name.Contains("VID", StringComparison.OrdinalIgnoreCase))
                            {
                                data.Voltage = $"{sensor.Value:F3} V";
                            }
                            else if (name.Contains("VID", StringComparison.OrdinalIgnoreCase))
                            {
                                vidValues.Add(Math.Round((double)sensor.Value, 3));
                            }
                            break;
                        case SensorType.Power when sensor.Value > 0:
                            if (name.Contains("Package", StringComparison.OrdinalIgnoreCase) && data.Power == "--")
                                data.Power = $"{sensor.Value:F1} W";
                            break;
                    }
                }
                foreach (var sub in hw.SubHardware)
                {
                    foreach (var sensor in sub.Sensors)
                    {
                        if (sensor.Value == null || sensor.Value == 0) continue;
                        if (sensor.SensorType == SensorType.Temperature && data.Temperature == "--")
                        {
                            if (sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
                                sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                                data.Temperature = $"{sensor.Value:F0}°C";
                        }
                        if (sensor.SensorType == SensorType.Voltage && data.Voltage == "--")
                        {
                            if (sensor.Name.Contains("Vcore", StringComparison.OrdinalIgnoreCase) ||
                                sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                                data.Voltage = $"{sensor.Value:F3} V";
                        }
                    }
                }
            }

            // Filter out static VID values (same voltage on all cores = not actual voltage)
            if (data.Voltage == "--" && vidValues.Count > 0)
            {
                if (vidValues.Count > 1)
                {
                    // VID values vary across cores - use average
                    data.Voltage = $"{vidValues.Average():F3} V";
                }
                // else: all VID values identical = static, leave as "--"
            }

            // Fallback: HWiNFO shared memory
            if (data.Frequency == "--" || data.Temperature == "--" || data.Voltage == "--" || data.Usage == "--")
            {
                var hw = HwInfoReader.GetCpuData();
                if (data.Frequency == "--" && hw.Frequency != "--") data.Frequency = hw.Frequency;
                if (data.Temperature == "--" && hw.Temperature != "--") data.Temperature = hw.Temperature;
                if (data.Voltage == "--" && hw.Voltage != "--") data.Voltage = hw.Voltage;
                if (data.Usage == "--" && hw.Usage != "--") data.Usage = hw.Usage;
            }

            // Fallback: Performance Counter for real-time CPU frequency
            if (data.Frequency == "--")
            {
                var freq = GetCpuFrequencyFromWindows();
                if (freq > 0) data.Frequency = $"{freq:F0} MHz";
            }

            // Fallback: ACPI thermal zone for CPU temperature
            if (data.Temperature == "--")
            {
                var temp = GetCpuTempFromAcpi();
                if (temp > 0) data.Temperature = $"{temp:F0}°C";
            }
            return data;
        }

        public GpuSensorData GetGpuData()
        {
            var data = new GpuSensorData();
            if (_computer == null) return data;

            // Prioritize discrete GPU (NVIDIA > AMD > Intel)
            IHardware? primaryGpu = null;
            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType == HardwareType.GpuNvidia) { primaryGpu = hw; break; }
                if (hw.HardwareType == HardwareType.GpuAmd && primaryGpu == null) primaryGpu = hw;
                if (hw.HardwareType == HardwareType.GpuIntel && primaryGpu == null) primaryGpu = hw;
            }
            if (primaryGpu == null) return data;

            bool isDiscrete = primaryGpu.HardwareType == HardwareType.GpuNvidia;

            foreach (var sensor in primaryGpu.Sensors)
            {
                if (sensor.Value == null) continue;
                var name = sensor.Name;
                switch (sensor.SensorType)
                {
                    case SensorType.Clock:
                        if ((name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase)) &&
                            !name.Contains("SoC", StringComparison.OrdinalIgnoreCase))
                        {
                            if (data.Frequency == "--")
                                data.Frequency = $"{sensor.Value:F0} MHz";
                        }
                        else if (name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                        {
                            data.MemoryFrequency = $"{sensor.Value:F0} MHz";
                        }
                        break;
                    case SensorType.Load when
                        name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                        name == "GPU":
                        data.Usage = $"{sensor.Value:F0}%";
                        break;
                    case SensorType.Temperature when
                        name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Edge", StringComparison.OrdinalIgnoreCase):
                        if (data.Temperature == "--")
                            data.Temperature = $"{sensor.Value:F0}°C";
                        break;
                    case SensorType.Voltage when
                        name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("GPU Core Voltage", StringComparison.OrdinalIgnoreCase):
                        data.Voltage = $"{sensor.Value:F3} V";
                        break;
                    case SensorType.Power when isDiscrete:
                        if (name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                            data.Power = $"{sensor.Value:F0} W";
                        break;
                }
            }
            return data;
        }

        public List<FanSensorData> GetFanData()
        {
            var fans = new List<FanSensorData>();
            if (_computer == null) return fans;

            // Try ASUS ATKACPI first
            if (AsusFanReader.IsAvailable())
            {
                fans = AsusFanReader.GetFanData();
                if (fans.Count > 0) return fans;
            }

            // LibreHardwareMonitorLib
            foreach (var hw in _computer.Hardware)
            {
                CollectFans(hw.Sensors, fans);
                foreach (var sub in hw.SubHardware)
                {
                    CollectFans(sub.Sensors, fans);
                }
            }

            // Fallback to HWiNFO
            if (fans.Count == 0)
            {
                fans = HwInfoReader.GetFanData();
            }
            return fans;
        }

        private static PerformanceCounter? _cpuFreqCounter;
        private static bool _cpuFreqInit;

        private static double GetCpuFrequencyFromWindows()
        {
            try
            {
                if (!_cpuFreqInit)
                {
                    _cpuFreqInit = true;
                    _cpuFreqCounter = new PerformanceCounter("Processor Information", "Actual Frequency", "0,0", true);
                    _cpuFreqCounter.NextValue(); // first read is always 0
                }
                return _cpuFreqCounter?.NextValue() ?? 0;
            }
            catch { return 0; }
        }

        private static double GetCpuTempFromAcpi()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var raw = Convert.ToDouble(obj["CurrentTemperature"]);
                    // Convert from tenths of Kelvin to Celsius
                    return (raw - 2732.0) / 10.0;
                }
            }
            catch { }
            return 0;
        }

        private static void CollectFans(IEnumerable<ISensor> sensors, List<FanSensorData> fans)
        {
            foreach (var sensor in sensors)
            {
                if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue && sensor.Value > 0)
                {
                    fans.Add(new FanSensorData { Name = sensor.Name, Rpm = $"{sensor.Value:F0} RPM" });
                }
            }
        }

        public MemorySensorData GetMemoryData()
        {
            var data = new MemorySensorData();
            if (_computer == null) return data;

            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Memory) continue;
                if (hw.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.Value == null) continue;
                    if (sensor.SensorType == SensorType.Load && sensor.Name == "Memory")
                    {
                        data.Usage = $"{sensor.Value:F0}%";
                    }
                }
            }
            return data;
        }

        public DiskSensorData GetDiskData()
        {
            var data = new DiskSensorData();
            if (_computer == null) return data;

            var drives = new List<string>();
            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Storage) continue;
                long? total = null, used = null;
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used"))
                        used = (long)(sensor.Value ?? 0);
                    if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Total"))
                        total = (long)(sensor.Value ?? 0);
                    if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Used Space"))
                        data.Usage = $"{sensor.Value:F0}%";
                }
                if (total.HasValue && total > 0)
                {
                    double totalGB = total.Value;
                    double usedGB = used ?? 0;
                    drives.Add($"{hw.Name} {usedGB:F0}/{totalGB:F0} GB");
                }
                else
                {
                    drives.Add(hw.Name);
                }
            }
            data.DiskList = string.Join("\n", drives);
            return data;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _computer?.Close();
            _computer = null;
        }
    }

    public class CpuSensorData
    {
        public string Frequency { get; set; } = "--";
        public string Usage { get; set; } = "--";
        public string Temperature { get; set; } = "--";
        public string Voltage { get; set; } = "--";
        public string Power { get; set; } = "--";
    }

    public class GpuSensorData
    {
        public string Frequency { get; set; } = "--";
        public string MemoryFrequency { get; set; } = "--";
        public string Usage { get; set; } = "--";
        public string Temperature { get; set; } = "--";
        public string Voltage { get; set; } = "--";
        public string Power { get; set; } = "--";
    }

    public class FanSensorData
    {
        public string Name { get; set; } = "";
        public string Rpm { get; set; } = "--";
    }

    public class MemorySensorData
    {
        public string Usage { get; set; } = "--";
    }

    public class DiskSensorData
    {
        public string Usage { get; set; } = "--";
        public string DiskList { get; set; } = "";
    }
}
