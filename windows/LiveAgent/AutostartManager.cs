using System.Diagnostics;
using Microsoft.Win32;

namespace LiveAgent;

public static class AutostartManager
{
    private const string AutostartName = "LiveDashboardAgent";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static string GetAutostartCommand()
    {
        string exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        return $"\"{Path.GetFullPath(exePath)}\"";
    }

    private static bool HasRegistryAutostart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            if (key == null) return false;
            var value = key.GetValue(AutostartName);
            return value is string s && !string.IsNullOrWhiteSpace(s);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Autostart registry query failed: {ex.Message}");
            return false;
        }
    }

    private static bool SetRegistryAutostart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (key == null) return false;

            if (enabled)
            {
                key.SetValue(AutostartName, GetAutostartCommand(), RegistryValueKind.String);
            }
            else
            {
                try { key.DeleteValue(AutostartName, false); }
                catch (ArgumentException) { /* Value doesn't exist */ }
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Autostart registry update failed: {ex.Message}");
            return false;
        }
    }

    private static bool HasLegacyStartupTask()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/query /tn {AutostartName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.Debug($"Autostart task query failed: {ex.Message}");
            return false;
        }
    }

    private static bool RemoveLegacyStartupTask()
    {
        if (!HasLegacyStartupTask())
            return true;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/delete /tn {AutostartName} /f",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(10000);
            return proc?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Legacy startup task removal failed: {ex.Message}");
            return false;
        }
    }

    public static bool IsAutostartEnabled()
    {
        return HasRegistryAutostart() || HasLegacyStartupTask();
    }

    public static bool ToggleAutostart(bool enable)
    {
        if (enable)
        {
            return SetRegistryAutostart(true);
        }
        else
        {
            bool registryOk = SetRegistryAutostart(false);
            bool legacyOk = RemoveLegacyStartupTask();
            return registryOk && legacyOk;
        }
    }
}
