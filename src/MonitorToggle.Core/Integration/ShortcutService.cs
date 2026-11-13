using System.Text;
using MonitorToggle.Core.Model;

namespace MonitorToggle.Core.Integration;

/// <summary>
/// Creates .lnk shortcuts that switch to a profile. They can be pinned anywhere, including the Xbox Game Bar
/// "LaunchDeck" widget, and run the app with the <c>apply</c> command.
/// </summary>
public static class ShortcutService
{
    public static string DesktopFolder => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string StartMenuFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MonitorToggle");

    public static string FileNameFor(Profile profile) => Sanitize($"MonitorToggle - {profile.Name}") + ".lnk";

    public const string NextProfileFileName = "MonitorToggle - Next profile.lnk";

    public static string CreateForProfile(Profile profile, string folder, string exePath) =>
        Create(Path.Combine(folder, FileNameFor(profile)), exePath, $"apply \"{profile.Name}\"", $"Switch displays to {profile.Name}");

    public static string CreateForNextProfile(string folder, string exePath) =>
        Create(Path.Combine(folder, NextProfileFileName), exePath, "toggle", "Switch to the next display profile");

    public static string Create(string shortcutPath, string exePath, string arguments, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new PlatformNotSupportedException("The Windows Script Host is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(shortcutPath);
        link.TargetPath = exePath;
        link.Arguments = arguments;
        link.WorkingDirectory = Path.GetDirectoryName(exePath);
        link.IconLocation = exePath + ",0";
        link.Description = description;
        link.Save();
        return shortcutPath;
    }

    public static (string Target, string Arguments)? Read(string shortcutPath)
    {
        if (!File.Exists(shortcutPath))
        {
            return null;
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(shortcutPath);
        return ((string)link.TargetPath, (string)link.Arguments);
    }

    private static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
        }

        return builder.ToString();
    }
}
