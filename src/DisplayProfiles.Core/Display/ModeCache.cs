using System.Text.Json;
using System.Text.Json.Serialization;
using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Core.Display;

[JsonSerializable(typeof(Dictionary<string, string[]>))]
internal sealed partial class ModeCacheJsonContext : JsonSerializerContext;

/// <summary>
/// Remembers the display modes each monitor offered the last time it was part of the desktop. Windows only gives a
/// monitor's full list of resolutions and refresh rates while the monitor is on the desktop; once it is switched off
/// the list shrinks to a generic one, so the editor uses what was remembered instead.
/// </summary>
public sealed class ModeCache(string filePath)
{
    private readonly Lock gate = new();
    private Dictionary<string, string[]>? entries;

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplayProfiles", "modes.json");

    public IReadOnlyList<DisplayMode> Get(string key)
    {
        lock (gate)
        {
            return Load().TryGetValue(key, out var saved) ? [.. saved.Select(Parse).OfType<DisplayMode>()] : [];
        }
    }

    /// <summary>Stores the modes the monitor offers right now. Writes only when the list has changed.</summary>
    public void Remember(string key, IReadOnlyList<DisplayMode> modes)
    {
        string[] text = [.. modes.Select(m => $"{m.Width}x{m.Height}@{m.RefreshHz}").Distinct().Order()];
        lock (gate)
        {
            var all = Load();
            if (text.Length == 0 || (all.TryGetValue(key, out var existing) && existing.SequenceEqual(text)))
            {
                return;
            }

            all[key] = text;
            Save(all);
        }
    }

    private Dictionary<string, string[]> Load()
    {
        if (entries is not null)
        {
            return entries;
        }

        try
        {
            if (File.Exists(filePath))
            {
                using var stream = File.OpenRead(filePath);
                entries = JsonSerializer.Deserialize(stream, ModeCacheJsonContext.Default.DictionaryStringStringArray);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            entries = null;
        }

        return entries ??= [];
    }

    private void Save(Dictionary<string, string[]> all)
    {
        // The cache is only a convenience, so a failed write must never get in the way.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            string temp = filePath + ".tmp";
            using (var stream = File.Create(temp))
            {
                JsonSerializer.Serialize(stream, all, ModeCacheJsonContext.Default.DictionaryStringStringArray);
            }

            File.Move(temp, filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static DisplayMode? Parse(string text)
    {
        // 3840x2160@119
        int at = text.IndexOf('@', StringComparison.Ordinal);
        int cross = text.IndexOf('x', StringComparison.Ordinal);
        return at > cross && cross > 0 &&
            int.TryParse(text.AsSpan(0, cross), out int width) &&
            int.TryParse(text.AsSpan(cross + 1, at - cross - 1), out int height) &&
            int.TryParse(text.AsSpan(at + 1), out int hz)
                ? new DisplayMode(width, height, hz)
                : null;
    }
}
