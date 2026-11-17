using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DisplayProfiles.Core.Display;
using DisplayProfiles.Core.Model;

namespace DisplayProfiles.App.ViewModels;

/// <summary>Editable state of one monitor inside the profile being edited.</summary>
public sealed partial class MonitorViewModel : ObservableObject
{
    private static readonly string[] OrientationNames = ["Landscape", "Portrait", "Landscape (flipped)", "Portrait (flipped)"];

    private readonly List<DisplayMode> modes;
    private readonly List<DisplayMode> supported;
    private List<(int Width, int Height)> resolutions = [];
    private bool loading = true;

    public MonitorViewModel(MonitorConfig config, int number, bool isConnected, IReadOnlyList<DisplayMode> supportedModes)
    {
        Key = config.Key;
        Name = config.Name;
        Number = number;
        IsConnected = isConnected;
        modes = [.. supportedModes];
        supported = [.. supportedModes];

        // A monitor that isn't connected right now still shows the mode saved in the profile.
        if (config.Width > 0 && !modes.Contains(new DisplayMode(config.Width, config.Height, config.RefreshHz)))
        {
            modes.Add(new DisplayMode(config.Width, config.Height, config.RefreshHz));
        }

        resolutions = [.. modes.Select(m => (m.Width, m.Height)).Distinct().OrderByDescending(r => (long)r.Width * r.Height)];
        ResolutionLabels = [.. resolutions.Select(r => $"{r.Width} × {r.Height}")];

        Enabled = config.Enabled;
        IsPrimary = config.IsPrimary;
        X = config.X;
        Y = config.Y;
        Width = config.Width;
        Height = config.Height;
        RefreshHz = config.RefreshHz;
        OrientationIndex = (int)config.Orientation;
        DuplicateOf = config.DuplicateOf;
        ResolutionIndex = resolutions.FindIndex(r => r == (config.Width, config.Height));
        RebuildRefreshRates();

        loading = false;
    }

    public string Key { get; }

    public string Name { get; }

    /// <summary>Position in the editor's list (1, 2, 3 ...). Only for display; monitors are matched by <see cref="Key"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial int Number { get; set; }

    public bool IsConnected { get; }

    /// <summary>Only a display that isn't connected can be removed from the profile.</summary>
    public bool CanRemove => !IsConnected;

    public IReadOnlyList<string> ResolutionLabels { get; }

    public IReadOnlyList<string> Orientations => OrientationNames;

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    [ObservableProperty]
    public partial bool IsPrimary { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial int X { get; set; }

    [ObservableProperty]
    public partial int Y { get; set; }

    [ObservableProperty]
    public partial int Width { get; set; }

    [ObservableProperty]
    public partial int Height { get; set; }

    [ObservableProperty]
    public partial int RefreshHz { get; set; }

    [ObservableProperty]
    public partial int ResolutionIndex { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> RefreshLabels { get; set; } = [];

    [ObservableProperty]
    public partial int RefreshIndex { get; set; }

    [ObservableProperty]
    public partial int OrientationIndex { get; set; }

    /// <summary>Key of the display this one mirrors (duplicate mode), or null when it extends the desktop.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDuplicate))]
    [NotifyPropertyChangedFor(nameof(IsExtended))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial string? DuplicateOf { get; set; }

    public bool IsDuplicate => DuplicateOf is not null;

    public bool IsExtended => DuplicateOf is null;

    /// <summary>The number shown for the display this one mirrors, for the summary.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial int DuplicateOfNumber { get; set; }

    /// <summary>The options of the "Multiple displays" setting: extend, or duplicate one of the other displays.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> ModeChoices { get; set; } = [ExtendLabel];

    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    /// <summary>For each entry of <see cref="ModeChoices"/>, the key of the display it duplicates (null = extend).</summary>
    public IReadOnlyList<string?> ModeTargets { get; private set; } = [null];

    public const string ExtendLabel = "Extend desktop to this display";

    /// <summary>The mode, place and size this display had as part of the extended desktop, kept while it is a duplicate.</summary>
    public (int X, int Y, int Width, int Height, int RefreshHz)? OwnLayout { get; set; }
    public string Title => $"{Number}. {Name}";

    public string Summary => !Enabled
        ? (IsConnected ? "Off" : "Off · not connected")
        : IsDuplicate
            ? $"Duplicating display {DuplicateOfNumber}{(IsConnected ? string.Empty : " · not connected")}"
            : $"{Width} × {Height}, {RefreshHz} Hz{(IsPrimary ? " · Main display" : string.Empty)}{(IsConnected ? string.Empty : " · not connected")}";

    public MonitorConfig ToConfig() => new()
    {
        Key = Key,
        Name = Name,
        Enabled = Enabled,
        IsPrimary = Enabled && IsPrimary,
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
        RefreshHz = RefreshHz,
        Orientation = (DisplayOrientation)OrientationIndex,
        DuplicateOf = Enabled ? DuplicateOf : null,
    };

    /// <summary>Replaces the options of the "Multiple displays" setting, keeping <paramref name="selectedKey"/> selected.</summary>
    public void SetModeChoices(IReadOnlyList<string> labels, IReadOnlyList<string?> targets, string? selectedKey)
    {
        ModeChoices = labels;
        ModeTargets = targets;
        ModeIndex = Math.Max(0, targets.ToList().IndexOf(selectedKey));
    }

    /// <summary>
    /// A display that was a duplicate has its original's mode, which it may not support. Switches to its best mode then.
    /// </summary>
    public void EnsureSupportedMode()
    {
        if (!IsConnected || supported.Count == 0 || supported.Contains(new DisplayMode(Width, Height, RefreshHz)))
        {
            return;
        }

        var best = supported.OrderByDescending(m => (long)m.Width * m.Height).ThenByDescending(m => m.RefreshHz).First();
        SetMode(best.Width, best.Height, best.RefreshHz);
    }

    /// <summary>Sets the mode of the display (for example when it stops being a duplicate), keeping the pickers in step.</summary>
    public void SetMode(int width, int height, int refreshHz)
    {
        bool previous = loading;
        loading = true;
        Width = width;
        Height = height;
        RefreshHz = refreshHz;
        ResolutionIndex = resolutions.FindIndex(r => r == (width, height));
        RebuildRefreshRates();
        loading = previous;
    }

    /// <summary>Moves the monitor without raising per-property edits from the editor's point of view.</summary>
    public void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    partial void OnResolutionIndexChanged(int value)
    {
        if (loading || value < 0 || value >= resolutions.Count)
        {
            return;
        }

        var (width, height) = resolutions[value];
        Width = width;
        Height = height;
        RebuildRefreshRates();
    }

    partial void OnRefreshIndexChanged(int value)
    {
        if (loading || value < 0 || value >= RefreshLabels.Count)
        {
            return;
        }

        RefreshHz = ParseHz(RefreshLabels[value]);
    }

    partial void OnOrientationIndexChanged(int oldValue, int newValue)
    {
        if (loading || newValue < 0)
        {
            return;
        }

        // Rotating a landscape monitor to portrait (or back) swaps the desktop size.
        bool wasSideways = oldValue is 1 or 3;
        bool isSideways = newValue is 1 or 3;
        if (wasSideways != isSideways)
        {
            (Width, Height) = (Height, Width);
        }
    }

    partial void OnEnabledChanged(bool value) => OnPropertyChanged(nameof(Summary));

    partial void OnIsPrimaryChanged(bool value) => OnPropertyChanged(nameof(Summary));

    partial void OnWidthChanged(int value) => OnPropertyChanged(nameof(Summary));

    partial void OnHeightChanged(int value) => OnPropertyChanged(nameof(Summary));

    partial void OnRefreshHzChanged(int value) => OnPropertyChanged(nameof(Summary));

    private void RebuildRefreshRates()
    {
        var current = ResolutionIndex >= 0 && ResolutionIndex < resolutions.Count ? resolutions[ResolutionIndex] : (Width, Height);
        var rates = modes.Where(m => (m.Width, m.Height) == current).Select(m => m.RefreshHz).Distinct().OrderByDescending(r => r).ToList();

        bool previous = loading;
        loading = true;
        RefreshLabels = [.. rates.Select(r => $"{r} Hz")];
        int index = rates.IndexOf(RefreshHz);
        if (index < 0 && rates.Count > 0)
        {
            index = 0;
            RefreshHz = rates[0];
        }

        RefreshIndex = index;
        loading = previous;
    }

    private static int ParseHz(string label) => int.TryParse(label.Split(' ')[0], out int hz) ? hz : 0;
}
