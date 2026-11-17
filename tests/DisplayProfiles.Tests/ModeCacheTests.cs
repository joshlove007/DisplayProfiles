using DisplayProfiles.Core.Display;
using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Tests;

public sealed class ModeCacheTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "DisplayProfilesTests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Remembers_modes_across_instances()
    {
        string file = Path.Combine(folder, "modes.json");
        new ModeCache(file).Remember("GSMC0C8", [new DisplayMode(3840, 2160, 120), new DisplayMode(2560, 1600, 59)]);

        var modes = new ModeCache(file).Get("GSMC0C8");

        Assert.Equal(2, modes.Count);
        Assert.Contains(new DisplayMode(3840, 2160, 120), modes);
        Assert.Contains(new DisplayMode(2560, 1600, 59), modes);
        Assert.Empty(new ModeCache(file).Get("OTHER"));
    }

    [Fact]
    public void A_newer_list_replaces_the_old_one()
    {
        string file = Path.Combine(folder, "modes.json");
        var cache = new ModeCache(file);
        cache.Remember("TV", [new DisplayMode(1920, 1080, 60)]);
        cache.Remember("TV", [new DisplayMode(3840, 2160, 60)]);

        Assert.Equal([new DisplayMode(3840, 2160, 60)], new ModeCache(file).Get("TV"));
    }

    [Fact]
    public void Survives_an_unreadable_file()
    {
        string file = Path.Combine(folder, "modes.json");
        Directory.CreateDirectory(folder);
        File.WriteAllText(file, "not json");

        var cache = new ModeCache(file);

        Assert.Empty(cache.Get("TV"));
        cache.Remember("TV", [new DisplayMode(1920, 1080, 60)]);
        Assert.Single(cache.Get("TV"));
    }
}
