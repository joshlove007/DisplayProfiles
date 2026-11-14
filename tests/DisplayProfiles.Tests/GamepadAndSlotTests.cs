using DisplayProfiles.Core.Input;
using DisplayProfiles.Core.Model;
using static DisplayProfiles.Tests.TestData;

namespace DisplayProfiles.Tests;

public class GamepadRepeaterTests
{
    private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Buttons_fire_once_per_press()
    {
        var pad = new GamepadRepeater();

        Assert.Equal([GamepadAction.Accept], pad.Update(new PadState(PadButtons.A), Ms(0)));
        Assert.Empty(pad.Update(new PadState(PadButtons.A), Ms(16)));
        Assert.Empty(pad.Update(new PadState(PadButtons.None), Ms(32)));
        Assert.Equal([GamepadAction.Back, GamepadAction.NextSection], pad.Update(new PadState(PadButtons.B | PadButtons.RightShoulder), Ms(48)));
    }

    [Fact]
    public void Held_direction_waits_then_repeats()
    {
        var pad = new GamepadRepeater();
        var down = new PadState(PadButtons.DPadDown);

        Assert.Equal([GamepadAction.Down], pad.Update(down, Ms(0)));
        Assert.Empty(pad.Update(down, Ms(200)));
        Assert.Equal([GamepadAction.Down], pad.Update(down, Ms(400)));
        Assert.Empty(pad.Update(down, Ms(450)));
        Assert.Equal([GamepadAction.Down], pad.Update(down, Ms(520)));

        pad.Update(new PadState(PadButtons.None), Ms(540));
        Assert.Equal([GamepadAction.Down], pad.Update(down, Ms(560)));
    }

    [Fact]
    public void Left_stick_uses_only_the_dominant_axis_above_the_threshold()
    {
        var pad = new GamepadRepeater();

        Assert.Empty(pad.Update(new PadState(PadButtons.None, LeftX: 8000), Ms(0)));
        Assert.Equal([GamepadAction.Right], pad.Update(new PadState(PadButtons.None, LeftX: 30000, LeftY: 20000), Ms(10)));

        var other = new GamepadRepeater();
        Assert.Equal([GamepadAction.Down], other.Update(new PadState(PadButtons.None, LeftX: 5000, LeftY: -30000), Ms(0)));
    }

    [Fact]
    public void Right_stick_scrolls_while_pushed()
    {
        var pad = new GamepadRepeater();
        var up = new PadState(PadButtons.None, RightY: 30000);

        Assert.Equal([GamepadAction.ScrollUp], pad.Update(up, Ms(0)));
        Assert.Empty(pad.Update(up, Ms(20)));
        Assert.Equal([GamepadAction.ScrollUp], pad.Update(up, Ms(45)));
        Assert.Equal([GamepadAction.ScrollDown], pad.Update(new PadState(PadButtons.None, RightY: -30000), Ms(90)));
    }
}

public class AttachSlotTests
{
    private static readonly MonitorConfig A = Mon("A", 0, 0, primary: true);
    private static readonly MonitorConfig B = Mon("B", 2560, 0);

    [Fact]
    public void Slots_touch_another_monitor_and_never_overlap()
    {
        var slots = LayoutMath.AttachSlots(B, [A, B]);

        Assert.Contains((2560, 0), slots);
        Assert.Contains((-2560, 0), slots);
        Assert.Contains((0, 1440), slots);
        Assert.Contains((0, -1440), slots);
        Assert.All(slots, s => Assert.False(LayoutMath.Overlaps(B with { X = s.X, Y = s.Y }, A)));
    }

    [Fact]
    public void Slots_around_two_neighbours_skip_positions_that_would_overlap_either()
    {
        var c = Mon("C", 5120, 0);

        var slots = LayoutMath.AttachSlots(c, [A, B, c]);

        Assert.DoesNotContain((2560, 0), slots);
        Assert.Contains((5120, 0), slots);
        Assert.Contains((-2560, 0), slots);
    }

    [Fact]
    public void NextSlot_moves_in_the_pressed_direction_to_the_nearest_slot()
    {
        var slots = LayoutMath.AttachSlots(B, [A, B]);
        (int X, int Y) current = (2560, 0);

        Assert.Equal((0, 1440), LayoutMath.NextSlot(current, slots, MoveDirection.Down));
        Assert.Equal((0, -1440), LayoutMath.NextSlot(current, slots, MoveDirection.Up));
        Assert.Equal((-2560, 0), LayoutMath.NextSlot(current, slots, MoveDirection.Left));
        Assert.Null(LayoutMath.NextSlot(current, slots, MoveDirection.Right));
    }

    [Fact]
    public void Moving_down_then_right_walks_around_the_neighbour()
    {
        var slots = LayoutMath.AttachSlots(B, [A, B]);

        var below = LayoutMath.NextSlot((2560, 0), slots, MoveDirection.Down)!.Value;
        var back = LayoutMath.NextSlot(below, slots, MoveDirection.Right);

        Assert.Equal((0, 1440), below);
        Assert.Equal((2560, 0), back);
    }
}
