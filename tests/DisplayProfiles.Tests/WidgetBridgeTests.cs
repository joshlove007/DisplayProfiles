using System.Text.Json;
using DisplayProfiles.Core.Cli;
using DisplayProfiles.Core.Integration;
using DisplayProfiles.Core.Model;
using DisplayProfiles.Core.Services;
using static DisplayProfiles.Tests.TestData;

namespace DisplayProfiles.Tests;

public class WidgetBridgeTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "DisplayProfilesBridge-" + Guid.NewGuid());
    private readonly ProfileService service;

    public WidgetBridgeTests()
    {
        Directory.CreateDirectory(dir);
        var fake = new FakeDisplayManager(
        [
            Disp("ASUS", @"\\.\DISPLAY1", attached: true, primary: true),
            Disp("TV", @"\\.\DISPLAY3", attached: false),
        ]);
        service = new ProfileService(fake, new ProfileStore(Path.Combine(dir, "profiles.json"))) { SettleDelay = TimeSpan.Zero };
        service.Update(d => d with
        {
            Profiles = [Profile.Create("Desk", service.CaptureLayout()), Profile.Create("Living room", [Mon("TV", 0, 0, 3840, 2160, primary: true)])],
        });
    }

    public void Dispose() => Directory.Delete(dir, recursive: true);

    [Theory]
    [InlineData("displayprofiles:apply/TV", "apply|TV")]
    [InlineData("displayprofiles:apply/Living%20room", "apply|Living room")]
    [InlineData("DisplayProfiles://apply/TV/", "apply|TV")]
    [InlineData("displayprofiles:next", "next")]
    [InlineData("displayprofiles:refresh", "refresh")]
    public void Uris_translate_to_cli_arguments(string uri, string expected) =>
        Assert.Equal(expected, string.Join('|', WidgetBridge.TranslateUri(uri)!));

    [Theory]
    [InlineData("displayprofiles:apply")]
    [InlineData("displayprofiles:format-c")]
    [InlineData("https://example.com")]
    public void Unknown_uris_are_rejected(string uri) => Assert.Null(WidgetBridge.TranslateUri(uri));

    [Fact]
    public void Cli_runs_uri_commands()
    {
        var output = new StringWriter();

        int code = CliRunner.Run(["displayprofiles:apply/living"], service, output, output);

        Assert.Equal(CliRunner.Success, code);
        Assert.Equal("Living room", service.FindActiveProfile()?.Name);
        Assert.Equal(CliRunner.Usage, CliRunner.Run(["displayprofiles:bogus"], service, output, output));
    }

    [Fact]
    public void Cli_failures_are_published_for_the_widget()
    {
        string folder = Path.Combine(dir, "LocalState");
        var snapshot = WidgetBridge.BuildSnapshot(service, "TV is not responding");

        Assert.Equal("TV is not responding", snapshot.LastError);
        Assert.Null(WidgetBridge.BuildSnapshot(service).LastError);

        Assert.True(WidgetBridge.Publish(service, folder, "TV is not responding"));
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, WidgetBridge.SnapshotFileName)));
        Assert.Equal("TV is not responding", json.RootElement.GetProperty("lastError").GetString());
    }

    [Fact]
    public void Publish_records_the_last_applied_profile()
    {
        var living = service.Data.Profiles.Single(p => p.Name == "Living room");
        service.Apply(living);

        Assert.Equal(living.Id, WidgetBridge.BuildSnapshot(service).LastAppliedProfileId);
    }

    [Fact]
    public void Publish_writes_profiles_with_the_active_one_marked()
    {
        string folder = Path.Combine(dir, "LocalState");

        Assert.True(WidgetBridge.Publish(service, folder));

        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, WidgetBridge.SnapshotFileName)));
        var profiles = json.RootElement.GetProperty("profiles").EnumerateArray().ToList();
        Assert.Equal(["Desk", "Living room"], profiles.Select(p => p.GetProperty("name").GetString()));
        Assert.Equal([true, false], profiles.Select(p => p.GetProperty("isActive").GetBoolean()));
    }

    [Fact]
    public void Protocol_registration_points_at_the_exe_and_is_idempotent()
    {
        string key = @"Software\DisplayProfilesTests\" + Guid.NewGuid();
        try
        {
            WidgetBridge.EnsureProtocolRegistered(@"C:\Apps\DisplayProfiles.exe", key);
            WidgetBridge.EnsureProtocolRegistered(@"C:\Apps\DisplayProfiles.exe", key);

            using var command = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"{key}\displayprofiles\shell\open\command");
            Assert.Equal("\"C:\\Apps\\DisplayProfiles.exe\" \"%1\"", command!.GetValue(null));
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
        }
    }
}
