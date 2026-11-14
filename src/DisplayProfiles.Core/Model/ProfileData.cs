namespace DisplayProfiles.Core.Model;

public enum ControllerModePreference
{
    /// <summary>Use the controller layout while the PC is in Xbox mode.</summary>
    Auto,
    Always,
    Never,
}

/// <summary>Colour theme of the normal desktop window.</summary>
public enum DesktopThemePreference
{
    /// <summary>Follow the Windows app mode.</summary>
    System,
    Dark,
    Light,
}

/// <summary>Colour theme of the controller (Xbox mode) layout.</summary>
public enum ControllerThemePreference
{
    /// <summary>The current Xbox app look: charcoal with a light-grey accent.</summary>
    Dark,

    /// <summary>The Xbox console's light look: grey background with white cards.</summary>
    Light,

    /// <summary>The original dark-green Xbox look.</summary>
    Classic,
}

public sealed record AppSettings
{
    /// <summary>Profile applied when the PC enters Xbox mode.</summary>
    public Guid? XboxEnterProfileId { get; init; }

    /// <summary>Profile applied when the PC leaves Xbox mode.</summary>
    public Guid? XboxExitProfileId { get; init; }

    public bool StartWithWindows { get; init; }

    /// <summary>When the app switches to its large, controller-friendly look.</summary>
    public ControllerModePreference ControllerMode { get; init; } = ControllerModePreference.Auto;

    public DesktopThemePreference DesktopTheme { get; init; } = DesktopThemePreference.System;

    public ControllerThemePreference ControllerTheme { get; init; } = ControllerThemePreference.Dark;

    /// <summary>Seconds the "keep these settings?" prompt waits before reverting.</summary>
    public int RevertTimeoutSeconds { get; init; } = 15;

    public Guid? LastAppliedProfileId { get; init; }
}

public sealed record ProfileData
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public AppSettings Settings { get; init; } = new();

    public IReadOnlyList<Profile> Profiles { get; init; } = [];
}
