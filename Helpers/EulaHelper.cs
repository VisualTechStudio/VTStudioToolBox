using System;
using System.IO;
using System.Threading.Tasks;

namespace VTStudioToolBox.Helpers;

internal static class EulaHelper
{
    private const string FolderName = "VTStudioToolBox";
    private const string FileName = "Eula.txt";
    private const string AgreedContent = "true";

    private static string GetEulaFilePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, FolderName, FileName);
    }

    public static bool HasUserAgreed()
    {
        try
        {
            string filePath = GetEulaFilePath();
            bool exists = File.Exists(filePath);
            Logger.Dev("EulaHelper", $"EULA file exists={exists}, path={filePath}");
            if (exists)
            {
                string content = File.ReadAllText(filePath);
                bool agreed = content.Trim().Equals(AgreedContent, StringComparison.OrdinalIgnoreCase);
                Logger.Dev("EulaHelper", $"EULA content='{content.Trim()}', agreed={agreed}");
                return agreed;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("EulaHelper", "Failed to check EULA status", ex);
        }
        return false;
    }

    public static async Task SetUserAgreedAsync()
    {
        try
        {
            string filePath = GetEulaFilePath();
            string directory = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(filePath, AgreedContent);
            Logger.Info("EulaHelper", "EULA agreement saved");
        }
        catch (Exception ex)
        {
            Logger.Error("EulaHelper", "Failed to save EULA agreement", ex);
        }
    }

    public static async Task RevokeAsync()
    {
        try
        {
            string filePath = GetEulaFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Logger.Info("EulaHelper", "EULA agreement revoked");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("EulaHelper", "Failed to revoke EULA", ex);
        }
    }
}
