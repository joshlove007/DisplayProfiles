using Microsoft.Win32;

namespace DisplayProfiles.Core.Integration;

/// <summary>Carries data and registrations over from the app's previous name, "MonitorToggle". Safe to run on every start.</summary>
public static class PreviousNameMigration
{
    private const string OldName = "MonitorToggle";
    private const string OldUriScheme = "monitortoggle";

    public static void Run(string exePath, string? appDataRoot = null, string classesKeyPath = WidgetBridge.ClassesKeyPath, string runKeyPath = StartupRegistration.RunKeyPath)
    {
        appDataRoot ??= Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        MoveProfiles(appDataRoot);
        MoveStartupEntry(exePath, runKeyPath);

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{classesKeyPath}\{OldUriScheme}", throwOnMissingSubKey: false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // A leftover link for an app that no longer exists is harmless.
        }
    }

    private static void MoveProfiles(string appDataRoot)
    {
        string oldFile = Path.Combine(appDataRoot, OldName, "profiles.json");
        string newFile = Path.Combine(appDataRoot, "DisplayProfiles", "profiles.json");
        if (!File.Exists(oldFile) || File.Exists(newFile))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newFile)!);
            File.Copy(oldFile, newFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The app falls back to creating fresh profiles.
        }
    }

    private static void MoveStartupEntry(string exePath, string runKeyPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
        if (key?.GetValue(OldName) is null)
        {
            return;
        }

        key.DeleteValue(OldName, throwOnMissingValue: false);
        StartupRegistration.SetEnabled(true, exePath, runKeyPath);
    }
}
