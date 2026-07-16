using System;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace VTStudioToolBox.Helpers;

public static class ThemeHelper
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VTStudioToolBox", "theme.json");

    public static ElementTheme CurrentTheme { get; private set; } = ElementTheme.Dark;

    public static event Action? ThemeChanged;

    // References to both theme dictionaries for dynamic lookup
    private static ResourceDictionary _darkThemeDict = null!;
    private static ResourceDictionary _lightThemeDict = null!;

    public static void Initialize()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var data = JsonSerializer.Deserialize<ThemeData>(json);
                if (data != null && !string.IsNullOrEmpty(data.Theme))
                {
                    if (Enum.TryParse<ElementTheme>(data.Theme, out var theme))
                    {
                        CurrentTheme = theme;
                    }
                }
            }
        }
        catch { }

        if (CurrentTheme == default)
            CurrentTheme = ElementTheme.Dark;

        RefreshThemeDict();
    }

    private static void RefreshThemeDict()
    {
        try
        {
            var dicts = Application.Current.Resources.ThemeDictionaries;
            if (dicts != null)
            {
                if (dicts.ContainsKey("Dark"))
                    _darkThemeDict = (ResourceDictionary)dicts["Dark"];
                if (dicts.ContainsKey("Light"))
                    _lightThemeDict = (ResourceDictionary)dicts["Light"];
            }
        }
        catch { }
    }

    private static ResourceDictionary GetCurrentThemeDict()
    {
        // Use CurrentTheme directly — it is always set before ThemeChanged fires
        return CurrentTheme == ElementTheme.Light ? _lightThemeDict : _darkThemeDict;
    }

    public static void ApplyTheme(ElementTheme theme)
    {
        if (CurrentTheme == theme) return;

        CurrentTheme = theme;
        RefreshThemeDict();

        var window = WindowHelper.GetWindow();
        if (window?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme;
        }

        Save(theme);
        ThemeChanged?.Invoke();
    }

    /// <summary>
    /// Get a color from the current theme's resource dictionary.
    /// Returns raw color value so callers can create fresh brushes that reflect theme changes.
    /// </summary>
    public static Windows.UI.Color GetColor(string key)
    {
        var dict = GetCurrentThemeDict();
        try
        {
            if (dict != null && dict.ContainsKey(key))
            {
                if (dict[key] is SolidColorBrush brush)
                    return brush.Color;
            }
        }
        catch { }

        // Fallback: try Application.Current.Resources
        try
        {
            if (Application.Current.Resources.ContainsKey(key))
            {
                if (Application.Current.Resources[key] is SolidColorBrush brush)
                    return brush.Color;
            }
        }
        catch { }

        return Windows.UI.Color.FromArgb(255, 255, 255, 255);
    }

    /// <summary>
    /// Create a fresh brush from the current theme. Always returns a new instance
    /// so colors update correctly when the theme changes.
    /// </summary>
    public static Brush GetBrush(string key)
    {
        return new SolidColorBrush(GetColor(key));
    }

    private static void Save(ElementTheme theme)
    {
        try
        {
            string dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var data = new ThemeData { Theme = theme.ToString() };
            string json = JsonSerializer.Serialize(data);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    private class ThemeData
    {
        public string? Theme { get; set; }
    }
}
