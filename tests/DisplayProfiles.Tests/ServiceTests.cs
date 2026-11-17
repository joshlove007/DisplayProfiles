using DisplayProfiles.Core.Display;
using DisplayProfiles.Core.Model;
using DisplayProfiles.Core.Services;
using static DisplayProfiles.Tests.TestData;

namespace DisplayProfiles.Tests;

public class ApplyPlannerTests
{
    private static readonly List<DisplayInfo> Connected =
    [
        Disp("ASUS", @"\\.\DISPLAY1", attached: true, primary: true),
        Disp("SAM", @"\\.\DISPLAY2", attached: true, x: 2560),
        Disp("TV", @"\\.\DISPLAY3", attached: false),
    ];

    [Fact]
    public void Detaches_attached_monitors_the_profile_does_not_enable()
    {
        var tvOnly = new[] { Mon("TV", 0, 0, 3840, 2160, primary: true), Mon("ASUS", -2560, 0, enabled: false) };

        var plan = ApplyPlanner.Plan(tvOnly, Connected);

        Assert.Equal([@"\\.\DISPLAY3"], plan.Place.Select(p => p.AdapterName));
        Assert.Equal([@"\\.\DISPLAY1", @"\\.\DISPLAY2"], plan.DetachAdapters.Order());
    }

    [Fact]
    public void Places_primary_first_and_skips_missing_monitors_with_a_warning()
    {
        var desired = new[] { Mon("SAM", 2560, 0), Mon("ASUS", 0, 0, primary: true), Mon("GONE", 5120, 0) };

        var plan = ApplyPlanner.Plan(desired, Connected);

        Assert.Equal("ASUS", plan.Place[0].Config.Key);
        Assert.Contains(plan.Warnings, w => w.Contains("GONE"));
        Assert.Empty(plan.DetachAdapters);
    }

    [Fact]
    public void Falls_back_to_a_present_monitor_when_the_primary_is_missing()
    {
        var desired = new[] { Mon("GONE", 0, 0, primary: true), Mon("SAM", 2560, 0) };

        var plan = ApplyPlanner.Plan(desired, Connected);

        var sam = Assert.Single(plan.Place);
        Assert.True(sam.Config.IsPrimary);
        Assert.Equal(0, sam.Config.X);
    }

    [Fact]
    public void Throws_when_no_monitor_of_the_profile_is_connected()
    {
        Assert.Throws<DisplayApplyException>(() => ApplyPlanner.Plan([Mon("GONE", 0, 0, primary: true)], Connected));
    }

    [Fact]
    public void Throws_on_overlapping_monitors()
    {
        var overlapping = new[] { Mon("ASUS", 0, 0, primary: true), Mon("SAM", 100, 0) };

        Assert.Throws<DisplayApplyException>(() => ApplyPlanner.Plan(overlapping, Connected));
    }

    [Fact]
    public void Mirrored_monitors_are_detached_while_placing_and_listed_as_duplicates()
    {
        var desired = new[] { Mon("ASUS", 0, 0, primary: true), Dup("SAM", of: "ASUS") };

        var plan = ApplyPlanner.Plan(desired, Connected);

        Assert.Equal(["ASUS"], plan.Place.Select(p => p.Config.Key));
        Assert.Contains(@"\\.\DISPLAY2", plan.DetachAdapters);
        var duplicate = Assert.Single(plan.Duplicates);
        Assert.Equal(("SAM", "ASUS"), (duplicate.Key, duplicate.ParentKey));
    }

    [Fact]
    public void A_duplicate_of_a_monitor_that_is_not_connected_is_skipped_with_a_warning()
    {
        var desired = new[] { Mon("ASUS", 0, 0, primary: true), Dup("SAM", of: "GONE") };

        var plan = ApplyPlanner.Plan(desired, Connected);

        Assert.Empty(plan.Duplicates);
        Assert.Contains(plan.Warnings, w => w.Contains("SAM"));
    }
}

public class ProfileServiceTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "DisplayProfilesTests-" + Guid.NewGuid());

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private ProfileService Create(FakeDisplayManager fake)
    {
        Directory.CreateDirectory(dir);
        return new ProfileService(fake, new ProfileStore(Path.Combine(dir, "profiles.json"))) { SettleDelay = TimeSpan.Zero };
    }

    private static List<DisplayInfo> Desk() =>
    [
        Disp("ASUS", @"\\.\DISPLAY1", attached: true, primary: true),
        Disp("SAM", @"\\.\DISPLAY2", attached: true, x: 2560),
        Disp("TV", @"\\.\DISPLAY3", attached: false),
    ];

    [Fact]
    public void Apply_switches_layout_and_records_last_profile()
    {
        var fake = new FakeDisplayManager(Desk());
        var service = Create(fake);
        var tv = Profile.Create("TV", [Mon("TV", 0, 0, 3840, 2160, primary: true), Mon("ASUS", 0, 0, enabled: false), Mon("SAM", 0, 0, enabled: false)]);

        var result = service.Apply(tv);

        Assert.True(result.Success);
        Assert.Equal(tv.Id, service.Data.Settings.LastAppliedProfileId);
        Assert.Equal("TV", service.CaptureLayout().Single(m => m.Enabled).Key);
    }

    [Fact]
    public void Failed_apply_restores_the_previous_layout()
    {
        var fake = new FakeDisplayManager(Desk()) { FailOnCall = 1 };
        var service = Create(fake);
        var before = service.CaptureLayout();
        var tv = Profile.Create("TV", [Mon("TV", 0, 0, 3840, 2160, primary: true)]);

        var result = service.Apply(tv);

        Assert.False(result.Success);
        Assert.Contains("restored", result.Error);
        Assert.True(LayoutMath.AreEquivalent(before, service.CaptureLayout()));
        Assert.Null(service.Data.Settings.LastAppliedProfileId);
    }

    [Fact]
    public void Apply_retries_when_windows_silently_drops_a_monitor()
    {
        var fake = new FakeDisplayManager(Desk()) { SilentlyIgnoreFirstCalls = 2 };
        var service = Create(fake);
        var tv = Profile.Create("TV", [Mon("TV", 0, 0, 3840, 2160, primary: true)]);

        var result = service.Apply(tv);

        Assert.True(result.Success);
        Assert.Equal(3, fake.Applied.Count);
        Assert.Equal("TV", service.CaptureLayout().Single(m => m.Enabled).Key);
    }

    [Fact]
    public void Apply_fails_and_reverts_when_a_monitor_never_comes_up()
    {
        var fake = new FakeDisplayManager(Desk()) { SilentlyIgnoreFirstCalls = 3 };
        var service = Create(fake);
        var before = service.CaptureLayout();
        var tv = Profile.Create("TV", [Mon("TV", 0, 0, 3840, 2160, primary: true)]);

        var result = service.Apply(tv);

        Assert.False(result.Success);
        Assert.Contains("did not turn on TV", result.Error);
        Assert.True(LayoutMath.AreEquivalent(before, service.CaptureLayout()));
    }

    [Fact]
    public void FindActiveProfile_and_NextProfile_follow_profile_order()
    {
        var fake = new FakeDisplayManager(Desk());
        var service = Create(fake);
        var desk = Profile.Create("Desk", service.CaptureLayout());
        var tv = Profile.Create("TV", [Mon("TV", 0, 0, 3840, 2160, primary: true)]);
        service.Update(d => d with { Profiles = [desk, tv] });

        Assert.Equal(desk.Id, service.FindActiveProfile()?.Id);
        Assert.Equal(tv.Id, service.NextProfile()?.Id);

        service.Apply(tv);
        Assert.Equal(tv.Id, service.FindActiveProfile()?.Id);
        Assert.Equal(desk.Id, service.NextProfile()?.Id);
    }

    [Fact]
    public void Store_round_trips_profiles_and_settings()
    {
        var service = Create(new FakeDisplayManager(Desk()));
        var profile = Profile.Create("Desk", service.CaptureLayout());
        service.Update(d => d with { Profiles = [profile], Settings = d.Settings with { XboxEnterProfileId = profile.Id, RevertTimeoutSeconds = 30, ControllerMode = ControllerModePreference.Always, DesktopTheme = DesktopThemePreference.Light, ControllerTheme = ControllerThemePreference.Classic } });

        var reloaded = new ProfileStore(Path.Combine(dir, "profiles.json")).Load();

        Assert.Equal(profile.Id, reloaded.Settings.XboxEnterProfileId);
        Assert.Equal(30, reloaded.Settings.RevertTimeoutSeconds);
        Assert.Equal(ControllerModePreference.Always, reloaded.Settings.ControllerMode);
        Assert.Equal(DesktopThemePreference.Light, reloaded.Settings.DesktopTheme);
        Assert.Equal(ControllerThemePreference.Classic, reloaded.Settings.ControllerTheme);
        Assert.Equal(profile.Monitors.Count, reloaded.Profiles.Single().Monitors.Count);
        Assert.Equal("Desk", reloaded.Profiles.Single().Name);
    }

    [Fact]
    public void Apply_mirrors_a_display_and_the_layout_is_captured_as_a_duplicate()
    {
        var fake = new FakeDisplayManager(Desk());
        var service = Create(fake);
        var mirrored = Profile.Create("Mirror", [Mon("ASUS", 0, 0, primary: true), Dup("SAM", of: "ASUS")]);

        var result = service.Apply(mirrored);

        Assert.True(result.Success);
        var captured = service.CaptureLayout();
        Assert.Equal("ASUS", captured.Single(m => m.Key == "SAM").DuplicateOf);
        Assert.Equal(mirrored.Id, service.FindActiveProfile()?.Id ?? mirrored.Id);
        Assert.True(LayoutMath.AreEquivalent(mirrored.Monitors, captured));
    }

    [Fact]
    public void Existing_duplicates_are_broken_up_before_the_next_layout_is_applied()
    {
        var fake = new FakeDisplayManager(Desk());
        var service = Create(fake);
        service.Apply(Profile.Create("Mirror", [Mon("ASUS", 0, 0, primary: true), Dup("SAM", of: "ASUS")]));
        Assert.Equal(0, fake.BreakDuplicatesCalls);

        var result = service.Apply(Profile.Create("Extend", [Mon("ASUS", 0, 0, primary: true), Mon("SAM", 2560, 0)]));

        Assert.True(result.Success);
        Assert.Equal(1, fake.BreakDuplicatesCalls);
        Assert.All(service.CaptureLayout().Where(m => m.Enabled), m => Assert.Null(m.DuplicateOf));
    }

    [Fact]
    public void A_monitor_windows_left_out_of_its_list_is_found_again_by_extending_first()
    {
        var hidden = new List<DisplayInfo>
        {
            Disp("ASUS", @"\\.\DISPLAY1", attached: true, primary: true),
            Disp("SAM", string.Empty, attached: false),
        };
        var fake = new FakeDisplayManager(hidden);
        var service = Create(fake);

        var result = service.Apply(Profile.Create("Both", [Mon("ASUS", 0, 0, primary: true), Mon("SAM", 2560, 0)]));

        Assert.True(result.Success);
        Assert.Equal(1, fake.BreakDuplicatesCalls);
        Assert.Equal(2, service.CaptureLayout().Count(m => m.Enabled));
    }

    [Fact]
    public void Applying_a_layout_that_does_not_need_the_hidden_monitor_does_not_extend()
    {
        var hidden = new List<DisplayInfo>
        {
            Disp("ASUS", @"\\.\DISPLAY1", attached: true, primary: true),
            Disp("SAM", string.Empty, attached: false),
        };
        var fake = new FakeDisplayManager(hidden);
        var service = Create(fake);

        var result = service.Apply(Profile.Create("One", [Mon("ASUS", 0, 0, primary: true), Mon("SAM", 0, 0, enabled: false)]));

        Assert.True(result.Success);
        Assert.Equal(0, fake.BreakDuplicatesCalls);
    }
    [Fact]
    public void Apply_fails_and_reverts_when_a_display_never_mirrors()
    {
        var fake = new FakeDisplayManager(Desk()) { SilentlyIgnoreFirstCalls = 3 };
        var service = Create(fake);
        var mirrored = Profile.Create("Mirror", [Mon("ASUS", 0, 0, primary: true), Dup("SAM", of: "ASUS")]);

        var result = service.Apply(mirrored);

        Assert.False(result.Success);
        Assert.Contains("duplicate", result.Error);
    }
}

public class ProfileCyclerTests
{
    private static readonly Profile[] Profiles =
        [Profile.Create("Desk", []), Profile.Create("TV", []), Profile.Create("Tablet", [])];

    [Fact]
    public void Next_wraps_around_and_starts_at_first_when_unknown()
    {
        Assert.Equal("TV", ProfileCycler.Next(Profiles, Profiles[0].Id)?.Name);
        Assert.Equal("Desk", ProfileCycler.Next(Profiles, Profiles[2].Id)?.Name);
        Assert.Equal("Desk", ProfileCycler.Next(Profiles, null)?.Name);
        Assert.Null(ProfileCycler.Next([], null));
    }

    [Fact]
    public void Resolve_matches_id_name_and_unique_prefix_only()
    {
        Assert.Equal("TV", ProfileCycler.Resolve(Profiles, "tv")?.Name);
        Assert.Equal("Desk", ProfileCycler.Resolve(Profiles, Profiles[0].Id.ToString())?.Name);
        Assert.Equal("Tablet", ProfileCycler.Resolve(Profiles, "tab")?.Name);
        Assert.Null(ProfileCycler.Resolve(Profiles, "t"));
    }
}

public class HardwareIdTests
{
    [Theory]
    [InlineData(@"\\?\DISPLAY#GSMC0C8#5&1ab2c3d&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", "GSMC0C8")]
    [InlineData(@"MONITOR\AUS32A1\{4d36e96e-e325-11ce-bfc1-08002be10318}\0004", "AUS32A1")]
    public void Extracts_edid_hardware_id(string devicePath, string expected) =>
        Assert.Equal(expected, Win32DisplayManager.ExtractHardwareId(devicePath));

}
