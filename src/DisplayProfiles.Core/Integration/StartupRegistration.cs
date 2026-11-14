using Microsoft.Win32;

namespace DisplayProfiles.Core.Integration;

/// <summary>Starts the tray app (--background) when the user signs in, via the per-user Run key.</summary>
public static class StartupRegistration
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "DisplayProfiles";

    public static bool IsEnabled(string keyPath = RunKeyPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled, string exePath, string keyPath = RunKeyPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{exePath}\" --background");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
