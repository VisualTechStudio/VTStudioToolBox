using System;
using System.IO;

namespace VTStudioToolBox.Helpers;

internal static class SetupWizardHelper
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VTStudioToolBox", "setup_done.txt");

    public static bool HasCompleted()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            return File.ReadAllText(FilePath).Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static void MarkCompleted()
    {
        try
        {
            string dir = Path.GetDirectoryName(FilePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, "true");
            Logger.Info("SetupWizard", "Setup wizard completed");
        }
        catch (Exception ex)
        {
            Logger.Error("SetupWizard", "Failed to save setup status", ex);
        }
    }
}
