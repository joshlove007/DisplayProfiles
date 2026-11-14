namespace DisplayProfiles.Core.Input;

/// <summary>Button bits, matching the XInput XINPUT_GAMEPAD_* values so they can be copied straight from a controller reading.</summary>
[Flags]
public enum PadButtons : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

public readonly record struct PadState(PadButtons Buttons, short LeftX = 0, short LeftY = 0, short RightX = 0, short RightY = 0);

/// <summary>What a controller press means to the app.</summary>
public enum GamepadAction
{
    Up,
    Down,
    Left,
    Right,
    Accept,
    Back,
    PreviousSection,
    NextSection,
    Primary,
    Secondary,
    ScrollUp,
    ScrollDown,
}
