namespace DisplayProfiles.Core.Input;

/// <summary>
/// Turns raw controller readings into discrete actions: buttons fire once per press; the D-pad and left stick fire
/// once and then repeat while held (like holding an arrow key); the right stick scrolls while pushed.
/// </summary>
public sealed class GamepadRepeater
{
    private const double StickThreshold = 0.55;
    private const double ScrollThreshold = 0.35;

    private readonly Dictionary<GamepadAction, TimeSpan> nextRepeat = [];
    private PadButtons previousButtons;
    private TimeSpan nextScroll;

    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(380);

    public TimeSpan RepeatInterval { get; init; } = TimeSpan.FromMilliseconds(110);

    public TimeSpan ScrollInterval { get; init; } = TimeSpan.FromMilliseconds(40);

    public IReadOnlyList<GamepadAction> Update(PadState state, TimeSpan now)
    {
        var actions = new List<GamepadAction>();
        var pressed = state.Buttons & ~previousButtons;
        previousButtons = state.Buttons;

        Edge(pressed, PadButtons.A, GamepadAction.Accept, actions);
        Edge(pressed, PadButtons.B, GamepadAction.Back, actions);
        Edge(pressed, PadButtons.Y, GamepadAction.Primary, actions);
        Edge(pressed, PadButtons.X, GamepadAction.Secondary, actions);
        Edge(pressed, PadButtons.LeftShoulder, GamepadAction.PreviousSection, actions);
        Edge(pressed, PadButtons.RightShoulder, GamepadAction.NextSection, actions);

        var held = HeldDirections(state);
        foreach (var direction in new[] { GamepadAction.Up, GamepadAction.Down, GamepadAction.Left, GamepadAction.Right })
        {
            if (!held.Contains(direction))
            {
                nextRepeat.Remove(direction);
                continue;
            }

            if (!nextRepeat.TryGetValue(direction, out var due))
            {
                actions.Add(direction);
                nextRepeat[direction] = now + InitialDelay;
            }
            else if (now >= due)
            {
                actions.Add(direction);
                nextRepeat[direction] = now + RepeatInterval;
            }
        }

        double scroll = state.RightY / 32767.0;
        if (Math.Abs(scroll) >= ScrollThreshold)
        {
            if (now >= nextScroll)
            {
                actions.Add(scroll > 0 ? GamepadAction.ScrollUp : GamepadAction.ScrollDown);
                nextScroll = now + ScrollInterval;
            }
        }
        else
        {
            nextScroll = TimeSpan.Zero;
        }

        return actions;
    }

    private static void Edge(PadButtons pressed, PadButtons button, GamepadAction action, List<GamepadAction> actions)
    {
        if (pressed.HasFlag(button))
        {
            actions.Add(action);
        }
    }

    private static HashSet<GamepadAction> HeldDirections(PadState state)
    {
        var held = new HashSet<GamepadAction>();
        if (state.Buttons.HasFlag(PadButtons.DPadUp)) { held.Add(GamepadAction.Up); }
        if (state.Buttons.HasFlag(PadButtons.DPadDown)) { held.Add(GamepadAction.Down); }
        if (state.Buttons.HasFlag(PadButtons.DPadLeft)) { held.Add(GamepadAction.Left); }
        if (state.Buttons.HasFlag(PadButtons.DPadRight)) { held.Add(GamepadAction.Right); }

        // Only the dominant stick axis counts, so a slightly diagonal push doesn't jump sideways.
        double x = state.LeftX / 32767.0;
        double y = state.LeftY / 32767.0;
        if (Math.Abs(x) >= StickThreshold && Math.Abs(x) >= Math.Abs(y))
        {
            held.Add(x > 0 ? GamepadAction.Right : GamepadAction.Left);
        }
        else if (Math.Abs(y) >= StickThreshold)
        {
            held.Add(y > 0 ? GamepadAction.Up : GamepadAction.Down);
        }

        return held;
    }
}
