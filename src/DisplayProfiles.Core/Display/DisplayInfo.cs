using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Core.Display;

/// <summary>A monitor that Windows currently knows about (connected), whether or not it is part of the desktop.</summary>
public sealed record DisplayInfo
{
    /// <summary>GDI device name such as \\.\DISPLAY3.</summary>
    public required string AdapterName { get; init; }

    public required string Key { get; init; }

    public required string FriendlyName { get; init; }

    public bool Attached { get; init; }

    public bool IsPrimary { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int RefreshHz { get; init; }

    public DisplayOrientation Orientation { get; init; }
}
