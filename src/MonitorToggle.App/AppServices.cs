using MonitorToggle.Core.Display;
using MonitorToggle.Core.Model;
using MonitorToggle.Core.Services;

namespace MonitorToggle.App;

internal static class AppServices
{
    public static ProfileService CreateProfiles() =>
        new(new Win32DisplayManager(), new ProfileStore(ProfileStore.DefaultPath));

    public static string AssetPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
}
