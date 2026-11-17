using DisplayProfiles.Core.Model;
using static DisplayProfiles.Tests.TestData;

namespace DisplayProfiles.Tests;

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
    public void Snap_pulls_a_far_away_monitor_against_the_others()
    {
        var a = Mon("A", 0, 0, primary: true);
        var b = Mon("B", 5000, 5000);

        var (x, y) = LayoutMath.Snap(b, 4000, 4000, [a, b], threshold: 80);

        Assert.True(LayoutMath.Touches(b with { X = x, Y = y }, a));
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

    [Fact]
    public void Duplicates_take_no_part_in_overlap_and_touch_checks()
    {
        var layout = new[] { Mon("A", 0, 0, primary: true), Mon("B", 2560, 0), Dup("C", of: "A"), Dup("D", of: "B", x: 2560) };

        Assert.Empty(LayoutMath.Validate(layout));
    }

    [Fact]
    public void A_duplicate_of_a_display_that_is_off_or_itself_a_duplicate_is_flagged()
    {
        var ofOff = new[] { Mon("A", 0, 0, primary: true), Mon("B", 2560, 0, enabled: false), Dup("C", of: "B") };
        var chained = new[] { Mon("A", 0, 0, primary: true), Dup("B", of: "A"), Dup("C", of: "B") };

        Assert.Contains(LayoutMath.Validate(ofOff), i => i.Kind == LayoutIssueKind.Duplicate);
        Assert.Contains(LayoutMath.Validate(chained), i => i.Kind == LayoutIssueKind.Duplicate);
    }

    [Fact]
    public void Normalize_never_anchors_on_a_duplicate_and_moves_it_with_its_original()
    {
        var layout = LayoutMath.Normalize([Mon("A", 100, 50, primary: true), Dup("B", of: "A", x: 100, y: 50)]);

        Assert.True(layout.Single(m => m.Key == "A").IsPrimary);
        var b = layout.Single(m => m.Key == "B");
        Assert.False(b.IsPrimary);
        Assert.Equal((0, 0), (b.X, b.Y));
    }

    [Fact]
    public void AreEquivalent_does_not_care_which_of_two_mirrored_displays_is_the_original()
    {
        var saved = new[] { Mon("A", 0, 0, primary: true), Dup("B", of: "A") };
        var live = new[] { Mon("B", 0, 0, primary: true), Dup("A", of: "B") };
        var extended = new[] { Mon("A", 0, 0, primary: true), Mon("B", 2560, 0) };
        var otherPair = new[] { Mon("A", 0, 0, primary: true), Mon("B", 2560, 0), Dup("C", of: "B", x: 2560) };

        Assert.True(LayoutMath.AreEquivalent(saved, live));
        Assert.False(LayoutMath.AreEquivalent(saved, extended));
        Assert.False(LayoutMath.AreEquivalent(extended, otherPair));
    }

    [Fact]
    public void CloseGaps_pulls_a_floating_monitor_against_the_rest()
    {
        var layout = new[] { Mon("A", 0, 0, primary: true), Mon("B", 5120, 0), Dup("C", of: "B", x: 5120) };

        var closed = LayoutMath.CloseGaps(layout);

        Assert.True(LayoutMath.Touches(closed.Single(m => m.Key == "A"), closed.Single(m => m.Key == "B")));
        Assert.Empty(LayoutMath.Validate(LayoutMath.Normalize(closed)));
    }

    [Fact]
    public void CloseGaps_leaves_a_connected_layout_alone()
    {
        var layout = new[] { Mon("A", 0, 0, primary: true), Mon("B", 2560, 0) };

        Assert.Equal(layout, LayoutMath.CloseGaps(layout));
    }

    [Fact]
    public void PlanMove_inserts_between_two_monitors_and_moves_the_rest_along()
    {
        var a = Mon("A", 0, 0, primary: true);
        var b = Mon("B", 2560, 0);
        var c = Mon("C", 5120, 0);

        // C dropped with its centre over the left half of B: it goes before B and B moves right to make room.
        var plan = LayoutMath.PlanMove(c, 1500, 0, [a, b, c], threshold: 0);

        Assert.Equal((2560, 0), (plan.X, plan.Y));
        Assert.Equal(("B", 5120, 0), Assert.Single(plan.Shifts));
    }

    [Fact]
    public void PlanMove_inserts_after_the_monitor_when_dropped_on_its_right_half()
    {
        var a = Mon("A", 0, 0, primary: true);
        var b = Mon("B", 2560, 0);
        var c = Mon("C", 5120, 0);

        var plan = LayoutMath.PlanMove(a, 3400, 0, [a, b, c], threshold: 0);

        Assert.Equal((5120, 0), (plan.X, plan.Y));
        Assert.Equal(("C", 7680, 0), Assert.Single(plan.Shifts));
    }

    [Fact]
    public void PlanMove_only_snaps_when_not_dropped_over_another_monitor()
    {
        var a = Mon("A", 0, 0, primary: true);
        var b = Mon("B", 5000, 0);

        var plan = LayoutMath.PlanMove(b, 2600, 30, [a, b], threshold: 80);

        Assert.Empty(plan.Shifts);
        Assert.True(LayoutMath.Touches(b with { X = plan.X, Y = plan.Y }, a));
    }

    [Fact]
    public void PlanMove_closes_the_gap_the_dragged_monitor_leaves_before_inserting()
    {
        // A B C in a row. Dragging B onto the left half of A: A and C close up around B's old place, so the whole row ends
        // up as B A C with no extra space (C does not move away).
        var a = Mon("A", 0, 0, primary: true);
        var b = Mon("B", 2560, 0);
        var c = Mon("C", 5120, 0);

        var plan = LayoutMath.PlanMove(b, -1000, 0, [a, b, c], threshold: 0);

        Assert.Equal((0, 0), (plan.X, plan.Y));
        var final = new Dictionary<string, (int X, int Y)> { ["A"] = (0, 0), ["C"] = (5120, 0) };
        foreach (var s in plan.Shifts) { final[s.Key] = (s.X, s.Y); }
        Assert.Equal((2560, 0), final["A"]);
        Assert.Equal((5120, 0), final["C"]);
    }
}