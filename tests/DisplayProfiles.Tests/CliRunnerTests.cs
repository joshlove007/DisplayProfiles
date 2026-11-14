using DisplayProfiles.Core.Cli;
using DisplayProfiles.Core.Model;
using DisplayProfiles.Core.Services;
using static DisplayProfiles.Tests.TestData;

namespace DisplayProfiles.Tests;

public class CliRunnerTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "DisplayProfilesCli-" + Guid.NewGuid());
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();
    private readonly ProfileService service;

    public CliRunnerTests()
    {
        Directory.CreateDirectory(dir);
        var fake = new FakeDisplayManager(
        [
            Disp("ASUS", @"\\.\DISPLAY1", attached: true, primary: true),
            Disp("TV", @"\\.\DISPLAY3", attached: false),
        ]);
        service = new ProfileService(fake, new ProfileStore(Path.Combine(dir, "profiles.json"))) { SettleDelay = TimeSpan.Zero };
        var desk = Profile.Create("Desk", service.CaptureLayout());
        var tv = Profile.Create("TV", [Mon("TV", 0, 0, 3840, 2160, primary: true)]);
        service.Update(d => d with { Profiles = [desk, tv] });
    }

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private int Run(params string[] args) => CliRunner.Run(args, service, output, error);

    [Fact]
    public void No_arguments_or_flags_start_the_gui()
    {
        Assert.Equal(CliRunner.NotHandled, Run());
        Assert.Equal(CliRunner.NotHandled, Run("--background"));
    }

    [Fact]
    public void Apply_switches_profile_by_name_and_list_marks_the_active_one()
    {
        Assert.Equal(CliRunner.Success, Run("apply", "tv"));
        Assert.Contains("Switched to 'TV'", output.ToString());

        output.GetStringBuilder().Clear();
        Run("list");
        Assert.Equal($"  Desk{Environment.NewLine}* TV{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public void Toggle_cycles_to_the_next_profile()
    {
        Run("toggle");
        Assert.Equal("TV", service.FindActiveProfile()?.Name);

        Run("next");
        Assert.Equal("Desk", service.FindActiveProfile()?.Name);
    }

    [Fact]
    public void Unknown_profile_and_unknown_command_report_errors()
    {
        Assert.Equal(CliRunner.Failure, Run("apply", "nope"));
        Assert.Contains("No profile matches", error.ToString());
        Assert.Equal(CliRunner.Usage, Run("frobnicate"));
        Assert.Equal(CliRunner.Usage, Run("apply"));
    }

    [Fact]
    public void Capture_saves_the_current_layout_and_rejects_duplicates()
    {
        Assert.Equal(CliRunner.Success, Run("capture", "Movie", "night"));
        Assert.Contains(service.Data.Profiles, p => p.Name == "Movie night");

        Assert.Equal(CliRunner.Failure, Run("capture", "desk"));
        Assert.Equal(3, service.Data.Profiles.Count);
    }
}
