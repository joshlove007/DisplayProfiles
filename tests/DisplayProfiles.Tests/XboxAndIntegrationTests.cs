using DisplayProfiles.Core.Integration;
using DisplayProfiles.Core.Model;
using DisplayProfiles.Core.Services;
using DisplayProfiles.Core.XboxMode;
using static DisplayProfiles.Tests.TestData;

namespace DisplayProfiles.Tests;

internal sealed class FakeXboxSource(bool active) : IXboxModeSource
{
    public bool IsActive { get; set; } = active;

    public event Action<bool>? StateChanged;

    public void Raise(bool state)
    {
        IsActive = state;
        StateChanged?.Invoke(state);
    }
}

public class XboxModeControllerTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "DisplayProfilesXbox-" + Guid.NewGuid());
    private readonly ProfileService service;
    private readonly Profile desk;
    private readonly Profile tv;

    public XboxModeControllerTests()
    {
        Directory.CreateDirectory(dir);
        var fake = new FakeDisplayManager(
        [
            Disp("ASUS", @"\\.\DISPLAY1", attached: true, primary: true),
            Disp("TV", @"\\.\DISPLAY3", attached: false),
        ]);
        service = new ProfileService(fake, new ProfileStore(Path.Combine(dir, "profiles.json"))) { SettleDelay = TimeSpan.Zero };
        desk = Profile.Create("Desk", service.CaptureLayout());
        tv = Profile.Create("TV", [Mon("TV", 0, 0, 3840, 2160, primary: true)]);
        service.Update(d => d with
        {
            Profiles = [desk, tv],
            Settings = d.Settings with { XboxEnterProfileId = tv.Id, XboxExitProfileId = desk.Id },
        });
    }

    public void Dispose() => Directory.Delete(dir, recursive: true);

    [Fact]
    public async Task Entering_and_leaving_xbox_mode_applies_the_configured_profiles()
    {
        var source = new FakeXboxSource(active: false);
        var controller = new XboxModeController(source, service) { SettleDelay = TimeSpan.Zero };

        source.IsActive = true;
        await controller.HandleAsync(true);
        Assert.Equal(tv.Id, service.FindActiveProfile()?.Id);

        source.IsActive = false;
        await controller.HandleAsync(false);
        Assert.Equal(desk.Id, service.FindActiveProfile()?.Id);
    }

    [Fact]
    public async Task A_state_that_flipped_back_during_the_settle_delay_is_ignored()
    {
        var source = new FakeXboxSource(active: false);
        var controller = new XboxModeController(source, service) { SettleDelay = TimeSpan.Zero };

        await controller.HandleAsync(true);

        Assert.Equal(desk.Id, service.FindActiveProfile()?.Id);
    }

    [Fact]
    public async Task Nothing_happens_when_no_profile_is_configured()
    {
        service.Update(d => d with { Settings = d.Settings with { XboxEnterProfileId = null } });
        var source = new FakeXboxSource(active: true);
        var controller = new XboxModeController(source, service) { SettleDelay = TimeSpan.Zero };

        await controller.HandleAsync(true);

        Assert.Equal(desk.Id, service.FindActiveProfile()?.Id);
    }

    [Fact]
    public async Task Starting_while_already_in_xbox_mode_applies_the_enter_profile()
    {
        var source = new FakeXboxSource(active: true);
        var controller = new XboxModeController(source, service) { SettleDelay = TimeSpan.Zero };
        var applied = new TaskCompletionSource<string>();
        controller.Applied += (name, _) => applied.TrySetResult(name);

        controller.Start();

        Assert.Equal("TV", await applied.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_windows_api_can_be_created_and_started_when_present()
    {
        using var monitor = FullScreenExperienceMonitor.TryCreate();
        if (monitor is null)
        {
            return;
        }

        Assert.True(monitor.Start());
        _ = monitor.IsActive;
    }
}

public class IntegrationTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "DisplayProfilesInt-" + Guid.NewGuid());

    public IntegrationTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private static List<MonitorConfig> Live() =>
    [
        Mon("AUS32A1", 0, 0, primary: true),
        Mon("SAM7173", 2560, 0),
        Mon("GSMC0C8", 5120, 0, 4096, 2160, enabled: false),
        Mon("SDC41A3", 5120, 0, 2560, 1600, enabled: false),
    ];

    [Fact]
    public void Legacy_script_and_state_become_desk_and_tv_profiles()
    {
        File.WriteAllText(Path.Combine(dir, "toggle-monitor.ps1"), "$TvId = 'GSMC0C8'\n$PrimaryOffId = 'AUS32A1'\n$SamsungId = 'SAM7173'\n");
        File.WriteAllText(Path.Combine(dir, ".monitor-toggle-state"), "RES 3840 2160 60\nPOS SAM7173 -5120 0\nPOS AUS32A1 -2560 0\n");

        var profiles = LegacyImporter.TryImport(dir, Live());

        var desk = profiles.Single(p => p.Name == "Desk").Monitors;
        Assert.Equal([true, true, false, false], desk.Select(m => m.Enabled));
        Assert.True(desk.Single(m => m.Key == "AUS32A1").IsPrimary);
        Assert.Equal(2560, desk.Single(m => m.Key == "SAM7173").X);

        var tv = profiles.Single(p => p.Name == "TV").Monitors;
        var tvMonitor = tv.Single(m => m.Key == "GSMC0C8");
        Assert.True(tvMonitor.IsPrimary && tvMonitor.Enabled);
        Assert.Equal((3840, 2160, 60), (tvMonitor.Width, tvMonitor.Height, tvMonitor.RefreshHz));
        Assert.Equal(-2560, tv.Single(m => m.Key == "AUS32A1").X);
        Assert.Equal(-5120, tv.Single(m => m.Key == "SAM7173").X);
        Assert.False(tv.Single(m => m.Key == "SDC41A3").Enabled);
        Assert.Empty(LayoutMath.Validate(tv.Where(m => m.Enabled).ToList()));
    }

    [Fact]
    public void Missing_legacy_script_imports_nothing()
    {
        Assert.Empty(LegacyImporter.TryImport(dir, Live()));
    }

    [Fact]
    public void First_run_seeds_profiles_once()
    {
        var fake = new FakeDisplayManager([Disp("ASUS", @"\\.\DISPLAY1", attached: true, primary: true)]);
        var service = new ProfileService(fake, new ProfileStore(Path.Combine(dir, "profiles.json")));

        Assert.True(service.SeedIfFirstRun(dir));
        Assert.Equal("Current layout", service.Data.Profiles.Single().Name);
        Assert.False(service.SeedIfFirstRun(dir));
    }

    [Fact]
    public void Startup_registration_round_trips()
    {
        string key = @"Software\DisplayProfilesTests\" + Guid.NewGuid();
        try
        {
            Assert.False(StartupRegistration.IsEnabled(key));
            StartupRegistration.SetEnabled(true, @"C:\Apps\DisplayProfiles.exe", key);
            Assert.True(StartupRegistration.IsEnabled(key));
            StartupRegistration.SetEnabled(false, @"C:\Apps\DisplayProfiles.exe", key);
            Assert.False(StartupRegistration.IsEnabled(key));
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void Shortcuts_point_at_the_app_with_the_apply_command()
    {
        var profile = Profile.Create("Living: room", []);

        string path = ShortcutService.CreateForProfile(profile, dir, @"C:\Apps\DisplayProfiles.exe");

        Assert.EndsWith("Display Profiles - Living_ room.lnk", path);
        var (target, arguments) = ShortcutService.Read(path)!.Value;
        Assert.Equal(@"C:\Apps\DisplayProfiles.exe", target);
        Assert.Equal("apply \"Living: room\"", arguments);
    }
}
