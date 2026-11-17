using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Core.Display;

public interface IDisplayManager
{
    /// <summary>All connected monitors, attached to the desktop or not.</summary>
    IReadOnlyList<DisplayInfo> GetDisplays();

    /// <summary>Modes the monitor supports (works for monitors that are currently disabled).</summary>
    IReadOnlyList<DisplayMode> GetModes(string adapterName);

    /// <summary>Applies the plan atomically; throws <see cref="DisplayApplyException"/> if Windows rejects it.</summary>
    void Apply(ApplyPlan plan);

    /// <summary>
    /// Turns every duplicated (mirrored) display back into its own part of the extended desktop. Windows lists a
    /// mirrored display under the one it mirrors, so this has to happen before a layout is placed.
    /// </summary>
    void BreakDuplicates();
}
