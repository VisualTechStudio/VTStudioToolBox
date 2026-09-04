using System;
using System.IO;
using System.Text.Json;

namespace VTStudioToolBox.Helpers;

/// <summary>
/// Loads OAuth/API secrets from secrets.json (local dev) or environment variables (CI).
/// Priority: secrets.json > environment variables.
/// </summary>
internal static class SecretsConfig
{
    // Environment variable names used in CI pipelines
    private const string Env_GitHubClientId     = "VTSTUDIO_GITHUB_CLIENT_ID";
    private const string Env_GitHubClientSecret = "VTSTUDIO_GITHUB_CLIENT_SECRET";
    private const string Env_MicrosoftClientId  = "VTSTUDIO_MICROSOFT_CLIENT_ID";
    private const string Env_GoogleClientId     = "VTSTUDIO_GOOGLE_CLIENT_ID";
    private const string Env_SteamApiKey        = "VTSTUDIO_STEAM_API_KEY";

    private static readonly string ConfigPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secrets.json");

    private static SecretsData? _data;

    public static string GitHubClientId     => Data.GitHubClientId;
    public static string GitHubClientSecret => Data.GitHubClientSecret;
    public static string MicrosoftClientId  => Data.MicrosoftClientId;
    public static string GoogleClientId     => Data.GoogleClientId;
    public static string SteamApiKey        => Data.SteamApiKey;

    private static SecretsData Data
    {
        get
        {
            if (_data != null) return _data;

            // 1. Try secrets.json
            _data = TryLoadFromFile();

            // 2. Fall back to environment variables
            if (_data == null)
                _data = TryLoadFromEnvironment();

            if (_data == null)
                throw new InvalidOperationException(
                    "No secrets found. Provide secrets.json or set VTSTUDIO_* environment variables. " +
                    "See secrets.example.json for the required keys.");

            return _data;
        }
    }

    private static SecretsData? TryLoadFromFile()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return null;

            string json = File.ReadAllText(ConfigPath);
            var data = JsonSerializer.Deserialize<SecretsData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data != null && !string.IsNullOrEmpty(data.GitHubClientId))
            {
                Logger.Info("Secrets", "Loaded secrets from secrets.json");
                return data;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Secrets", $"Failed to load secrets.json: {ex.Message}");
        }
        return null;
    }

    private static SecretsData? TryLoadFromEnvironment()
    {
        string ghId  = Environment.GetEnvironmentVariable(Env_GitHubClientId) ?? "";
        string ghSec = Environment.GetEnvironmentVariable(Env_GitHubClientSecret) ?? "";
        string msId  = Environment.GetEnvironmentVariable(Env_MicrosoftClientId) ?? "";
        string gId   = Environment.GetEnvironmentVariable(Env_GoogleClientId) ?? "";
        string steam = Environment.GetEnvironmentVariable(Env_SteamApiKey) ?? "";

        // At minimum, GitHub client id must be present
        if (string.IsNullOrEmpty(ghId)) return null;

        Logger.Info("Secrets", "Loaded secrets from environment variables");
        return new SecretsData
        {
            GitHubClientId     = ghId,
            GitHubClientSecret = ghSec,
            MicrosoftClientId  = msId,
            GoogleClientId     = gId,
            SteamApiKey        = steam
        };
    }

    private sealed class SecretsData
    {
        public string GitHubClientId { get; set; } = "";
        public string GitHubClientSecret { get; set; } = "";
        public string MicrosoftClientId { get; set; } = "";
        public string GoogleClientId { get; set; } = "";
        public string SteamApiKey { get; set; } = "";
    }
}
