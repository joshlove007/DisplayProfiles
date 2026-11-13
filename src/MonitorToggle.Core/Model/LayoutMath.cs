namespace MonitorToggle.Core.Model;

public enum LayoutIssueKind
{
    NoMonitors,
    PrimaryCount,
    NoResolution,
    Overlap,
    Disconnected,
}

public sealed record LayoutIssue(LayoutIssueKind Kind, string Message);

/// <summary>Pure geometry helpers shared by the apply engine, the GUI layout editor and the tests.</summary>
public static class LayoutMath
{
    /// <summary>
    /// Makes a layout valid for Windows: exactly one primary among the enabled monitors, and the
    /// primary at (0,0) with every other enabled monitor shifted accordingly.
    /// </summary>
    public static IReadOnlyList<MonitorConfig> Normalize(IReadOnlyList<MonitorConfig> monitors)
    {
        var enabled = monitors.Where(m => m.Enabled).ToList();
        if (enabled.Count == 0)
        {
            return monitors.Select(m => m with { IsPrimary = false }).ToList();
        }

        var primary = enabled.FirstOrDefault(m => m.IsPrimary) ?? enabled[0];
        int dx = primary.X;
        int dy = primary.Y;

        return monitors.Select(m => m.Enabled
            ? m with { IsPrimary = m.Key == primary.Key, X = m.X - dx, Y = m.Y - dy }
            : m with { IsPrimary = false }).ToList();
    }

    public static bool Overlaps(MonitorConfig a, MonitorConfig b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    /// <summary>True when two rectangles share part of an edge (Windows needs every monitor to touch another).</summary>
    public static bool Touches(MonitorConfig a, MonitorConfig b)
    {
        bool verticalEdge = (a.Right == b.X || b.Right == a.X) && a.Y < b.Bottom && b.Y < a.Bottom;
        bool horizontalEdge = (a.Bottom == b.Y || b.Bottom == a.Y) && a.X < b.Right && b.X < a.Right;
        return verticalEdge || horizontalEdge;
    }

    /// <summary>Returns problems that would make Windows reject or rewrite the layout.</summary>
    public static IReadOnlyList<LayoutIssue> Validate(IReadOnlyList<MonitorConfig> monitors)
    {
        var problems = new List<LayoutIssue>();
        var enabled = monitors.Where(m => m.Enabled).ToList();

        if (enabled.Count == 0)
        {
            problems.Add(new(LayoutIssueKind.NoMonitors, "At least one monitor must be enabled."));
            return problems;
        }

        if (enabled.Count(m => m.IsPrimary) != 1)
        {
            problems.Add(new(LayoutIssueKind.PrimaryCount, "Exactly one enabled monitor must be the main display."));
        }

        foreach (var m in enabled.Where(m => m.Width <= 0 || m.Height <= 0))
        {
            problems.Add(new(LayoutIssueKind.NoResolution, $"{m.Name} has no resolution set."));
        }

        for (int i = 0; i < enabled.Count; i++)
        {
            for (int j = i + 1; j < enabled.Count; j++)
            {
                if (Overlaps(enabled[i], enabled[j]))
                {
                    problems.Add(new(LayoutIssueKind.Overlap, $"{enabled[i].Name} and {enabled[j].Name} overlap."));
                }
            }
        }

        if (enabled.Count > 1 && !IsConnected(enabled))
        {
            problems.Add(new(LayoutIssueKind.Disconnected, "Every monitor must touch at least one other monitor."));
        }

        return problems;
    }

    private static bool IsConnected(List<MonitorConfig> enabled)
    {
        var seen = new HashSet<int> { 0 };
        var queue = new Queue<int>([0]);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            for (int i = 0; i < enabled.Count; i++)
            {
                if (!seen.Contains(i) && Touches(enabled[current], enabled[i]))
                {
                    seen.Add(i);
                    queue.Enqueue(i);
                }
            }
        }

        return seen.Count == enabled.Count;
    }

    /// <summary>
    /// Snaps the moving monitor's edges to nearby edges of the other monitors (like the Windows
    /// Display settings page) and then pushes it out of any overlap.
    /// </summary>
    public static (int X, int Y) Snap(MonitorConfig moving, int proposedX, int proposedY, IEnumerable<MonitorConfig> others, int threshold)
    {
        var candidates = others.Where(o => o.Enabled && o.Key != moving.Key).ToList();
        int x = proposedX;
        int y = proposedY;
        int bestDx = int.MaxValue;
        int bestDy = int.MaxValue;

        foreach (var o in candidates)
        {
            foreach (int dx in new[] { o.Right - proposedX, o.X - (proposedX + moving.Width), o.X - proposedX, o.Right - (proposedX + moving.Width) })
            {
                if (Math.Abs(dx) <= threshold && Math.Abs(dx) < Math.Abs(bestDx))
                {
                    bestDx = dx;
                }
            }

            foreach (int dy in new[] { o.Bottom - proposedY, o.Y - (proposedY + moving.Height), o.Y - proposedY, o.Bottom - (proposedY + moving.Height) })
            {
                if (Math.Abs(dy) <= threshold && Math.Abs(dy) < Math.Abs(bestDy))
                {
                    bestDy = dy;
                }
            }
        }

        if (bestDx != int.MaxValue)
        {
            x += bestDx;
        }

        if (bestDy != int.MaxValue)
        {
            y += bestDy;
        }

        return ResolveOverlap(moving, x, y, candidates);
    }

    /// <summary>Moves the rectangle out of any overlap along the axis that needs the smallest push.</summary>
    public static (int X, int Y) ResolveOverlap(MonitorConfig moving, int x, int y, IEnumerable<MonitorConfig> others)
    {
        foreach (var o in others.Where(o => o.Enabled && o.Key != moving.Key))
        {
            var probe = moving with { X = x, Y = y };
            if (!Overlaps(probe, o))
            {
                continue;
            }

            int pushLeft = o.X - probe.Right;
            int pushRight = o.Right - probe.X;
            int pushUp = o.Y - probe.Bottom;
            int pushDown = o.Bottom - probe.Y;

            int bestX = Math.Abs(pushLeft) < Math.Abs(pushRight) ? pushLeft : pushRight;
            int bestY = Math.Abs(pushUp) < Math.Abs(pushDown) ? pushUp : pushDown;

            if (Math.Abs(bestX) <= Math.Abs(bestY))
            {
                x += bestX;
            }
            else
            {
                y += bestY;
            }
        }

        return (x, y);
    }

    /// <summary>Do two layouts describe the same desktop? Used to detect which profile is currently active.</summary>
    public static bool AreEquivalent(IReadOnlyList<MonitorConfig> a, IReadOnlyList<MonitorConfig> b)
    {
        var left = Normalize(a).Where(m => m.Enabled).OrderBy(m => m.Key, StringComparer.Ordinal).ToList();
        var right = Normalize(b).Where(m => m.Enabled).OrderBy(m => m.Key, StringComparer.Ordinal).ToList();

        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            var l = left[i];
            var r = right[i];
            bool sameRefresh = l.RefreshHz == 0 || r.RefreshHz == 0 || l.RefreshHz == r.RefreshHz;
            if (l.Key != r.Key || l.IsPrimary != r.IsPrimary || l.X != r.X || l.Y != r.Y ||
                l.Width != r.Width || l.Height != r.Height || l.Orientation != r.Orientation || !sameRefresh)
            {
                return false;
            }
        }

        return true;
    }
}
