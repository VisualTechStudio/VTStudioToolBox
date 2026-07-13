using System;
using System.IO;
using System.Management;
using System.Threading;

namespace VTStudioToolBox.Helpers;

internal enum LogLevel
{
    Dev = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

internal static class Logger
{
    private static readonly string LogsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    private static string _currentLogPath = "";
    private static readonly ReaderWriterLockSlim _lock = new();
    private static DateTime _lastRotation = DateTime.MinValue;

    private static bool _enabled = true;
    private static LogLevel _minLevel = LogLevel.Dev;

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            SaveSettings();
        }
    }

    public static LogLevel MinLevel
    {
        get => _minLevel;
        set
        {
            _minLevel = value;
            SaveSettings();
        }
    }

    public static void Init()
    {
        Directory.CreateDirectory(LogsDir);
        LoadSettings();

#if !RELEASE
        _enabled = true;
        _minLevel = LogLevel.Dev;
#endif

        RotateIfNeeded();
        Info("App", $"VTStudioToolBox started (v{Cfg.AppVersion})");
        Info("App", $"Logging: enabled={_enabled}, level={_minLevel}");
        LogSystemInfo();
    }

    private static void LogSystemInfo()
    {
        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Caption, OSArchitecture, BuildNumber FROM Win32_OperatingSystem");
            foreach (ManagementObject os in cs.Get())
                Dev("System", $"OS: {os["Caption"]} {os["OSArchitecture"]} Build {os["BuildNumber"]}");
        }
        catch { }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject cpu in cs.Get())
                Dev("System", $"CPU: {cpu["Name"]} ({cpu["NumberOfCores"]}C/{cpu["NumberOfLogicalProcessors"]}T {cpu["MaxClockSpeed"]}MHz)");
        }
        catch { }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Capacity, Speed, Manufacturer FROM Win32_PhysicalMemory");
            foreach (ManagementObject ram in cs.Get())
            {
                double gb = Convert.ToInt64(ram["Capacity"] ?? 0) / 1073741824.0;
                Dev("System", $"RAM: {ram["Manufacturer"]} {gb:F0}GB {ram["Speed"]}MHz");
            }
        }
        catch { }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            foreach (ManagementObject gpu in cs.Get())
            {
                double vram = Convert.ToInt64(gpu["AdapterRAM"] ?? 0) / 1073741824.0;
                Dev("System", $"GPU: {gpu["Name"]} ({vram:F0}GB)");
            }
        }
        catch { }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Model, Size FROM Win32_DiskDrive");
            foreach (ManagementObject disk in cs.Get())
            {
                double size = Convert.ToInt64(disk["Size"] ?? 0) / 1073741824.0;
                Dev("System", $"Disk: {disk["Model"]} ({size:F0}GB)");
            }
        }
        catch { }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
            foreach (ManagementObject mb in cs.Get())
                Dev("System", $"Motherboard: {mb["Manufacturer"]} {mb["Product"]}");
        }
        catch { }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Name FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True");
            foreach (ManagementObject nic in cs.Get())
                Dev("System", $"NIC: {nic["Name"]}");
        }
        catch { }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Name FROM Win32_SoundDevice");
            foreach (ManagementObject audio in cs.Get())
                Dev("System", $"Audio: {audio["Name"]}");
        }
        catch { }
    }

    public static void Dev(string source, string message) => Write(LogLevel.Dev, "DEV", source, message);
    public static void Info(string source, string message) => Write(LogLevel.Info, "INFO", source, message);
    public static void Warn(string source, string message) => Write(LogLevel.Warn, "WARN", source, message);
    public static void Error(string source, string message, Exception? ex = null)
    {
        string text = ex != null ? $"{message}: {ex.GetType().Name} - {ex.Message}" : message;
        Write(LogLevel.Error, "ERROR", source, text);
    }

    private static void Write(LogLevel level, string levelTag, string source, string message)
    {
        if (!_enabled || level < _minLevel) return;

        RotateIfNeeded();
        _lock.EnterWriteLock();
        try
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{levelTag}] [{source}] {message}";
            File.AppendAllText(_currentLogPath, line + Environment.NewLine);
        }
        catch { }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private static string SettingsFilePath => Path.Combine(LogsDir, "logger_settings.json");

    private static void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                // Simple JSON parse: {"enabled":true,"level":0}
                if (json.Contains("\"enabled\""))
                {
                    _enabled = json.Contains("\"enabled\":true") || json.Contains("\"enabled\": true");
                }
                if (json.Contains("\"level\""))
                {
                    int start = json.IndexOf("\"level\"") + 7;
                    int end = json.IndexOf('}', start);
                    string levelStr = json[start..end].Trim().Trim(',', ' ');
                    if (int.TryParse(levelStr, out int level) && level >= 0 && level <= 3)
                        _minLevel = (LogLevel)level;
                }
            }
        }
        catch { }
    }

    private static void SaveSettings()
    {
        try
        {
            string json = $"{{\"enabled\":{_enabled.ToString().ToLower()},\"level\":{(int)_minLevel}}}";
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }

    private static void RotateIfNeeded()
    {
        _lock.EnterWriteLock();
        try
        {
            DateTime now = DateTime.Now;
            if (_currentLogPath == "" || now.Date > _lastRotation.Date)
            {
                string latestPath = Path.Combine(LogsDir, "latest.log");
                if (File.Exists(latestPath))
                {
                    string timestamp = _lastRotation == DateTime.MinValue
                        ? now.ToString("yyyyMMdd_HHmmss")
                        : _lastRotation.ToString("yyyyMMdd_HHmmss");
                    string archived = Path.Combine(LogsDir, $"{timestamp}.log");
                    try { File.Move(latestPath, archived); } catch { }
                }

                _currentLogPath = latestPath;
                _lastRotation = now;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
