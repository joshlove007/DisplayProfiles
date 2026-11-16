using Microsoft.Win32;
using Windows.ApplicationModel;

namespace DisplayProfiles.Core.Integration;

/// <summary>Starts the tray app (--background) when the user signs in, via the per-user Run key.</summary>
public static class StartupRegistration
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "DisplayProfiles";

    public static bool IsEnabled(string keyPath = RunKeyPath)
    {
        if (PackageIdentity.IsPackaged)
        {
            return Task.Run(async () => (await StartupTask.GetAsync(PackageIdentity.StartupTaskId)).State)
                .GetAwaiter().GetResult() is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }

        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled, string exePath, string keyPath = RunKeyPath)
    {
        if (PackageIdentity.IsPackaged)
        {
            Task.Run(async () =>
            {
                var task = await StartupTask.GetAsync(PackageIdentity.StartupTaskId);
                if (enabled)
                {
                    await task.RequestEnableAsync();
                }
                else
                {
                    task.Disable();
                }
            }).GetAwaiter().GetResult();
            return;
        }

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
