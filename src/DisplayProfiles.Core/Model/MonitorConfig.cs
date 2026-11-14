namespace DisplayProfiles.Core.Model;

public enum DisplayOrientation
{
    Landscape = 0,
    Portrait = 1,
    LandscapeFlipped = 2,
    PortraitFlipped = 3,
}

/// <summary>
/// Desired state of one physical monitor inside a profile. Coordinates and sizes use the same
/// convention as the Windows desktop: the primary monitor sits at (0,0) and Width/Height are
/// the on-desktop pixel size (already rotated for portrait).
/// </summary>
public sealed record MonitorConfig
{
    /// <summary>Stable monitor identity: the EDID hardware id (e.g. GSMC0C8), suffixed with #2, #3 ... for identical monitors.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public bool Enabled { get; init; }

    public bool IsPrimary { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>Refresh rate in Hz; 0 means "whatever Windows picks".</summary>
    public int RefreshHz { get; init; }

    public DisplayOrientation Orientation { get; init; }

    public int Right => X + Width;

    public int Bottom => Y + Height;
}
