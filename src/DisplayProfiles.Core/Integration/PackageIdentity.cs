using Windows.Win32;
using Windows.Win32.Foundation;

namespace DisplayProfiles.Core.Integration;

/// <summary>
/// Whether the process runs with MSIX package identity. Packaged, the Game Bar widget lives in the same package as
/// the app, so the app talks to it through the package's own data folder, and the things an unpackaged app does
/// through the registry (start with Windows, the <c>displayprofiles:</c> link) come from the package manifest instead.
/// </summary>
public static class PackageIdentity
{
    /// <summary>The Startup task id declared in the package manifest.</summary>
    public const string StartupTaskId = "DisplayProfilesStartup";

    /// <summary>The execution alias declared in the package manifest; the only way to launch the packaged exe from outside.</summary>
    public const string ExecutionAlias = "DisplayProfiles.exe";

    private static readonly Lazy<string?> FamilyNameValue = new(ReadFamilyName);

    public static string? FamilyName => FamilyNameValue.Value;

    public static bool IsPackaged => FamilyName is not null;

    /// <summary>The package's LocalState folder, shared by the app and the widget.</summary>
    public static string? LocalStateFolder => FamilyName is null
        ? null
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", FamilyName, "LocalState");

    /// <summary>What to run for a shortcut or a Run entry: the execution alias when packaged, else the exe itself.</summary>
    public static string LaunchPath(string exePath) => IsPackaged
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", ExecutionAlias)
        : exePath;

    private static string? ReadFamilyName()
    {
        uint length = 0;
        var result = PInvoke.GetCurrentPackageFamilyName(ref length, default);
        if (result != WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
        {
            return null;
        }

        var buffer = new char[length];
        result = PInvoke.GetCurrentPackageFamilyName(ref length, buffer);
        return result == WIN32_ERROR.ERROR_SUCCESS ? new string(buffer, 0, (int)length - 1) : null;
    }
}