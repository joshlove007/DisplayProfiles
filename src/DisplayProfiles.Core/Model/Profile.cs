namespace DisplayProfiles.Core.Model;

public sealed record Profile
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<MonitorConfig> Monitors { get; init; } = [];

    public static Profile Create(string name, IReadOnlyList<MonitorConfig> monitors) =>
        new() { Id = Guid.NewGuid(), Name = name, Monitors = monitors };

    /// <summary>
    /// True when both profiles hold the same settings: the same name and the same displays with the same settings, in any
    /// order. (The record's own equality would compare the monitor lists by reference.)
    /// </summary>
    public bool HasSameContentAs(Profile other) =>
        Id == other.Id &&
        string.Equals(Name, other.Name, StringComparison.Ordinal) &&
        Monitors.Count == other.Monitors.Count &&
        Monitors.OrderBy(m => m.Key, StringComparer.Ordinal)
            .SequenceEqual(other.Monitors.OrderBy(m => m.Key, StringComparer.Ordinal));
}
