using DisplayProfiles.Core.Integration;
using Microsoft.Win32;

namespace DisplayProfiles.Tests;

public class PreviousNameMigrationTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "DisplayProfilesMigration-" + Guid.NewGuid());
    private readonly string keyRoot = @"Software\DisplayProfilesTests\" + Guid.NewGuid();

    public PreviousNameMigrationTests() => Directory.CreateDirectory(dir);

    public void Dispose()
    {
        Directory.Delete(dir, recursive: true);
        Registry.CurrentUser.DeleteSubKeyTree(keyRoot, throwOnMissingSubKey: false);
    }

    [Fact]
    public void Profiles_are_copied_once_and_never_overwrite_new_ones()
    {
        Directory.CreateDirectory(Path.Combine(dir, "MonitorToggle"));
        File.WriteAllText(Path.Combine(dir, "MonitorToggle", "profiles.json"), "old");

        PreviousNameMigration.Run(@"C:\Apps\DisplayProfiles.exe", dir, keyRoot + @"\Classes", keyRoot + @"\Run");
        string target = Path.Combine(dir, "DisplayProfiles", "profiles.json");
        Assert.Equal("old", File.ReadAllText(target));

        File.WriteAllText(target, "new");
        PreviousNameMigration.Run(@"C:\Apps\DisplayProfiles.exe", dir, keyRoot + @"\Classes", keyRoot + @"\Run");
        Assert.Equal("new", File.ReadAllText(target));
    }

    [Fact]
    public void Old_startup_entry_and_link_are_replaced()
    {
        using (var run = Registry.CurrentUser.CreateSubKey(keyRoot + @"\Run"))
        {
            run.SetValue("MonitorToggle", @"""C:\Old\MonitorToggle.exe"" --background");
        }

        Registry.CurrentUser.CreateSubKey(keyRoot + @"\Classes\monitortoggle").Dispose();

        PreviousNameMigration.Run(@"C:\Apps\DisplayProfiles.exe", dir, keyRoot + @"\Classes", keyRoot + @"\Run");

        using var runKey = Registry.CurrentUser.OpenSubKey(keyRoot + @"\Run")!;
        Assert.Null(runKey.GetValue("MonitorToggle"));
        Assert.True(StartupRegistration.IsEnabled(keyRoot + @"\Run"));
        Assert.Null(Registry.CurrentUser.OpenSubKey(keyRoot + @"\Classes\monitortoggle"));
    }

    [Fact]
    public void Nothing_is_enabled_when_the_old_app_was_not_set_to_start_with_windows()
    {
        PreviousNameMigration.Run(@"C:\Apps\DisplayProfiles.exe", dir, keyRoot + @"\Classes", keyRoot + @"\Run");

        Assert.False(StartupRegistration.IsEnabled(keyRoot + @"\Run"));
    }
}
