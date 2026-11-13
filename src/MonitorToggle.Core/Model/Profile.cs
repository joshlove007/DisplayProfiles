namespace MonitorToggle.Core.Model;

public sealed record Profile
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<MonitorConfig> Monitors { get; init; } = [];

    public static Profile Create(string name, IReadOnlyList<MonitorConfig> monitors) =>
        new() { Id = Guid.NewGuid(), Name = name, Monitors = monitors };
}
