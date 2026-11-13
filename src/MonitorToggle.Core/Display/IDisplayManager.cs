using MonitorToggle.Core.Model;

namespace MonitorToggle.Core.Display;

public interface IDisplayManager
{
    /// <summary>All connected monitors, attached to the desktop or not.</summary>
    IReadOnlyList<DisplayInfo> GetDisplays();

    /// <summary>Modes the monitor supports (works for monitors that are currently disabled).</summary>
    IReadOnlyList<DisplayMode> GetModes(string adapterName);

    /// <summary>Applies the plan atomically; throws <see cref="DisplayApplyException"/> if Windows rejects it.</summary>
    void Apply(ApplyPlan plan);
}
