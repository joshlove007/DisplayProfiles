using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisplayProfiles.Core.Model;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProfileData))]
internal sealed partial class ProfileJsonContext : JsonSerializerContext;

/// <summary>Reads and writes profiles.json. Writes are atomic so the CLI, tray app and GUI can share the file.</summary>
public sealed class ProfileStore(string filePath)
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplayProfiles", "profiles.json");

    public string FilePath { get; } = filePath;

    public ProfileData Load()
    {
        if (!File.Exists(FilePath))
        {
            return new ProfileData();
        }

        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize(stream, ProfileJsonContext.Default.ProfileData) ?? new ProfileData();
        }
        catch (JsonException)
        {
            // Keep the unreadable file around instead of silently destroying the user's profiles.
            File.Move(FilePath, FilePath + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            return new ProfileData();
        }
    }

    public void Save(ProfileData data)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = FilePath + ".tmp";
        using (var stream = File.Create(temp))
        {
            JsonSerializer.Serialize(stream, data, ProfileJsonContext.Default.ProfileData);
        }

        File.Move(temp, FilePath, overwrite: true);
    }
}
