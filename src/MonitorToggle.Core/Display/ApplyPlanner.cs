using MonitorToggle.Core.Model;

namespace MonitorToggle.Core.Display;

public sealed record PlannedPlacement(string AdapterName, MonitorConfig Config);

/// <summary>What to do to the desktop: place these monitors (primary first), then detach those.</summary>
public sealed record ApplyPlan(
    IReadOnlyList<PlannedPlacement> Place,
    IReadOnlyList<string> DetachAdapters,
    IReadOnlyList<string> Warnings);

public static class ApplyPlanner
{
    /// <summary>
    /// Turns a desired layout into concrete operations for the monitors that are really connected.
    /// The desired layout is authoritative: any connected, attached monitor it does not enable is detached.
    /// </summary>
    public static ApplyPlan Plan(IReadOnlyList<MonitorConfig> desired, IReadOnlyList<DisplayInfo> connected)
    {
        var warnings = new List<string>();
        var byKey = connected.ToDictionary(d => d.Key, StringComparer.Ordinal);

        var present = new List<MonitorConfig>();
        foreach (var config in desired.Where(c => c.Enabled))
        {
            if (byKey.ContainsKey(config.Key))
            {
                present.Add(config);
            }
            else
            {
                warnings.Add($"{config.Name} is not connected and was skipped.");
            }
        }

        if (present.Count == 0)
        {
            throw new DisplayApplyException("None of the monitors in this profile are connected, so nothing was changed.");
        }

        if (!present.Any(c => c.IsPrimary))
        {
            warnings.Add($"The main display of this profile is not connected; {present[0].Name} is the main display instead.");
        }

        var normalized = LayoutMath.Normalize(present);

        var issues = LayoutMath.Validate(normalized);
        var blocking = issues.Where(i => i.Kind != LayoutIssueKind.Disconnected).ToList();
        if (blocking.Count > 0)
        {
            throw new DisplayApplyException(string.Join(" ", blocking.Select(i => i.Message)));
        }

        warnings.AddRange(issues.Where(i => i.Kind == LayoutIssueKind.Disconnected).Select(i => i.Message));

        var place = normalized
            .OrderByDescending(c => c.IsPrimary)
            .Select(c => new PlannedPlacement(byKey[c.Key].AdapterName, c))
            .ToList();

        var keep = normalized.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var detach = connected.Where(d => d.Attached && !keep.Contains(d.Key)).Select(d => d.AdapterName).ToList();

        return new ApplyPlan(place, detach, warnings);
    }
}
