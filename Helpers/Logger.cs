using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

    // 日志文件大小限制 (10MB)
    private const long MaxLogFileSizeBytes = 10 * 1024 * 1024;

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

        // 清理超过30天的旧日志
        CleanOldLogs(30);

        Info("App", $"VTStudioToolBox started (v{Cfg.AppVersion})");
        Info("App", $"Logging: enabled={_enabled}, level={_minLevel}");

        // 注册全局异常处理
        RegisterGlobalExceptionHandlers();

        LogSystemInfo();
    }

    /// <summary>
    /// 注册全局异常处理器，捕获未处理的异常并记录到日志
    /// </summary>
    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Error("CRASH", $"Unhandled exception (IsTerminating={e.IsTerminating})", ex);
            FlushBuffer();
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Error("CRASH", "Unobserved task exception", e.Exception);
            e.SetObserved();
            FlushBuffer();
        };
    }

    /// <summary>
    /// 清理超过指定天数的旧日志文件
    /// </summary>
    private static void CleanOldLogs(int maxDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-maxDays);
            foreach (var file in Directory.GetFiles(LogsDir, "*.log"))
            {
                if (Path.GetFileName(file) == "latest.log") continue;
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                    Dev("Logger", $"Cleaned old log: {Path.GetFileName(file)}");
                }
            }
        }
        catch { /* best-effort */ }
    }

    private static void LogSystemInfo()
    {
        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Caption, OSArchitecture, BuildNumber FROM Win32_OperatingSystem");
            foreach (ManagementObject os in cs.Get())
                Dev("System", $"OS: {os["Caption"]} {os["OSArchitecture"]} Build {os["BuildNumber"]}");
        }
        catch { /* System info logging is best-effort */ }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject cpu in cs.Get())
                Dev("System", $"CPU: {cpu["Name"]} ({cpu["NumberOfCores"]}C/{cpu["NumberOfLogicalProcessors"]}T {cpu["MaxClockSpeed"]}MHz)");
        }
        catch { /* System info logging is best-effort */ }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Capacity, Speed, Manufacturer FROM Win32_PhysicalMemory");
            foreach (ManagementObject ram in cs.Get())
            {
                double gb = Convert.ToInt64(ram["Capacity"] ?? 0) / 1073741824.0;
                Dev("System", $"RAM: {ram["Manufacturer"]} {gb:F0}GB {ram["Speed"]}MHz");
            }
        }
        catch { /* System info logging is best-effort */ }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            foreach (ManagementObject gpu in cs.Get())
            {
                double vram = Convert.ToInt64(gpu["AdapterRAM"] ?? 0) / 1073741824.0;
                Dev("System", $"GPU: {gpu["Name"]} ({vram:F0}GB)");
            }
        }
        catch { /* System info logging is best-effort */ }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Model, Size FROM Win32_DiskDrive");
            foreach (ManagementObject disk in cs.Get())
            {
                double size = Convert.ToInt64(disk["Size"] ?? 0) / 1073741824.0;
                Dev("System", $"Disk: {disk["Model"]} ({size:F0}GB)");
            }
        }
        catch { /* System info logging is best-effort */ }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
            foreach (ManagementObject mb in cs.Get())
                Dev("System", $"Motherboard: {mb["Manufacturer"]} {mb["Product"]}");
        }
        catch { /* System info logging is best-effort */ }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Name FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True");
            foreach (ManagementObject nic in cs.Get())
                Dev("System", $"NIC: {nic["Name"]}");
        }
        catch { /* System info logging is best-effort */ }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Name FROM Win32_SoundDevice");
            foreach (ManagementObject audio in cs.Get())
                Dev("System", $"Audio: {audio["Name"]}");
        }
        catch { /* System info logging is best-effort */ }

        // 磁盘空间
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    double totalGb = drive.TotalSize / 1073741824.0;
                    double freeGb = drive.AvailableFreeSpace / 1073741824.0;
                    Dev("System", $"Drive {drive.Name}: {totalGb:F0}GB total, {freeGb:F0}GB free");
                }
            }
        }
        catch { /* System info logging is best-effort */ }
    }

    // ────────────────────── Public API ──────────────────────

    public static void Dev(string source, string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => Write(LogLevel.Dev, "DEV", source, message, caller, file, line);

    public static void Info(string source, string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => Write(LogLevel.Info, "INFO", source, message, caller, file, line);

    public static void Warn(string source, string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => Write(LogLevel.Warn, "WARN", source, message, caller, file, line);

    public static void Error(string source, string message, Exception? ex = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        var sb = new StringBuilder();
        sb.Append(message);
        if (ex != null)
        {
            sb.Append($"\n  {ex.GetType().Name}: {ex.Message}");
            if (ex.StackTrace != null)
            {
                // 只保留前3行堆栈，避免日志过长
                var stackLines = ex.StackTrace.Split('\n');
                for (int i = 0; i < Math.Min(3, stackLines.Length); i++)
                    sb.Append($"\n    {stackLines[i].Trim()}");
            }
            if (ex.InnerException != null)
                sb.Append($"\n  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
        Write(LogLevel.Error, "ERROR", source, sb.ToString(), caller, file, line);
    }

    /// <summary>
    /// 记录页面导航
    /// </summary>
    public static void Navigation(string from, string to)
        => Info("Navigation", $"{from} → {to}");

    /// <summary>
    /// 记录用户操作
    /// </summary>
    public static void UserAction(string action, string? detail = null)
        => Info("UserAction", detail != null ? $"{action}: {detail}" : action);

    /// <summary>
    /// 记录性能计时 - 手动模式
    /// </summary>
    public static Stopwatch StartTimer(string operation)
    {
        var sw = Stopwatch.StartNew();
        Dev("Perf", $"Started: {operation}");
        return sw;
    }

    /// <summary>
    /// 记录性能计时 - 停止并记录耗时
    /// </summary>
    public static void StopTimer(Stopwatch sw, string operation)
    {
        sw.Stop();
        var ms = sw.ElapsedMilliseconds;
        if (ms > 1000)
            Warn("Perf", $"{operation} took {ms}ms ({ms / 1000.0:F1}s)");
        else
            Dev("Perf", $"{operation} took {ms}ms");
    }

    /// <summary>
    /// 记录性能计时 - 包装异步操作
    /// </summary>
    public static async Task<T> TimeAsync<T>(string operation, Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await action();
            sw.Stop();
            Dev("Perf", $"{operation} completed in {sw.ElapsedMilliseconds}ms");
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Error("Perf", $"{operation} failed after {sw.ElapsedMilliseconds}ms", ex);
            throw;
        }
    }

    /// <summary>
    /// 记录性能计时 - 包装同步操作
    /// </summary>
    public static T Time<T>(string operation, Func<T> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = action();
            sw.Stop();
            Dev("Perf", $"{operation} completed in {sw.ElapsedMilliseconds}ms");
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Error("Perf", $"{operation} failed after {sw.ElapsedMilliseconds}ms", ex);
            throw;
        }
    }

    // ────────────────────── Core Write ──────────────────────

    private static void Write(LogLevel level, string levelTag, string source, string message,
        string caller, string file, int line)
    {
        if (!_enabled || level < _minLevel) return;

        int threadId = Environment.CurrentManagedThreadId;
        string? threadName = Thread.CurrentThread.Name;
        string threadInfo = threadName != null ? $"{threadId}({threadName})" : threadId.ToString();

        // 格式: [时间] [级别] [线程] [来源] 消息 (调用者@文件:行号)
        // Dev 级别显示调用者信息，其他级别不显示以保持简洁
        string location = "";
        if (level == LogLevel.Dev && !string.IsNullOrEmpty(caller))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            location = $" ({caller}@{fileName}:{line})";
        }

        _lock.EnterWriteLock();
        try
        {
            RotateIfNeededInternal();
            string line2 = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{levelTag}] [T{threadInfo}] [{source}] {message}{location}";
            File.AppendAllText(_currentLogPath, line2 + Environment.NewLine);
        }
        catch { }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 刷新缓冲区（用于崩溃前确保日志写入）
    /// </summary>
    private static void FlushBuffer()
    {
        // File.AppendAllText already flushes, but ensure any buffered content is written
        try
        {
            _lock.EnterWriteLock();
            try
            {
                // Force a flush by reading and rewriting (no-op if already flushed)
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        catch { }
    }

    // ────────────────────── Settings ──────────────────────

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

    // ────────────────────── Log Rotation ──────────────────────

    private static void RotateIfNeeded()
    {
        _lock.EnterWriteLock();
        try
        {
            RotateIfNeededInternal();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private static void RotateIfNeededInternal()
    {
        DateTime now = DateTime.Now;
        bool needRotation = false;

        // 按日轮转
        if (_currentLogPath == "" || now.Date > _lastRotation.Date)
        {
            needRotation = true;
        }
        // 按大小轮转
        else if (_currentLogPath != "" && File.Exists(_currentLogPath))
        {
            try
            {
                var fileInfo = new FileInfo(_currentLogPath);
                if (fileInfo.Length > MaxLogFileSizeBytes)
                {
                    needRotation = true;
                    Dev("Logger", $"Log file exceeded {MaxLogFileSizeBytes / 1024 / 1024}MB, rotating");
                }
            }
            catch { /* best-effort */ }
        }

        if (needRotation)
        {
            string latestPath = Path.Combine(LogsDir, "latest.log");
            if (File.Exists(latestPath))
            {
                string timestamp = now.ToString("yyyyMMdd_HHmmss");
                string archived = Path.Combine(LogsDir, $"{timestamp}.log");
                try { File.Move(latestPath, archived); } catch { }
            }

            _currentLogPath = latestPath;
            _lastRotation = now;
        }
    }
}
