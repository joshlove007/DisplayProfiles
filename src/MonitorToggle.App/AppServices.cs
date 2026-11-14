using MonitorToggle.Core.Display;
using MonitorToggle.Core.Integration;
using MonitorToggle.Core.Model;
using MonitorToggle.Core.Services;

namespace MonitorToggle.App;

internal static class AppServices
{
    public static ProfileService CreateProfiles()
    {
        var profiles = new ProfileService(new Win32DisplayManager(), new ProfileStore(ProfileStore.DefaultPath));
        profiles.SeedIfFirstRun(LegacyImporter.DefaultFolder);
        WidgetBridge.EnsureProtocolRegistered(Environment.ProcessPath!);
        profiles.Changed += () => WidgetBridge.Publish(profiles);
        return profiles;
    }

    public static string AssetPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
}
