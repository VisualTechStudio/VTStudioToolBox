using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using VTStudioToolBox.Helpers;

namespace VTStudioToolBox.Services
{
    public static class PawnIOReader
    {
        private static bool? _isAvailable;
        private static IntPtr _handle = IntPtr.Zero;
        private static bool _loaded = false;
        private static byte[]? _moduleBlob;
        private static CpuVendor _cpuVendor = CpuVendor.Unknown;

        // Intel MSR addresses
        private const uint MSR_IA32_PERF_STATUS = 0x198;

        private enum CpuVendor
        {
            Unknown,
            Intel,
            AMD
        }

        public static bool IsAvailable()
        {
            if (_isAvailable.HasValue) return _isAvailable.Value;

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_SystemDriver WHERE Name = 'PawnIO'");
                _isAvailable = searcher.Get().Count > 0;
            }
            catch
            {
                _isAvailable = false;
            }

            return _isAvailable.Value;
        }

        public static double ReadCpuVoltage()
        {
            if (!IsAvailable())
            {
                Logger.Dev("PawnIOReader", "PawnIO driver not installed");
                return 0;
            }

            try
            {
                if (!Initialize())
                {
                    Logger.Warn("PawnIOReader", "Failed to initialize PawnIO");
                    return 0;
                }

                return _cpuVendor switch
                {
                    CpuVendor.Intel => ReadIntelVoltage(),
                    CpuVendor.AMD => ReadAmdVoltage(),
                    _ => 0
                };
            }
            catch (Exception ex)
            {
                Logger.Warn("PawnIOReader", $"Failed to read CPU voltage: {ex.Message}");
                return 0;
            }
        }

        private static double ReadIntelVoltage()
        {
            var result = ExecuteMsrRead(MSR_IA32_PERF_STATUS);
            if (result == 0) return 0;

            // Intel MSR_IA32_PERF_STATUS (0x198):
            // - Bits 15:0: Current performance state (frequency)
            // - Bits 47:32: Current voltage (Skylake+)
            // - Older CPUs may use different bits
            
            // Try Skylake+ encoding first (bits 47:32)
            var voltageRaw = (uint)((result >> 32) & 0xFFFF);
            if (voltageRaw > 0)
            {
                double voltage = voltageRaw / 8192.0;
                if (voltage >= 0.5 && voltage <= 2.0)
                {
                    Logger.Dev("PawnIOReader", $"Intel voltage (Skylake+): {voltage:F3}V (raw: 0x{voltageRaw:X4})");
                    return voltage;
                }
            }
            
            // Try older encoding (bits 15:0)
            voltageRaw = (uint)(result & 0xFFFF);
            if (voltageRaw > 0)
            {
                double voltage = voltageRaw / 8192.0;
                if (voltage >= 0.5 && voltage <= 2.0)
                {
                    Logger.Dev("PawnIOReader", $"Intel voltage (legacy): {voltage:F3}V (raw: 0x{voltageRaw:X4})");
                    return voltage;
                }
                
                voltage = voltageRaw / 16384.0;
                if (voltage >= 0.5 && voltage <= 2.0)
                {
                    Logger.Dev("PawnIOReader", $"Intel voltage (legacy alt): {voltage:F3}V (raw: 0x{voltageRaw:X4})");
                    return voltage;
                }
            }
            
            // Debug: log the raw MSR value
            Logger.Dev("PawnIOReader", $"Raw MSR_IA32_PERF_STATUS: 0x{result:X16}");
            Logger.Warn("PawnIOReader", $"Could not decode Intel voltage");
            return 0;
        }

        private static double ReadAmdVoltage()
        {
            // Step 1: Resolve PM table address
            var resolveResult = ExecuteSmuCommand("ioctl_resolve_pm_table", Array.Empty<ulong>(), 2);
            if (resolveResult == null || resolveResult.Length < 2)
            {
                Logger.Warn("PawnIOReader", "Failed to resolve PM table");
                return 0;
            }

            // Step 2: Update PM table
            ExecuteSmuCommand("ioctl_update_pm_table", Array.Empty<ulong>(), 0);

            // Step 3: Read PM table
            var pmTableResult = ExecuteSmuCommand("ioctl_read_pm_table", Array.Empty<ulong>(), 256);
            if (pmTableResult != null && pmTableResult.Length > 0)
            {
                // Common voltage offsets for Zen4 (Ryzen 9 9955HX)
                int[] voltageOffsets = { 13, 14, 15, 16, 17, 18, 19, 20 };
                
                foreach (var offset in voltageOffsets)
                {
                    if (offset >= pmTableResult.Length) continue;
                    
                    var rawVal = pmTableResult[offset];
                    if (rawVal == 0) continue;
                    
                    // Try as float32 in lower 32 bits
                    var floatVal = BitConverter.ToSingle(BitConverter.GetBytes((uint)(rawVal & 0xFFFFFFFF)), 0);
                    if (floatVal >= 0.5 && floatVal <= 2.0)
                    {
                        Logger.Dev("PawnIOReader", $"AMD voltage from PM[{offset}] (float): {floatVal:F3}V");
                        return floatVal;
                    }
                    
                    // Try millivolt encoding (raw / 1000.0)
                    double voltage = rawVal / 1000.0;
                    if (voltage >= 0.5 && voltage <= 2.0)
                    {
                        Logger.Dev("PawnIOReader", $"AMD voltage from PM[{offset}]: {voltage:F3}V");
                        return voltage;
                    }
                }
            }

            // Step 4: Try direct SMU command
            var smuArgs = new ulong[7];
            smuArgs[0] = 0x00000002; // SMU_MSG_GetVoltage
            
            var smuResult = ExecuteSmuCommand("ioctl_send_smu_command", smuArgs, 6);
            if (smuResult != null && smuResult.Length > 0)
            {
                for (int i = 0; i < smuResult.Length; i++)
                {
                    var val = smuResult[i];
                    if (val == 0) continue;
                    
                    // Millivolt encoding
                    double voltage = val / 1000.0;
                    if (voltage >= 0.5 && voltage <= 2.0)
                    {
                        Logger.Dev("PawnIOReader", $"AMD voltage via SMU: {voltage:F3}V");
                        return voltage;
                    }
                }
            }

            Logger.Warn("PawnIOReader", "Could not decode AMD voltage");
            return 0;
        }

        private static ulong ExecuteMsrRead(uint msr)
        {
            if (_handle == IntPtr.Zero) return 0;

            try
            {
                var inBuf = new ulong[] { msr };
                var outBuf = new ulong[1];
                
                var hr = PawnIOLib.pawnio_execute(_handle, "ioctl_read_msr", 
                    inBuf, new IntPtr(1), outBuf, new IntPtr(1), out var returnSize);
                
                if (hr != 0 || returnSize == IntPtr.Zero)
                {
                    Logger.Warn("PawnIOReader", $"Failed to read MSR 0x{msr:X4}: 0x{hr:X8}");
                    return 0;
                }

                return outBuf[0];
            }
            catch (Exception ex)
            {
                Logger.Warn("PawnIOReader", $"Exception reading MSR 0x{msr:X4}: {ex.Message}");
                return 0;
            }
        }

        private static ulong[]? ExecuteSmuCommand(string functionName, ulong[] inputArgs, int outputCount)
        {
            if (_handle == IntPtr.Zero) return null;

            try
            {
                var outBuf = new ulong[outputCount];
                int hr;
                IntPtr returnSize;
                
                if (inputArgs.Length > 0)
                {
                    Logger.Dev("PawnIOReader", $"Executing {functionName} with {inputArgs.Length} args, expecting {outputCount} outputs");
                    hr = PawnIOLib.pawnio_execute(_handle, functionName, 
                        inputArgs, new IntPtr(inputArgs.Length), outBuf, new IntPtr(outputCount), out returnSize);
                }
                else
                {
                    Logger.Dev("PawnIOReader", $"Executing {functionName} with no args, expecting {outputCount} outputs");
                    hr = PawnIOLib.pawnio_execute_noinput(_handle, functionName, 
                        IntPtr.Zero, IntPtr.Zero, outBuf, new IntPtr(outputCount), out returnSize);
                }
                
                Logger.Dev("PawnIOReader", $"{functionName} result: hr=0x{hr:X8}, returnSize={returnSize.ToInt64()}");
                
                if (hr != 0)
                {
                    Logger.Warn("PawnIOReader", $"Failed to execute {functionName}: 0x{hr:X8}");
                    return null;
                }

                var resultSize = returnSize.ToInt32() / sizeof(ulong);
                if (resultSize == 0)
                {
                    Logger.Warn("PawnIOReader", $"{functionName} returned no data (returnSize=0)");
                    
                    // Try reading outBuf directly even if returnSize is 0
                    bool hasData = false;
                    for (int i = 0; i < outBuf.Length; i++)
                    {
                        if (outBuf[i] != 0)
                        {
                            hasData = true;
                            Logger.Dev("PawnIOReader", $"outBuf[{i}] = 0x{outBuf[i]:X16}");
                        }
                    }
                    
                    if (hasData)
                    {
                        Logger.Dev("PawnIOReader", $"Found data in outBuf despite returnSize=0");
                        return outBuf;
                    }
                    
                    return null;
                }

                Logger.Dev("PawnIOReader", $"{functionName} returned {resultSize} values");
                return outBuf[..resultSize];
            }
            catch (Exception ex)
            {
                Logger.Warn("PawnIOReader", $"Exception executing {functionName}: {ex.Message}");
                return null;
            }
        }

        private static bool Initialize()
        {
            if (_loaded) return _handle != IntPtr.Zero;

            try
            {
                _cpuVendor = DetectCpuVendor();
                Logger.Dev("PawnIOReader", $"Detected CPU vendor: {_cpuVendor}");
                
                var hr = PawnIOLib.pawnio_open(out _handle);
                if (hr != 0)
                {
                    Logger.Warn("PawnIOReader", $"Failed to open PawnIO device: 0x{hr:X8}");
                    return false;
                }
                Logger.Dev("PawnIOReader", $"PawnIO device opened, handle=0x{_handle:X}");

                var blob = LoadModuleBlob();
                if (blob == null)
                {
                    Logger.Warn("PawnIOReader", "PawnIO module not found");
                    PawnIOLib.pawnio_close(_handle);
                    _handle = IntPtr.Zero;
                    return false;
                }
                Logger.Dev("PawnIOReader", $"Module blob loaded, size={blob.Length} bytes");

                hr = PawnIOLib.pawnio_load(_handle, blob, new IntPtr(blob.Length));
                if (hr != 0)
                {
                    Logger.Warn("PawnIOReader", $"Failed to load PawnIO module: 0x{hr:X8}");
                    PawnIOLib.pawnio_close(_handle);
                    _handle = IntPtr.Zero;
                    return false;
                }
                Logger.Dev("PawnIOReader", "PawnIO module loaded successfully");

                _loaded = true;
                Logger.Info("PawnIOReader", $"PawnIO initialized with {_cpuVendor} module");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn("PawnIOReader", $"Failed to initialize PawnIO: {ex.Message}");
                return false;
            }
        }

        private static byte[]? LoadModuleBlob()
        {
            if (_moduleBlob != null) return _moduleBlob;

            var baseName = _cpuVendor == CpuVendor.AMD ? "RyzenSMU" : "IntelMSR";
            
            try
            {
                var assembly = typeof(PawnIOReader).Assembly;
                // Try .amx first, then .bin
                foreach (var ext in new[] { "amx", "bin" })
                {
                    var resourceName = $"VTStudioToolBox.Assets.{baseName}.{ext}";
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        _moduleBlob = ms.ToArray();
                        Logger.Dev("PawnIOReader", $"Loaded {baseName}.{ext} from embedded resource");
                        return _moduleBlob;
                    }
                }

                var searchPaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "Assets"),
                    Path.Combine(AppContext.BaseDirectory, "Tools", "PawnIO"),
                    Path.Combine(AppContext.BaseDirectory, "PawnIO"),
                };

                foreach (var dir in searchPaths)
                {
                    foreach (var ext in new[] { "amx", "bin" })
                    {
                        var path = Path.Combine(dir, $"{baseName}.{ext}");
                        if (File.Exists(path))
                        {
                            _moduleBlob = File.ReadAllBytes(path);
                            Logger.Dev("PawnIOReader", $"Loaded {baseName}.{ext} from {path}");
                            return _moduleBlob;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("PawnIOReader", $"Failed to load {baseName} module: {ex.Message}");
            }

            Logger.Warn("PawnIOReader", $"{baseName} module not found (.amx or .bin)");
            return null;
        }

        private static CpuVendor DetectCpuVendor()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var manufacturer = obj["Manufacturer"]?.ToString()?.ToUpperInvariant() ?? "";
                    if (manufacturer.Contains("INTEL")) return CpuVendor.Intel;
                    if (manufacturer.Contains("AMD")) return CpuVendor.AMD;
                }
            }
            catch { }

            return CpuVendor.Unknown;
        }

        public static void Cleanup()
        {
            if (_handle != IntPtr.Zero)
            {
                PawnIOLib.pawnio_close(_handle);
                _handle = IntPtr.Zero;
            }
            _loaded = false;
        }
    }
}
