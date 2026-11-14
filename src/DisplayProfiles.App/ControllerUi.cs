namespace DisplayProfiles.App;

/// <summary>Whether the app is currently showing its large, controller-friendly (Xbox mode) layout.</summary>
internal static class ControllerUi
{
    public static bool IsActive { get; private set; }

    /// <summary>Command line override: true for --xbox, false for --desktop, null to follow the setting.</summary>
    public static bool? Forced { get; set; }

    /// <summary>Show the controller layout in a normal window instead of full screen (--windowed).</summary>
    public static bool Windowed { get; set; }

    public static void SetActive(bool active) => IsActive = active;

    /// <summary>The UI is designed for a 720 dip tall screen and scaled up from there, so it reads from across a room.</summary>
    public static double ScaleFor(double heightDips) => Math.Clamp(heightDips / 720.0, 1.25, 2.5);
}
