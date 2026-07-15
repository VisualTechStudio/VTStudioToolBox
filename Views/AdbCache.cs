using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VTStudioToolBox.Views
{
    public static class AdbCache
    {
        private static readonly string AdbPath = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
        private static readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromMilliseconds(500) };

        public static bool IsReady { get; private set; }
        public static string Status { get; private set; } = "";
        public static string PidText { get; private set; } = "";
        public static string Version { get; private set; } = "";
        public static List<DeviceInfo> Devices { get; private set; } = new();
        public static Dictionary<string, (string manufacturer, string model, string conn)> DeviceDetails { get; private set; } = new();
        private static readonly Dictionary<string, string> DeviceStates = new();

        public static event Action? Updated;

        private static string _lastHash = "";

        static AdbCache()
        {
            Timer.Tick += async (s, e) => await Poll();
        }

        public static void Start()
        {
            if (!File.Exists(AdbPath))
            {
                Status = "ADB not found";
                return;
            }
            _ = InitVersion();
            Timer.Start();
        }

        public static void Stop() => Timer.Stop();

        private static async Task InitVersion()
        {
            var (exit, output) = await Run("version");
            if (exit == 0 && output.Contains("Android Debug Bridge"))
            {
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("Android Debug Bridge"))
                    {
                        Version = line.Trim();
                        break;
                    }
                }
            }
        }

        private static async Task Poll()
        {
            if (!File.Exists(AdbPath)) return;

            int pid = FindPid();
            if (pid > 0)
            {
                Status = "Ready";
                PidText = $"PID: {pid}";
            }
            else
            {
                if (Status != "Not Running")
                {
                    Status = "Not Running";
                    PidText = "";
                    Devices.Clear();
                    DeviceDetails.Clear();
                    _lastHash = "";
                    Updated?.Invoke();
                }
                return;
            }

            var (exit, output) = await Run("devices -l");
            if (exit != 0) return;

            var devices = ParseDevices(output);
            string hash = string.Join("|", devices.Select(d => d.Serial + ":" + d.State));

            if (hash == _lastHash) return;
            _lastHash = hash;

            // Fetch details for new devices or state-changed devices
            foreach (var info in devices)
            {
                bool needsFetch = !DeviceDetails.ContainsKey(info.Serial)
                               || DeviceStates.TryGetValue(info.Serial, out var oldState) && oldState != info.State;

                if (needsFetch)
                {
                    var t1 = Run($"-s {info.Serial} shell getprop ro.product.manufacturer");
                    var t2 = Run($"-s {info.Serial} shell getprop ro.product.model");
                    await Task.WhenAll(t1, t2);

                    string mfr = t1.Result.exitCode == 0 ? t1.Result.output.Trim() : "";
                    string mdl = t2.Result.exitCode == 0 ? t2.Result.output.Trim() : "";
                    if (string.IsNullOrEmpty(mdl)) mdl = info.Product ?? info.Device ?? info.Serial;
                    string conn = !string.IsNullOrEmpty(info.Connection) ? $"USB ({info.Connection})"
                                : info.Serial.Contains(":") ? "Wireless" : "USB";

                    DeviceDetails[info.Serial] = (mfr, mdl, conn);
                }

                DeviceStates[info.Serial] = info.State;
            }

            // Cleanup stale
            var active = new HashSet<string>(devices.Select(d => d.Serial));
            foreach (var key in DeviceDetails.Keys.Where(k => !active.Contains(k)).ToList())
                DeviceDetails.Remove(key);
            foreach (var key in DeviceStates.Keys.Where(k => !active.Contains(k)).ToList())
                DeviceStates.Remove(key);

            Devices = devices;
            IsReady = true;
            Updated?.Invoke();
        }

        private static List<DeviceInfo> ParseDevices(string output)
        {
            var list = new List<DeviceInfo>();
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("List of") || trimmed.StartsWith("*"))
                    continue;

                var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                string serial = parts[0];
                string state = parts[1];
                if (state != "device" && state != "unauthorized" && state != "offline")
                    continue;

                var info = new DeviceInfo { Serial = serial, State = state };

                for (int i = 2; i < parts.Length; i++)
                {
                    var ci = parts[i].IndexOf(':');
                    if (ci > 0 && ci < parts[i].Length - 1)
                    {
                        string key = parts[i][..ci];
                        string val = parts[i][(ci + 1)..];
                        switch (key)
                        {
                            case "usb": info.Connection = val; break;
                            case "product": info.Product = val; break;
                            case "model": info.Model = val; break;
                            case "device": info.Device = val; break;
                        }
                    }
                }

                list.Add(info);
            }
            return list;
        }

        private static int FindPid()
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName("adb"))
                    return proc.Id;
            }
            catch { }
            return -1;
        }

        private static async Task<(int exitCode, string output)> Run(string args)
        {
            try
            {
                using var p = new Process();
                p.StartInfo = new ProcessStartInfo
                {
                    FileName = AdbPath, Arguments = args,
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                p.Start();
                var stdout = await p.StandardOutput.ReadToEndAsync();
                var stderr = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();
                return (p.ExitCode, string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}");
            }
            catch { return (-1, ""); }
        }
    }

    public class DeviceInfo
    {
        public string Serial { get; set; } = "";
        public string State { get; set; } = "";
        public string Connection { get; set; } = "";
        public string Product { get; set; } = "";
        public string Model { get; set; } = "";
        public string Device { get; set; } = "";
    }
}
