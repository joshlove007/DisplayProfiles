using DisplayProfiles.Core.Display;
using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Core.Services;

public sealed record ApplyResult(
    bool Success,
    string? Error,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<MonitorConfig> PreviousLayout);

/// <summary>Profile management and applying, shared by the CLI, tray, GUI and Game Bar helper.</summary>
public sealed class ProfileService(IDisplayManager displays, ProfileStore store)
{
    public IDisplayManager Displays { get; } = displays;

    public ProfileData Data { get; private set; } = store.Load();

    /// <summary>Raised after profiles or settings were saved. Used to keep the Game Bar widget's snapshot current.</summary>
    public event Action? Changed;

    public void Reload() => Data = store.Load();

    /// <summary>
    /// First run only (no profiles file yet): creates starting profiles from the legacy toggle script if it is
    /// found, otherwise a single profile capturing the current layout.
    /// </summary>
    public bool SeedIfFirstRun(string legacyFolder)
    {
        if (File.Exists(store.FilePath))
        {
            return false;
        }

        var live = CaptureLayout();
        var imported = Integration.LegacyImporter.TryImport(legacyFolder, live);
        IReadOnlyList<Profile> seeded = imported.Count > 0 ? imported : [Profile.Create("Current layout", live)];
        Update(d => d with { Profiles = seeded });
        return true;
    }

    public void Update(Func<ProfileData, ProfileData> change)
    {
        // Re-read first so a CLI invocation never overwrites edits made by the GUI process.
        Data = change(store.Load());
        store.Save(Data);
        Changed?.Invoke();
    }

    /// <summary>Current desktop as a layout, including connected monitors that are switched off.</summary>
    public IReadOnlyList<MonitorConfig> CaptureLayout()
    {
        var connected = Displays.GetDisplays();
        var layout = new List<MonitorConfig>();

        foreach (var d in connected.Where(d => d.Attached))
        {
            layout.Add(new MonitorConfig
            {
                Key = d.Key,
                Name = d.FriendlyName,
                Enabled = true,
                IsPrimary = d.IsPrimary,
                X = d.X,
                Y = d.Y,
                Width = d.Width,
                Height = d.Height,
                RefreshHz = d.RefreshHz,
                Orientation = d.Orientation,
                DuplicateOf = d.DuplicateOf,
            });
        }

        int nextX = layout.Count == 0 ? 0 : layout.Max(m => m.Right);
        foreach (var d in connected.Where(d => !d.Attached))
        {
            // A disabled monitor still needs a sensible size so it can be switched on from the editor.
            var best = Displays.GetModes(d).Cast<DisplayMode?>().FirstOrDefault();
            layout.Add(new MonitorConfig
            {
                Key = d.Key,
                Name = d.FriendlyName,
                Enabled = false,
                X = nextX,
                Y = 0,
                Width = best?.Width ?? 1920,
                Height = best?.Height ?? 1080,
                RefreshHz = best?.RefreshHz ?? 60,
            });
        }

        return LayoutMath.Normalize(layout);
    }

    public Profile? FindActiveProfile()
    {
        var current = CaptureLayout();
        return Data.Profiles.FirstOrDefault(p => LayoutMath.AreEquivalent(p.Monitors, current));
    }

    public Profile? Resolve(string nameOrId) => ProfileCycler.Resolve(Data.Profiles, nameOrId);

    public Profile? NextProfile()
    {
        var currentId = FindActiveProfile()?.Id ?? Data.Settings.LastAppliedProfileId;
        return ProfileCycler.Next(Data.Profiles, currentId);
    }

    public ApplyResult Apply(Profile profile)
    {
        var result = ApplyLayout(profile.Monitors);
        if (result.Success)
        {
            Update(d => d with { Settings = d.Settings with { LastAppliedProfileId = profile.Id } });
        }

        return result;
    }

    /// <summary>Applies a layout; if Windows rejects it, the previous layout is restored automatically.</summary>
    public ApplyResult ApplyLayout(IReadOnlyList<MonitorConfig> layout)
    {
        var previous = CaptureLayout();

        ApplyPlan plan;
        try
        {
            plan = PlanFresh(layout);
        }
        catch (DisplayApplyException ex)
        {
            return new ApplyResult(false, ex.Message, [], previous);
        }

        try
        {
            // A sleeping TV can ignore the first requests while Windows still reports success, so retry a few times.
            string problem = string.Empty;
            for (int attempt = 0; attempt < MaxApplyAttempts; attempt++)
            {
                Displays.Apply(plan);
                if (WaitUntilApplied(plan, out problem))
                {
                    return new ApplyResult(true, null, plan.Warnings, previous);
                }
            }

            throw new DisplayApplyException(problem);
        }
        catch (DisplayApplyException ex)
        {
            string error = ex.Message;
            try
            {
                Displays.Apply(PlanFresh(previous));
                error += " Your previous display settings were restored.";
            }
            catch (DisplayApplyException revertEx)
            {
                error += $" Restoring the previous settings also failed: {revertEx.Message}";
            }

            return new ApplyResult(false, error, plan.Warnings, previous);
        }
    }

    /// <summary>
    /// Plans a layout for the displays as they are now. Windows lists a duplicated (mirrored) display under the one it
    /// mirrors, so existing duplicates are broken up first: after that every connected display shows up on its own.
    /// The layout is checked before anything is changed.
    /// </summary>
    private ApplyPlan PlanFresh(IReadOnlyList<MonitorConfig> layout)
    {
        var current = Displays.GetDisplays();

        // Windows can leave a connected monitor out of its list (no adapter), and a monitor that mirrors another can drop
        // out too. Both come back when the desktop is extended again, which has to happen before the layout is planned.
        bool hidden = layout.Any(c => c.Enabled && current.Any(d => d.Key == c.Key && d.AdapterName.Length == 0));
        bool missingForMirror = layout.Any(c => c.Enabled && c.IsDuplicate) &&
            layout.Any(c => c.Enabled && current.All(d => d.Key != c.Key));
        bool needsExtend = hidden || missingForMirror || current.Any(d => d.DuplicateOf is not null);

        ApplyPlan? plan = null;
        try
        {
            plan = ApplyPlanner.Plan(layout, current);
        }
        catch (DisplayApplyException) when (needsExtend)
        {
            // Planned again below, once every monitor is listed.
        }

        if (needsExtend)
        {
            Displays.BreakDuplicates();
            plan = ApplyPlanner.Plan(layout, Displays.GetDisplays());
        }

        return plan!;
    }
    /// <summary>How long to let Windows settle before checking that the monitors really came up.</summary>
    public TimeSpan SettleDelay { get; init; } = TimeSpan.FromMilliseconds(1500);

    private const int MaxApplyAttempts = 3;

    // Windows names one display of a mirrored group as the one the others mirror, which isn't necessarily the one a profile named.
    private static string Original(IReadOnlyList<DisplayInfo> displays, string key) =>
        displays.FirstOrDefault(d => d.Key == key) is { } display ? display.DuplicateOf ?? display.Key : key;

    private bool WaitUntilApplied(ApplyPlan plan, out string problem)
    {
        problem = string.Empty;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (SettleDelay > TimeSpan.Zero)
            {
                Thread.Sleep(SettleDelay);
            }

            var now = Displays.GetDisplays();
            var off = plan.Place
                .Where(p => !now.Any(d => d.AdapterName == p.AdapterName && d.Attached))
                .Select(p => p.Config.Name)
                .ToList();
            var notMirrored = plan.Duplicates
                .Where(p => !now.Any(d => d.Key == p.Key && d.Attached) || Original(now, p.Key) != Original(now, p.ParentKey))
                .Select(p => $"{p.Name} on {p.ParentName}")
                .ToList();
            if (off.Count == 0 && notMirrored.Count == 0)
            {
                return true;
            }

            var parts = new List<string>();
            if (off.Count > 0)
            {
                parts.Add($"Windows did not turn on {string.Join(", ", off)}. Check that it is powered on and set to the right input.");
            }

            if (notMirrored.Count > 0)
            {
                parts.Add($"Windows could not duplicate {string.Join(", ", notMirrored)}.");
            }

            problem = string.Join(" ", parts);
        }

        return false;
    }
}