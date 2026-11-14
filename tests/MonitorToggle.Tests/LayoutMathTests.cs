using MonitorToggle.Core.Model;
using static MonitorToggle.Tests.TestData;

namespace MonitorToggle.Tests;

public class LayoutMathTests
{
    [Fact]
    public void Normalize_moves_primary_to_origin_and_shifts_others()
    {
        var layout = LayoutMath.Normalize([Mon("A", 100, 50, primary: false), Mon("B", 2660, 50, primary: true)]);

        var b = layout.Single(m => m.Key == "B");
        var a = layout.Single(m => m.Key == "A");
        Assert.True(b.IsPrimary);
        Assert.Equal((0, 0), (b.X, b.Y));
        Assert.Equal((-2560, 0), (a.X, a.Y));
    }

    [Fact]
    public void Normalize_picks_first_enabled_when_no_primary_and_ignores_disabled()
    {
        var layout = LayoutMath.Normalize([Mon("Off", 0, 0, primary: true, enabled: false), Mon("A", 10, 10), Mon("B", 2570, 10)]);

        Assert.True(layout.Single(m => m.Key == "A").IsPrimary);
        Assert.False(layout.Single(m => m.Key == "Off").IsPrimary);
        Assert.Equal(2560, layout.Single(m => m.Key == "B").X);
    }

    [Fact]
    public void Validate_reports_overlap_gap_and_missing_primary()
    {
        var overlap = LayoutMath.Validate([Mon("A", 0, 0, primary: true), Mon("B", 1000, 0)]);
        Assert.Contains(overlap, i => i.Kind == LayoutIssueKind.Overlap);

        var gap = LayoutMath.Validate([Mon("A", 0, 0, primary: true), Mon("B", 3000, 0)]);
        Assert.Contains(gap, i => i.Kind == LayoutIssueKind.Disconnected);

        var noPrimary = LayoutMath.Validate([Mon("A", 0, 0), Mon("B", 2560, 0)]);
        Assert.Contains(noPrimary, i => i.Kind == LayoutIssueKind.PrimaryCount);

        Assert.Empty(LayoutMath.Validate([Mon("A", 0, 0, primary: true), Mon("B", 2560, 400)]));
    }

    [Fact]
    public void Snap_attaches_to_nearby_edge_and_aligns_tops()
    {
        var a = Mon("A", 0, 0, primary: true);
        var b = Mon("B", 5000, 5000);

        var (x, y) = LayoutMath.Snap(b, 2600, 30, [a, b], threshold: 80);

        Assert.Equal((2560, 0), (x, y));
    }

    [Fact]
    public void Snap_does_not_move_when_far_from_other_monitors()
    {
        var a = Mon("A", 0, 0, primary: true);
        var b = Mon("B", 5000, 5000);

        Assert.Equal((4000, 4000), LayoutMath.Snap(b, 4000, 4000, [a, b], threshold: 80));
    }

    [Fact]
    public void Snap_pushes_out_of_overlap()
    {
        var a = Mon("A", 0, 0, primary: true);
        var b = Mon("B", 5000, 5000);

        var (x, y) = LayoutMath.Snap(b, 1000, 600, [a, b], threshold: 0);

        Assert.False(LayoutMath.Overlaps(b with { X = x, Y = y }, a));
    }

    [Fact]
    public void AreEquivalent_treats_59_and_60_hz_as_the_same_mode()
    {
        var saved = new[] { Mon("A", 0, 0, primary: true) with { RefreshHz = 60 } };
        var live = new[] { Mon("A", 0, 0, primary: true) with { RefreshHz = 59 } };
        var different = new[] { Mon("A", 0, 0, primary: true) with { RefreshHz = 144 } };

        Assert.True(LayoutMath.AreEquivalent(saved, live));
        Assert.False(LayoutMath.AreEquivalent(saved, different));
    }

    [Fact]
    public void AreEquivalent_ignores_disabled_monitors_and_origin_offset_but_not_position()
    {
        var one = new[] { Mon("A", 0, 0, primary: true), Mon("B", 2560, 0), Mon("C", 0, 0, enabled: false) };
        var shifted = new[] { Mon("A", 100, 100, primary: true), Mon("B", 2660, 100) };
        var moved = new[] { Mon("A", 0, 0, primary: true), Mon("B", 0, 1440) };

        Assert.True(LayoutMath.AreEquivalent(one, shifted));
        Assert.False(LayoutMath.AreEquivalent(one, moved));
    }
}
