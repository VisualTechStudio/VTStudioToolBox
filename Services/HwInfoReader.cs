using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace VTStudioToolBox.Services
{
    public static class HwInfoReader
    {
        private const string SharedMemoryName = "HWiFO_SENS_SM2";

        // HWiNFO shared memory header
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HwInfoHeader
        {
            public uint Signature;
            public uint Version;
            public uint NumSensorElements;
            public uint NumReadingElements;
            public uint PollTime;
            public long PollTime64;
        }

        // HWiNFO sensor element
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct HwInfoSensor
        {
            public uint SensorId;
            public uint Instance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Label;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string InternalName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string OriginalLabel;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string LabelWithParent;
        }

        // HWiNFO reading element
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct HwInfoReading
        {
            public uint SensorId;
            public uint SensorIndex;
            public uint ReadingId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Label;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Unit;
            public double Value;
            public double Min;
            public double Max;
            public double Average;
            public uint Flags;
        }

        public static bool IsAvailable()
        {
            try
            {
                using var mmf = MemoryMappedFile.OpenExisting(SharedMemoryName, MemoryMappedFileRights.Read);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static CpuSensorData GetCpuData()
        {
            var data = new CpuSensorData();
            var (readings, sensors) = ReadAll();
            if (readings == null) return data;

            for (int i = 0; i < readings.Length; i++)
            {
                var r = readings[i];
                var sensorName = i < sensors.Length ? sensors[i].Label : "";
                var label = r.Label;

                if (sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
                    sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase))
                {
                    var val = r.Value;
                    if (string.Equals(r.Unit, "MHz", StringComparison.OrdinalIgnoreCase) && val > 0 && data.Frequency == "--")
                        data.Frequency = $"{val:F0} MHz";
                    else if (string.Equals(r.Unit, "°C", StringComparison.OrdinalIgnoreCase) && val > 0 && data.Temperature == "--")
                        data.Temperature = $"{val:F0}°C";
                    else if (string.Equals(r.Unit, "V", StringComparison.OrdinalIgnoreCase) && val > 0 && val < 3 && data.Voltage == "--")
                        data.Voltage = $"{val:F3} V";
                    else if (string.Equals(r.Unit, "%", StringComparison.OrdinalIgnoreCase) && label.Contains("Total", StringComparison.OrdinalIgnoreCase))
                        data.Usage = $"{val:F0}%";
                }
            }
            return data;
        }

        public static GpuSensorData GetGpuData()
        {
            var data = new GpuSensorData();
            var (readings, sensors) = ReadAll();
            if (readings == null) return data;

            // Prefer discrete GPU
            int gpuIndex = -1;
            for (int i = 0; i < sensors.Length; i++)
            {
                if (sensors[i].Label.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                {
                    gpuIndex = i;
                    if (sensors[i].Label.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                        sensors[i].Label.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
                        sensors[i].Label.Contains("GTX", StringComparison.OrdinalIgnoreCase))
                        break;
                }
            }

            if (gpuIndex < 0) return data;

            for (int i = 0; i < readings.Length; i++)
            {
                var r = readings[i];
                if (r.SensorIndex != gpuIndex) continue;
                var label = r.Label;
                var val = r.Value;

                if (string.Equals(r.Unit, "MHz", StringComparison.OrdinalIgnoreCase))
                {
                    if (label.Contains("Core", StringComparison.OrdinalIgnoreCase) && data.Frequency == "--")
                        data.Frequency = $"{val:F0} MHz";
                    else if (label.Contains("Memory", StringComparison.OrdinalIgnoreCase) && data.MemoryFrequency == "--")
                        data.MemoryFrequency = $"{val:F0} MHz";
                }
                else if (string.Equals(r.Unit, "°C", StringComparison.OrdinalIgnoreCase) && val > 0 && data.Temperature == "--")
                    data.Temperature = $"{val:F0}°C";
                else if (string.Equals(r.Unit, "V", StringComparison.OrdinalIgnoreCase) && val > 0 && data.Voltage == "--")
                    data.Voltage = $"{val:F3} V";
                else if (string.Equals(r.Unit, "%", StringComparison.OrdinalIgnoreCase) && label.Contains("Core", StringComparison.OrdinalIgnoreCase) && data.Usage == "--")
                    data.Usage = $"{val:F0}%";
                else if (string.Equals(r.Unit, "W", StringComparison.OrdinalIgnoreCase) && label.Contains("Package", StringComparison.OrdinalIgnoreCase) && data.Power == "--")
                    data.Power = $"{val:F0} W";
            }
            return data;
        }

        public static List<FanSensorData> GetFanData()
        {
            var fans = new List<FanSensorData>();
            var (readings, sensors) = ReadAll();
            if (readings == null) return fans;

            for (int i = 0; i < readings.Length; i++)
            {
                var r = readings[i];
                if (string.Equals(r.Unit, "RPM", StringComparison.OrdinalIgnoreCase) && r.Value > 0)
                {
                    fans.Add(new FanSensorData { Name = r.Label, Rpm = $"{r.Value:F0} RPM" });
                }
            }
            return fans;
        }

        private static (HwInfoReading[]?, HwInfoSensor[]?) ReadAll()
        {
            try
            {
                using var mmf = MemoryMappedFile.OpenExisting(SharedMemoryName, MemoryMappedFileRights.Read);
                using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

                var header = new HwInfoHeader();
                accessor.Read(0, out header);

                if (header.Signature != 0x52494857) // "HWIR"
                    return (null, null);

                long offset = Marshal.SizeOf<HwInfoHeader>();

                var sensors = new HwInfoSensor[header.NumSensorElements];
                for (int i = 0; i < header.NumSensorElements; i++)
                {
                    accessor.Read(offset, out sensors[i]);
                    offset += Marshal.SizeOf<HwInfoSensor>();
                }

                var readings = new HwInfoReading[header.NumReadingElements];
                for (int i = 0; i < header.NumReadingElements; i++)
                {
                    accessor.Read(offset, out readings[i]);
                    offset += Marshal.SizeOf<HwInfoReading>();
                }

                return (readings, sensors);
            }
            catch
            {
                return (null, null);
            }
        }
    }
}
