using System;
using System.IO;
using System.Text.Json;

namespace VTStudioToolBox.Helpers;

public static class DashboardSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VTStudioToolBox", "dashboard.json");

    public static int RefreshIntervalMs { get; private set; } = 2000;
    public static bool SuppressHighRefreshWarning { get; private set; }
    public static event Action<int>? RefreshIntervalChanged;

    public static void Initialize()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var data = JsonSerializer.Deserialize<DashboardData>(json);
                if (data?.RefreshIntervalMs is > 0)
                    RefreshIntervalMs = data.RefreshIntervalMs;
                if (data?.SuppressHighRefreshWarning == true)
                    SuppressHighRefreshWarning = true;
            }
        }
        catch { }
    }

    public static void SetRefreshInterval(int ms)
    {
        RefreshIntervalMs = ms;
        Save();
        RefreshIntervalChanged?.Invoke(ms);
    }

    public static void SetSuppressHighRefreshWarning(bool suppress)
    {
        SuppressHighRefreshWarning = suppress;
        Save();
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(new DashboardData { RefreshIntervalMs = RefreshIntervalMs });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    private class DashboardData
    {
        public int RefreshIntervalMs { get; set; } = 2000;
        public bool SuppressHighRefreshWarning { get; set; }
    }
}
