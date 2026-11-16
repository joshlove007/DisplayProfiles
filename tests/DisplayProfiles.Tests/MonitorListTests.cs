using DisplayProfiles.Core.Display;
using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Tests;

public class MonitorListTests
{
    private static MonitorConfig Config(string key, bool enabled = false) =>
        new() { Key = key, Name = key, Enabled = enabled, Width = 1920, Height = 1080, RefreshHz = 60 };

    private static DisplayInfo Display(string key, string adapter) =>
        new() { AdapterName = adapter, Key = key, FriendlyName = key, Attached = true };

    [Fact]
    public void Connected_monitors_come_first_in_windows_output_order_then_the_rest()
    {
        var displays = new[]
        {
            Display("TV", @"\\.\DISPLAY10"),
            Display("DESK1", @"\\.\DISPLAY2"),
            Display("DESK2", @"\\.\DISPLAY1"),
        };
        var configs = new[] { Config("GONE"), Config("TV"), Config("DESK1"), Config("OTHER"), Config("DESK2") };

        var order = MonitorList.Order(configs, displays).Select(c => c.Key);

        // DISPLAY10 sorts after DISPLAY2, and monitors that aren't connected keep their saved order at the end.
        Assert.Equal(["DESK2", "DESK1", "TV", "GONE", "OTHER"], order);
    }

    [Fact]
    public void Gaps_in_windows_adapter_numbers_do_not_matter_because_numbers_are_list_positions()
    {
        // Adapters 1, 2, 3 and 5 (no 4), the situation after a remote desktop session came and went.
        var displays = new[]
        {
            Display("A", @"\\.\DISPLAY1"),
            Display("B", @"\\.\DISPLAY2"),
            Display("C", @"\\.\DISPLAY3"),
            Display("D", @"\\.\DISPLAY5"),
        };
        var configs = displays.Select(d => Config(d.Key)).Append(Config("DEFAULT_MONITOR")).Append(Config("DEFAULT_MONITOR#2")).ToList();

        var numbers = MonitorList.Order(configs, displays)
            .Select((c, i) => (c.Key, Number: i + 1))
            .ToList();

        Assert.Equal([1, 2, 3, 4, 5, 6], numbers.Select(n => n.Number));
        Assert.Equal("D", numbers[3].Key);
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, true)]
    public void A_disconnected_monitor_that_is_off_is_only_listed_on_request(bool connected, bool enabled, bool showOff, bool expected) =>
        Assert.Equal(expected, MonitorList.IsVisible(Config("X", enabled), connected, showOff));
}
