using DisplayProfiles.Core.Display;
using DisplayProfiles.Core.Integration;
using DisplayProfiles.Core.Model;
using DisplayProfiles.Core.Services;

namespace DisplayProfiles.App;

internal static class AppServices
{
    public static ProfileService CreateProfiles()
    {
        if (!PackageIdentity.IsPackaged)
        {
            PreviousNameMigration.Run(Environment.ProcessPath!);
        }

        var profiles = new ProfileService(new Win32DisplayManager(), new ProfileStore(ProfileStore.DefaultPath));
        if (profiles.SeedIfFirstRun(LegacyImporter.DefaultFolder) && profiles.Data.Settings.StartWithWindows)
        {
            // First run: start with Windows is on by default, so register it. After that it is only changed in Settings.
            StartupRegistration.SetEnabled(true, LaunchPath);
        }

        WidgetBridge.EnsureProtocolRegistered(Environment.ProcessPath!);
        profiles.Changed += () => WidgetBridge.Publish(profiles);
        return profiles;
    }

    /// <summary>What shortcuts and the Start-with-Windows entry should run: the exe, or its alias when packaged.</summary>
    public static string LaunchPath => PackageIdentity.LaunchPath(Environment.ProcessPath!);

    public static string AssetPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
}
