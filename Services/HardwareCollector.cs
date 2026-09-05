using System;
using System.IO;
using System.Management;
using System.Text.Json;
using VTStudioToolBox.Auth;
using VTStudioToolBox.Helpers;
using VTStudioToolBox.Models;

namespace VTStudioToolBox.Services;

public sealed class HardwareCollector : IHardwareCollector
{
    private const string DeviceIdFile = "device_guid.json";
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private static string DeviceIdPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", DeviceIdFile);

    public HardwareInfo Collect()
    {
        return new HardwareInfo
        {
            Cpu = QueryCpu(),
            Gpu = QueryGpu(),
            RamGb = QueryRamGb(),
            OsVersion = QueryOsVersion()
        };
    }

    public string GetOrCreateDeviceGuid()
    {
        try
        {
            if (File.Exists(DeviceIdPath))
            {
                string json = File.ReadAllText(DeviceIdPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("deviceGuid", out var prop))
                {
                    string existing = prop.GetString() ?? "";
                    if (Guid.TryParse(existing, out _)) return existing;
                }
            }
        }
        catch (Exception ex) { Logger.Warn("HardwareCollector", $"Failed to read DeviceGuid: {ex.Message}"); }

        string newGuid = Guid.NewGuid().ToString();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DeviceIdPath)!);
            File.WriteAllText(DeviceIdPath, JsonSerializer.Serialize(new { deviceGuid = newGuid }, JsonOpts));
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareCollector", $"Failed to persist DeviceGuid: {ex.Message}");
        }
        return newGuid;
    }

    // ── WMI Queries (privacy-safe: no disk serial, MAC, hostname, username) ──

    private static string QueryCpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
                return obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareCollector", $"CPU query failed: {ex.Message}");
        }
        return "Unknown CPU";
    }

    private static string QueryGpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
                return obj["Name"]?.ToString()?.Trim() ?? "Unknown GPU";
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareCollector", $"GPU query failed: {ex.Message}");
        }
        return "Unknown GPU";
    }

    private static double QueryRamGb()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Capacity FROM Win32_PhysicalMemory");
            long totalBytes = 0;
            foreach (ManagementObject obj in searcher.Get())
                totalBytes += Convert.ToInt64(obj["Capacity"] ?? 0);
            return Math.Round(totalBytes / 1073741824.0, 1);
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareCollector", $"RAM query failed: {ex.Message}");
        }
        return 0;
    }

    private static string QueryOsVersion()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Caption, BuildNumber FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                string caption = obj["Caption"]?.ToString() ?? "";
                string build = obj["BuildNumber"]?.ToString() ?? "";
                return $"{caption} Build {build}".Trim();
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareCollector", $"OS query failed: {ex.Message}");
        }
        return "Unknown OS";
    }
}
