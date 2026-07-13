using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.UI.Xaml;

namespace VTStudioToolBox.Helpers;

public static class LanguageHelper
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VTStudioToolBox", "language.json");

    private static readonly string StringsDir = Path.Combine(
        AppContext.BaseDirectory, "Strings");

    public static string CurrentLanguage { get; private set; } = "zh-CN";

    private static Dictionary<string, Dictionary<string, string>> _resources = new();
    private static Dictionary<string, string> _languageNames = new();

    static LanguageHelper()
    {
        // Scan Strings directory for all *.json files
        if (Directory.Exists(StringsDir))
        {
            foreach (string file in Directory.GetFiles(StringsDir, "*.json"))
            {
                string culture = Path.GetFileNameWithoutExtension(file);
                LoadLanguage(culture);
            }
        }

        // Fallback if directory is empty or missing
        if (_resources.Count == 0)
        {
            LoadLanguage("zh-CN");
            LoadLanguage("en-US");
        }
    }

    private static void LoadLanguage(string culture)
    {
        try
        {
            string filePath = Path.Combine(StringsDir, $"{culture}.json");
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null)
                {
                    _resources[culture] = dict;
                    // Extract _name if present
                    if (dict.TryGetValue("_name", out string? name))
                    {
                        _languageNames[culture] = name;
                    }
                }
            }
        }
        catch
        {
            // Fallback - empty dictionary
            _resources.TryAdd(culture, new Dictionary<string, string>());
        }
    }

    public static string GetLanguageName(string culture)
    {
        if (_languageNames.TryGetValue(culture, out string? name))
            return name;
        return culture;
    }

    public static Dictionary<string, string> GetAvailableLanguages()
    {
        return new Dictionary<string, string>(_languageNames);
    }

    public static void Initialize()
    {
        try
        {
            Logger.Info("Language", $"Settings path: {SettingsPath}");
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                Logger.Info("Language", $"Read: {json}");
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data != null && data.TryGetValue("language", out string? saved) && !string.IsNullOrEmpty(saved))
                {
                    Logger.Info("Language", $"Applying: {saved}");
                    ApplyLanguage(saved);
                    return;
                }
            }
            else
            {
                Logger.Info("Language", "No settings file found, using default");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Language", "Initialize failed", ex);
        }
        ApplyLanguage("zh-CN");
    }

    public static void ApplyLanguage(string language)
    {
        CurrentLanguage = language;
        try
        {
            var culture = new CultureInfo(language);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
        catch
        {
            // Fallback
        }

        try
        {
            string dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var data = new Dictionary<string, string> { ["language"] = language };
            string json = JsonSerializer.Serialize(data);
            File.WriteAllText(SettingsPath, json);
            Logger.Info("Language", $"Saved: {json} to {SettingsPath}");
        }
        catch (Exception ex)
        {
            Logger.Error("Language", "Save failed", ex);
        }
    }

    public static void RestartApp()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? "";
            if (!string.IsNullOrEmpty(exePath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? ""
                };
                Process.Start(psi);
            }
        }
        catch
        {
            // Fallback
        }
        Application.Current.Exit();
    }

    public static string GetString(string key)
    {
        if (_resources.TryGetValue(CurrentLanguage, out var dict) && dict.TryGetValue(key, out var value))
            return value;

        // Fallback to zh-CN
        if (_resources.TryGetValue("zh-CN", out var fallback) && fallback.TryGetValue(key, out var fallbackValue))
            return fallbackValue;

        return key;
    }

    public static string GetString(string key, params object[] args)
    {
        string format = GetString(key);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }
}
