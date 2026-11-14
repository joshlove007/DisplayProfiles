namespace DisplayProfiles.Core.Model;

public sealed record AppSettings
{
    /// <summary>Profile applied when the PC enters Xbox mode.</summary>
    public Guid? XboxEnterProfileId { get; init; }

    /// <summary>Profile applied when the PC leaves Xbox mode.</summary>
    public Guid? XboxExitProfileId { get; init; }

    public bool StartWithWindows { get; init; }

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
