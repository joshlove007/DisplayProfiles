using MonitorToggle.Core.Display;
using MonitorToggle.Core.Model;

namespace MonitorToggle.Core.Services;

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

    public void Reload() => Data = store.Load();

    public void Update(Func<ProfileData, ProfileData> change)
    {
        // Re-read first so a CLI invocation never overwrites edits made by the GUI process.
        Data = change(store.Load());
        store.Save(Data);
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
            });
        }

        int nextX = layout.Count == 0 ? 0 : layout.Max(m => m.Right);
        foreach (var d in connected.Where(d => !d.Attached))
        {
            // A disabled monitor still needs a sensible size so it can be switched on from the editor.
            var best = Displays.GetModes(d.AdapterName).Cast<DisplayMode?>().FirstOrDefault();
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
            plan = ApplyPlanner.Plan(layout, Displays.GetDisplays());
        }
        catch (DisplayApplyException ex)
        {
            return new ApplyResult(false, ex.Message, [], previous);
        }

        try
        {
            Displays.Apply(plan);
            if (!WaitUntilApplied(plan, out var missing))
            {
                // A sleeping TV sometimes ignores the first request; Windows still reports success.
                Displays.Apply(plan);
                if (!WaitUntilApplied(plan, out missing))
                {
                    throw new DisplayApplyException($"Windows did not turn on {string.Join(", ", missing)}.");
                }
            }

            return new ApplyResult(true, null, plan.Warnings, previous);
        }
        catch (DisplayApplyException ex)
        {
            string error = ex.Message;
            try
            {
                Displays.Apply(ApplyPlanner.Plan(previous, Displays.GetDisplays()));
                error += " Your previous display settings were restored.";
            }
            catch (DisplayApplyException revertEx)
            {
                error += $" Restoring the previous settings also failed: {revertEx.Message}";
            }

            return new ApplyResult(false, error, plan.Warnings, previous);
        }
    }

    /// <summary>How long to let Windows settle before checking that the monitors really came up.</summary>
    public TimeSpan SettleDelay { get; init; } = TimeSpan.FromMilliseconds(1500);

    private bool WaitUntilApplied(ApplyPlan plan, out List<string> missing)
    {
        missing = [];
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (SettleDelay > TimeSpan.Zero)
            {
                Thread.Sleep(SettleDelay);
            }

            var now = Displays.GetDisplays();
            missing = plan.Place
                .Where(p => !now.Any(d => d.AdapterName == p.AdapterName && d.Attached))
                .Select(p => p.Config.Name)
                .ToList();
            if (missing.Count == 0)
            {
                return true;
            }
        }

        return false;
    }
}
