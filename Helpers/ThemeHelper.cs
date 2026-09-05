using System;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace VTStudioToolBox.Helpers;

public static class ThemeHelper
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VTStudioToolBox", "theme.json");

    public static ElementTheme CurrentTheme { get; private set; } = ElementTheme.Dark;
    public static bool IsFollowingSystem { get; private set; }

    public static event Action? ThemeChanged;

    private static ResourceDictionary _darkThemeDict = null!;
    private static ResourceDictionary _lightThemeDict = null!;
    private static UISettings? _uiSettings;

    public static void Initialize()
    {
        string saved = "";
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var data = JsonSerializer.Deserialize<ThemeData>(json);
                saved = data?.Theme ?? "";
            }
        }
        catch (Exception ex) { Logger.Warn("ThemeHelper", $"Initialize failed: {ex.Message}"); }

        if (string.Equals(saved, "System", StringComparison.OrdinalIgnoreCase))
        {
            IsFollowingSystem = true;
            CurrentTheme = GetSystemTheme();
            StartSystemThemeListener();
        }
        else if (!string.IsNullOrEmpty(saved) && Enum.TryParse<ElementTheme>(saved, out var theme) && theme != default)
        {
            CurrentTheme = theme;
        }
        else
        {
            CurrentTheme = ElementTheme.Dark;
        }

        RefreshThemeDict();
    }

    private static ElementTheme GetSystemTheme()
    {
        try
        {
            var uiSettings = new UISettings();
            var bg = uiSettings.GetColorValue(UIColorType.Background);
            return bg.R == 0 && bg.G == 0 && bg.B == 0 ? ElementTheme.Dark : ElementTheme.Light;
        }
        catch (Exception ex) { Logger.Warn("ThemeHelper", $"GetSystemTheme failed: {ex.Message}"); return ElementTheme.Dark; }
    }

    private static void StartSystemThemeListener()
    {
        try
        {
            _uiSettings ??= new UISettings();
            _uiSettings.ColorValuesChanged += (_, _) =>
            {
                if (!IsFollowingSystem) return;
                var newTheme = GetSystemTheme();
                if (CurrentTheme == newTheme) return;

                CurrentTheme = newTheme;
                RefreshThemeDict();

                // Must dispatch to UI thread
                var dispatcher = WindowHelper.GetWindow()?.DispatcherQueue;
                if (dispatcher is not null)
                {
                    dispatcher.TryEnqueue(() =>
                    {
                        var window = WindowHelper.GetWindow();
                        if (window?.Content is FrameworkElement root)
                            root.RequestedTheme = CurrentTheme;

                        ThemeChanged?.Invoke();
                    });
                }
            };
        }
        catch (Exception ex) { Logger.Warn("ThemeHelper", $"StartSystemThemeListener failed: {ex.Message}"); }
    }

    private static void StopSystemThemeListener()
    {
        _uiSettings = null;
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
        catch (Exception ex) { Logger.Warn("ThemeHelper", $"RefreshThemeDict failed: {ex.Message}"); }
    }

    private static ResourceDictionary GetCurrentThemeDict()
    {
        // Use CurrentTheme directly — it is always set before ThemeChanged fires
        return CurrentTheme == ElementTheme.Light ? _lightThemeDict : _darkThemeDict;
    }

    public static void ApplyTheme(ElementTheme theme)
    {
        IsFollowingSystem = false;
        StopSystemThemeListener();

        if (CurrentTheme == theme && !IsFollowingSystem) return;

        CurrentTheme = theme;
        RefreshThemeDict();

        var window = WindowHelper.GetWindow();
        if (window?.Content is FrameworkElement rootElement)
            rootElement.RequestedTheme = theme;

        Save(theme.ToString());
        ThemeChanged?.Invoke();
    }

    public static void ApplyFollowSystem()
    {
        IsFollowingSystem = true;
        var systemTheme = GetSystemTheme();
        CurrentTheme = systemTheme;
        RefreshThemeDict();

        var window = WindowHelper.GetWindow();
        if (window?.Content is FrameworkElement rootElement)
            rootElement.RequestedTheme = systemTheme;

        StartSystemThemeListener();
        Save("System");
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
        catch (Exception ex) { Logger.Warn("ThemeHelper", $"GetColor theme dict failed: {ex.Message}"); }

        // Fallback: try Application.Current.Resources
        try
        {
            if (Application.Current.Resources.ContainsKey(key))
            {
                if (Application.Current.Resources[key] is SolidColorBrush brush)
                    return brush.Color;
            }
        }
        catch (Exception ex) { Logger.Warn("ThemeHelper", $"GetColor app resources failed: {ex.Message}"); }

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

    private static void Save(string theme)
    {
        try
        {
            string dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var data = new ThemeData { Theme = theme };
            string json = JsonSerializer.Serialize(data);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex) { Logger.Warn("ThemeHelper", $"Save failed: {ex.Message}"); }
    }

    private class ThemeData
    {
        public string? Theme { get; set; }
    }
}
