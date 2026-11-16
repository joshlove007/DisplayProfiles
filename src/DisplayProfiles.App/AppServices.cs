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
        profiles.SeedIfFirstRun(LegacyImporter.DefaultFolder);
        WidgetBridge.EnsureProtocolRegistered(Environment.ProcessPath!);
        profiles.Changed += () => WidgetBridge.Publish(profiles);
        return profiles;
    }

    /// <summary>What shortcuts and the Start-with-Windows entry should run: the exe, or its alias when packaged.</summary>
    public static string LaunchPath => PackageIdentity.LaunchPath(Environment.ProcessPath!);

    public static string AssetPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
}
