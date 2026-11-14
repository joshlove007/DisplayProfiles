namespace DisplayProfiles.Core.Model;

public enum ControllerModePreference
{
    /// <summary>Use the controller layout while the PC is in Xbox mode.</summary>
    Auto,
    Always,
    Never,
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
