using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Core.Display;

public sealed record PlannedPlacement(string AdapterName, MonitorConfig Config);

/// <summary>A display that should show the same picture as another one (identified by hardware key).</summary>
public sealed record PlannedDuplicate(string Key, string Name, string ParentKey, string ParentName);

/// <summary>What to do to the desktop: place these monitors (primary first), detach those, then mirror the duplicates.</summary>
public sealed record ApplyPlan(
    IReadOnlyList<PlannedPlacement> Place,
    IReadOnlyList<string> DetachAdapters,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Displays to show the same picture as another display, set up after the rest of the layout is in place.</summary>
    public IReadOnlyList<PlannedDuplicate> Duplicates { get; init; } = [];
}

public static class ApplyPlanner
{
    /// <summary>
    /// Turns a desired layout into concrete operations for the monitors that are really connected.
    /// The desired layout is authoritative: any connected, attached monitor it does not enable is detached.
    /// </summary>
    public static ApplyPlan Plan(IReadOnlyList<MonitorConfig> desired, IReadOnlyList<DisplayInfo> connected)
    {
        var warnings = new List<string>();

        // A monitor Windows hasn't given an adapter (see Win32DisplayManager) can't be placed.
        connected = [.. connected.Where(d => d.AdapterName.Length > 0)];
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

        // A duplicate of a display that isn't switched on or connected can't be mirrored, so it is skipped.
        foreach (var orphan in present.Where(c => c.IsDuplicate && !present.Any(p => !p.IsDuplicate && p.Key == c.DuplicateOf)).ToList())
        {
            present.Remove(orphan);
            warnings.Add($"{orphan.Name} was skipped: the display it duplicates isn't connected or switched on.");
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
            .Where(c => !c.IsDuplicate)
            .OrderByDescending(c => c.IsPrimary)
            .Select(c => new PlannedPlacement(byKey[c.Key].AdapterName, c))
            .ToList();

        // Duplicates are detached while the rest is placed (so they can't disturb it) and mirrored afterwards.
        var keep = normalized.Where(c => !c.IsDuplicate).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var detach = connected.Where(d => d.Attached && !keep.Contains(d.Key)).Select(d => d.AdapterName).ToList();

        var names = normalized.ToDictionary(c => c.Key, c => c.Name, StringComparer.Ordinal);
        var duplicates = normalized
            .Where(c => c.IsDuplicate)
            .Select(c => new PlannedDuplicate(c.Key, c.Name, c.DuplicateOf!, names[c.DuplicateOf!]))
            .ToList();

        return new ApplyPlan(place, detach, warnings) { Duplicates = duplicates };
    }
}
