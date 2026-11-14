using DisplayProfiles.Core.Display;
using DisplayProfiles.Core.Integration;
using DisplayProfiles.Core.Model;
using DisplayProfiles.Core.Services;

namespace DisplayProfiles.App;

internal static class AppServices
{
    public static ProfileService CreateProfiles()
    {
        PreviousNameMigration.Run(Environment.ProcessPath!);
        var profiles = new ProfileService(new Win32DisplayManager(), new ProfileStore(ProfileStore.DefaultPath));
        profiles.SeedIfFirstRun(LegacyImporter.DefaultFolder);
        WidgetBridge.EnsureProtocolRegistered(Environment.ProcessPath!);
        profiles.Changed += () => WidgetBridge.Publish(profiles);
        return profiles;
    }

    public static string AssetPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
}
