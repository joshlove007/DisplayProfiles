using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using MonitorToggle.Core.Services;

namespace MonitorToggle.Core.Integration;

public sealed record WidgetProfile(Guid Id, string Name, bool IsActive);

public sealed record WidgetSnapshot
{
    public int Version { get; init; } = 1;

    public DateTimeOffset Updated { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<WidgetProfile> Profiles { get; init; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WidgetSnapshot))]
internal sealed partial class WidgetJsonContext : JsonSerializerContext;

/// <summary>
/// Glue between the app and the Xbox Game Bar widget. The widget runs in a sandbox, so it can't read the app's
/// files or start it directly. Instead the app publishes a profile snapshot into the widget's own data folder and
/// registers a <c>monitortoggle:</c> URI that the widget launches to switch profiles.
/// </summary>
public static class WidgetBridge
{
    public const string UriScheme = "monitortoggle";
    public const string PackageNamePrefix = "MonitorToggle.GameBarWidget_";
    public const string SnapshotFileName = "profiles.json";
    public const string ClassesKeyPath = @"Software\Classes";

    /// <summary>The widget package's LocalState folder, or null if the widget isn't installed.</summary>
    public static string? FindWidgetDataFolder()
    {
        string packages = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
        if (!Directory.Exists(packages))
        {
            return null;
        }

        string? package = Directory.EnumerateDirectories(packages, PackageNamePrefix + "*").FirstOrDefault();
        return package is null ? null : Path.Combine(package, "LocalState");
    }

    public static WidgetSnapshot BuildSnapshot(ProfileService service)
    {
        var active = service.FindActiveProfile();
        return new WidgetSnapshot
        {
            Profiles = [.. service.Data.Profiles.Select(p => new WidgetProfile(p.Id, p.Name, p.Id == active?.Id))],
        };
    }

    /// <summary>Writes the snapshot for the widget. Returns false when the widget isn't installed.</summary>
    public static bool Publish(ProfileService service, string? folderOverride = null)
    {
        string? folder = folderOverride ?? FindWidgetDataFolder();
        if (folder is null)
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, SnapshotFileName);
            string temp = path + ".tmp";
            using (var stream = File.Create(temp))
            {
                JsonSerializer.Serialize(stream, BuildSnapshot(service), WidgetJsonContext.Default.WidgetSnapshot);
            }

            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The widget is optional; never let publishing break switching.
            return false;
        }
    }

    /// <summary>Turns <c>monitortoggle:apply/TV</c>, <c>monitortoggle:next</c> or <c>monitortoggle:refresh</c> into CLI arguments.</summary>
    public static string[]? TranslateUri(string uri)
    {
        if (!uri.StartsWith(UriScheme + ":", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string rest = uri[(UriScheme.Length + 1)..].TrimStart('/').TrimEnd('/');
        int slash = rest.IndexOf('/', StringComparison.Ordinal);
        string command = (slash < 0 ? rest : rest[..slash]).ToLowerInvariant();
        string argument = slash < 0 ? string.Empty : Uri.UnescapeDataString(rest[(slash + 1)..]);

        return command switch
        {
            "apply" when argument.Length > 0 => ["apply", argument],
            "next" or "toggle" => ["next"],
            "refresh" => ["refresh"],
            _ => null,
        };
    }

    /// <summary>Registers the <c>monitortoggle:</c> URI for the current user so the widget can launch the app.</summary>
    public static void EnsureProtocolRegistered(string exePath, string classesKeyPath = ClassesKeyPath)
    {
        string command = $"\"{exePath}\" \"%1\"";
        using var root = Registry.CurrentUser.CreateSubKey($@"{classesKeyPath}\{UriScheme}");
        using var open = root.CreateSubKey(@"shell\open\command");
        if (open.GetValue(null) as string == command)
        {
            return;
        }

        root.SetValue(null, "URL:MonitorToggle");
        root.SetValue("URL Protocol", string.Empty);
        open.SetValue(null, command);
    }
}
