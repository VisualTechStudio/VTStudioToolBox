using System;
using System.Diagnostics;
using System.IO;

namespace VTStudioToolBox.Helpers;

internal static class FirewallHelper
{
    private const string RuleName = "VTStudioToolBox_STUN";

    public static bool EnsureFirewallRule()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exePath)) return false;

            Logger.Dev("Firewall", $"Checking rule for: {exePath}");

            // Check if rule already exists
            using var checkProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall show rule name=\"{RuleName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (checkProcess != null)
            {
                string output = checkProcess.StandardOutput.ReadToEnd();
                checkProcess.WaitForExit();

                if (output.Contains(RuleName))
                {
                    Logger.Info("Firewall", "Firewall rule already exists");
                    return true;
                }
            }

            // Add inbound rule
            using var addInbound = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow program=\"{exePath}\" enable=yes profile=any",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            addInbound?.WaitForExit();

            // Add outbound rule
            using var addOutbound = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{RuleName}\" dir=out action=allow program=\"{exePath}\" enable=yes profile=any",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            addOutbound?.WaitForExit();

            Logger.Info("Firewall", $"Added firewall rule for {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Firewall", "Failed to add firewall rule", ex);
            return false;
        }
    }

    public static void RemoveFirewallRule()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name=\"{RuleName}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();

            Logger.Info("Firewall", "Removed firewall rule");
        }
        catch (Exception ex)
        {
            Logger.Error("Firewall", "Failed to remove firewall rule", ex);
        }
    }
}
