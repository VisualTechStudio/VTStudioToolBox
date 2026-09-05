using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VTStudioToolBox.Helpers;

namespace VTStudioToolBox.Services
{
    public static class AsusFanReader
    {
        private const string DevicePath = @"\\.\ATKACPI";
        private const uint IoControlCode = 0x0022240C;
        private const uint DSTS = 0x53545344;

        private const uint CPU_Fan = 0x00110013;
        private const uint GPU_Fan = 0x00110014;
        private const uint Mid_Fan = 0x00110031;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice, uint dwIoControlCode,
            byte[] lpInBuffer, uint nInBufferSize,
            byte[] lpOutBuffer, uint nOutBufferSize,
            ref uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        private static IntPtr _handle = new IntPtr(-1);
        private static bool _connected;
        private static bool _tried;
        private static readonly object _lock = new();

        public static bool IsAvailable()
        {
            lock (_lock)
            {
                if (_tried) return _connected;
                _tried = true;
                try
                {
                    _handle = CreateFile(DevicePath, GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    _connected = _handle != new IntPtr(-1);
                }
                catch (Exception ex) { _connected = false; Logger.Warn("AsusFanReader", $"IsAvailable failed: {ex.Message}"); }
                return _connected;
            }
        }

        public static List<FanSensorData> GetFanData()
        {
            var fans = new List<FanSensorData>();
            if (!IsAvailable()) return fans;

            int cpuRpm = ReadFan(CPU_Fan);
            if (cpuRpm > 0) fans.Add(new FanSensorData { Name = "CPU Fan", Rpm = $"{cpuRpm} RPM" });

            int gpuRpm = ReadFan(GPU_Fan);
            if (gpuRpm > 0) fans.Add(new FanSensorData { Name = "GPU Fan", Rpm = $"{gpuRpm} RPM" });

            int midRpm = ReadFan(Mid_Fan);
            if (midRpm > 0) fans.Add(new FanSensorData { Name = "Mid Fan", Rpm = $"{midRpm} RPM" });

            return fans;
        }

        private static int ReadFan(uint deviceId)
        {
            try
            {
                byte[] args = new byte[8];
                BitConverter.GetBytes(deviceId).CopyTo(args, 0);
                byte[] result = CallMethod(DSTS, args);
                if (result == null) return -1;
                int raw = BitConverter.ToInt32(result, 0) - 65536;
                int fan = raw & 0xFFFF;
                if (fan > 120 || (fan == 0 && raw < 0)) return -1;
                return fan * 100;
            }
            catch (Exception ex) { Logger.Warn("AsusFanReader", $"ReadFan failed: {ex.Message}"); return -1; }
        }

        private static byte[]? CallMethod(uint methodId, byte[] args)
        {
            if (!_connected) return null;
            try
            {
                byte[] acpiBuf = new byte[8 + args.Length];
                BitConverter.GetBytes(methodId).CopyTo(acpiBuf, 0);
                BitConverter.GetBytes((uint)args.Length).CopyTo(acpiBuf, 4);
                Array.Copy(args, 0, acpiBuf, 8, args.Length);

                byte[] outBuffer = new byte[16];
                uint bytesReturned = 0;
                bool ok = DeviceIoControl(_handle, IoControlCode, acpiBuf, (uint)acpiBuf.Length,
                    outBuffer, (uint)outBuffer.Length, ref bytesReturned, IntPtr.Zero);
                return ok ? outBuffer : null;
            }
            catch (Exception ex) { Logger.Warn("AsusFanReader", $"CallMethod failed: {ex.Message}"); return null; }
        }

        public static void Dispose()
        {
            if (_connected && _handle != new IntPtr(-1))
            {
                CloseHandle(_handle);
                _handle = new IntPtr(-1);
                _connected = false;
            }
        }
    }
}
