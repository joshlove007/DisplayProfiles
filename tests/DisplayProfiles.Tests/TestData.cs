using DisplayProfiles.Core.Display;
using DisplayProfiles.Core.Model;
using DisplayProfiles.Core.Services;

namespace DisplayProfiles.Tests;

internal static class TestData
{
    public static MonitorConfig Mon(string key, int x, int y, int w = 2560, int h = 1440, bool primary = false, bool enabled = true) =>
        new() { Key = key, Name = key, Enabled = enabled, IsPrimary = primary, X = x, Y = y, Width = w, Height = h, RefreshHz = 60 };

    /// <summary>A monitor that mirrors another: same place and size as it.</summary>
    public static MonitorConfig Dup(string key, string of, int x = 0, int y = 0, int w = 2560, int h = 1440) =>
        Mon(key, x, y, w, h) with { DuplicateOf = of };

    public static DisplayInfo Disp(string key, string adapter, bool attached, bool primary = false, int x = 0, int w = 2560, int h = 1440) =>
        new() { Key = key, AdapterName = adapter, FriendlyName = key, Attached = attached, IsPrimary = primary, X = x, Width = attached ? w : 0, Height = attached ? h : 0, RefreshHz = attached ? 60 : 0 };
}

internal sealed class FakeDisplayManager(List<DisplayInfo> displays) : IDisplayManager
{
    public List<ApplyPlan> Applied { get; } = [];

    /// <summary>When set, the Nth call to Apply (1-based) throws.</summary>
    public int FailOnCall { get; set; }

    /// <summary>Simulates Windows reporting success but not applying the first N requests.</summary>
    public int SilentlyIgnoreFirstCalls { get; set; }

    public IReadOnlyList<DisplayInfo> GetDisplays() => displays;

    public IReadOnlyList<DisplayMode> GetModes(DisplayInfo display) => [new DisplayMode(3840, 2160, 60)];

    public int BreakDuplicatesCalls { get; private set; }

    public void BreakDuplicates()
    {
        BreakDuplicatesCalls++;
        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i].AdapterName.Length == 0)
            {
                // Extending the desktop again makes Windows list a hidden monitor properly.
                displays[i] = displays[i] with { AdapterName = $@"\\.\DISPLAY{i + 10}" };
            }

            if (displays[i].DuplicateOf is not null)
            {
                displays[i] = displays[i] with { DuplicateOf = null, Attached = false, Width = 0, Height = 0, RefreshHz = 0 };
            }
        }
    }

    public void Apply(ApplyPlan plan)
    {
        Applied.Add(plan);
        if (Applied.Count == FailOnCall)
        {
            throw new DisplayApplyException("boom");
        }

        if (Applied.Count <= SilentlyIgnoreFirstCalls)
        {
            return;
        }

        // Mirror the plan into the fake desktop so later queries see the result.
        for (int i = 0; i < displays.Count; i++)
        {
            var d = displays[i];
            var placement = plan.Place.FirstOrDefault(p => p.AdapterName == d.AdapterName);
            if (placement is not null)
            {
                var c = placement.Config;
                displays[i] = d with { Attached = true, IsPrimary = c.IsPrimary, X = c.X, Width = c.Width, Height = c.Height, RefreshHz = c.RefreshHz, DuplicateOf = null };
            }
            else if (plan.DetachAdapters.Contains(d.AdapterName))
            {
                displays[i] = d with { Attached = false, IsPrimary = false, X = 0, Width = 0, Height = 0, RefreshHz = 0, DuplicateOf = null };
            }
        }

        foreach (var duplicate in plan.Duplicates)
        {
            int index = displays.FindIndex(d => d.Key == duplicate.Key);
            var parent = displays.First(d => d.Key == duplicate.ParentKey);
            displays[index] = displays[index] with
            {
                Attached = true,
                IsPrimary = false,
                X = parent.X,
                Y = parent.Y,
                Width = parent.Width,
                Height = parent.Height,
                RefreshHz = parent.RefreshHz,
                DuplicateOf = duplicate.ParentKey,
            };
        }
    }
}
