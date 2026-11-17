using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using DisplayProfiles.Core.Display;
using DisplayProfiles.Core.Integration;
using DisplayProfiles.Core.Model;
using DisplayProfiles.Core.Services;

namespace DisplayProfiles.App.ViewModels;

public sealed partial class ProfileItem(Guid id, string name) : ObservableObject
{
    public Guid Id { get; } = id;

    [ObservableProperty]
    public partial string Name { get; set; } = name;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>The profile has edits that haven't been saved yet.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; set; }
}

/// <summary>State and operations behind the profile editor page. Edits are kept in memory until the user saves them.</summary>
public sealed partial class ProfileEditorViewModel : ObservableObject
{
    private readonly ProfileService service;

    // Every monitor of the selected profile. Monitors is the part of it that is listed (see MonitorList.IsVisible).
    private readonly List<MonitorViewModel> all = [];
    private IReadOnlyList<DisplayInfo> displays = [];
    private bool loading;

    public ProfileEditorViewModel(ProfileService service)
    {
        this.service = service;
    }

    public ObservableCollection<ProfileItem> Profiles { get; } = [];

    public ObservableCollection<MonitorViewModel> Monitors { get; } = [];

    [ObservableProperty]
    public partial ProfileItem? SelectedProfile { get; set; }

    [ObservableProperty]
    public partial MonitorViewModel? SelectedMonitor { get; set; }

    [ObservableProperty]
    public partial string ProfileName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsBusy { get; set; }

    public bool IsNotBusy => !IsBusy;

    /// <summary>The selected profile has edits that haven't been saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsDirty { get; set; }

    public bool CanSave => IsDirty && !IsBusy;

    [ObservableProperty]
    public partial bool HasProfile { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool IsStatusOpen { get; set; }

    /// <summary>Raised when the whole monitor list was replaced or a monitor moved/resized, so the canvas redraws.</summary>
    public event Action? LayoutChanged;

    public ProfileService Service => service;

    /// <summary>Reloads everything from disk and drops any unsaved edits.</summary>
    public void Load(Guid? select = null)
    {
        service.Reload();
        displays = service.Displays.GetDisplays();

        loading = true;
        var wanted = select ?? SelectedProfile?.Id;
        Profiles.Clear();
        foreach (var profile in service.Data.Profiles)
        {
            Profiles.Add(new ProfileItem(profile.Id, profile.Name));
        }

        loading = false;
        RefreshActive();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == wanted) ?? Profiles.FirstOrDefault();
        HasProfile = SelectedProfile is not null;
        if (SelectedProfile is null)
        {
            all.Clear();
            Monitors.Clear();
            IsDirty = false;
            LayoutChanged?.Invoke();
        }
    }

    public void RefreshActive()
    {
        var active = service.FindActiveProfile();
        foreach (var item in Profiles)
        {
            item.IsActive = item.Id == active?.Id;
        }
    }

    public void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = message.Length > 0;
    }

    partial void OnSelectedProfileChanged(ProfileItem? value)
    {
        if (loading)
        {
            return;
        }

        HasProfile = value is not null;
        loading = true;
        ProfileName = value?.Name ?? string.Empty;
        loading = false;
        var profile = service.Data.Profiles.FirstOrDefault(p => p.Id == value?.Id);
        LoadMonitors(profile);
    }

    private void LoadMonitors(Profile? profile)
    {
        loading = true;
        foreach (var old in all)
        {
            old.PropertyChanged -= OnMonitorChanged;
        }

        all.Clear();
        Monitors.Clear();
        SelectedMonitor = null;

        if (profile is not null)
        {
            // Monitors connected now but missing from the profile appear switched off, ready to be enabled.
            var configs = profile.Monitors.ToList();
            double right = configs.Where(c => c.Enabled).Select(c => (double)c.Right).DefaultIfEmpty(0).Max();
            foreach (var display in displays.Where(d => configs.All(c => c.Key != d.Key)))
            {
                var best = service.Displays.GetModes(display).Cast<DisplayMode?>().FirstOrDefault();
                configs.Add(new MonitorConfig
                {
                    Key = display.Key,
                    Name = display.FriendlyName,
                    Enabled = false,
                    X = (int)right,
                    Width = best?.Width ?? 1920,
                    Height = best?.Height ?? 1080,
                    RefreshHz = best?.RefreshHz ?? 60,
                });
            }

            foreach (var config in MonitorList.Order(configs, displays))
            {
                var display = displays.FirstOrDefault(d => d.Key == config.Key);
                var modes = display is null ? [] : service.Displays.GetModes(display);
                var monitor = new MonitorViewModel(config, 0, display is not null, modes);
                monitor.PropertyChanged += OnMonitorChanged;
                all.Add(monitor);
            }

            RebuildVisibleList();
            RefreshGroupModes();
            SyncGroups();
            RefreshModeChoices();
            SelectedMonitor = Monitors.FirstOrDefault(m => m.Enabled) ?? Monitors.FirstOrDefault();
            if (SelectedMonitor is not null)
            {
                SelectedMonitor.IsSelected = true;
            }
        }

        MarkClean();
        loading = false;
        LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Fills <see cref="Monitors"/> with the monitors that should be listed and numbers them 1, 2, 3 ... in list order.
    /// The numbers are only labels: nothing is stored or matched by them.
    /// </summary>
    private void RebuildVisibleList()
    {
        bool showDisconnectedOff = service.Data.Settings.ShowDisconnectedDisplays;
        Monitors.Clear();
        foreach (var monitor in all)
        {
            if (MonitorList.IsVisible(monitor.ToConfig(), monitor.IsConnected, showDisconnectedOff))
            {
                Monitors.Add(monitor);
            }
            else
            {
                monitor.Number = 0;
            }
        }

        for (int i = 0; i < Monitors.Count; i++)
        {
            Monitors[i].Number = i + 1;
        }

        RefreshModeChoices();
    }

    private MonitorViewModel RootOf(MonitorViewModel monitor) =>
        monitor.DuplicateOf is { } key && all.FirstOrDefault(m => m.Key == key) is { } root ? root : monitor;

    // The displays that show one picture: the original first, then the displays that duplicate it.
    private List<MonitorViewModel> GroupOf(MonitorViewModel root) =>
        [root, .. all.Where(m => m != root && m.Enabled && m.DuplicateOf == root.Key)];

    private static string Describe(IReadOnlyList<MonitorViewModel> others, string verb)
    {
        var numbers = others.Select(m => m.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        string list = numbers.Count <= 1 ? string.Concat(numbers) : $"{string.Join(", ", numbers.Take(numbers.Count - 1))} and {numbers[^1]}";
        return $"{verb} display {list}";
    }

    /// <summary>
    /// Rebuilds each display's "Display mode" options. A display that is on its own can extend the desktop or duplicate
    /// another display that is on. A display that shares its picture with others says which ones ("Duplicate display 3"),
    /// and can leave the group by extending.
    /// </summary>
    private void RefreshModeChoices()
    {
        bool previous = loading;
        loading = true;
        foreach (var monitor in all)
        {
            var group = GroupOf(RootOf(monitor));
            monitor.InGroup = monitor.Enabled && group.Count > 1 && group.Contains(monitor);

            var labels = new List<string>();
            var targets = new List<string?>();
            if (monitor.InGroup)
            {
                var others = group.Where(m => m != monitor).OrderBy(m => m.Number).ToList();
                labels.Add(Describe(others, "Duplicate"));
                targets.Add(MonitorViewModel.KeepGroup);
                monitor.GroupNote = Describe(others, "Duplicating");
            }
            else
            {
                monitor.GroupNote = string.Empty;
            }

            labels.Add(MonitorViewModel.ExtendLabel);
            targets.Add(null);
            if (!monitor.InGroup && monitor.Enabled)
            {
                foreach (var other in Monitors.Where(m => m != monitor && m.Enabled && !m.IsDuplicate))
                {
                    // Joining a display that is already mirrored joins the whole group, and the label says so.
                    var members = GroupOf(other).Where(m => m != monitor).OrderBy(m => m.Number).ToList();
                    labels.Add(Describe(members, "Duplicate"));
                    targets.Add(other.Key);
                }
            }

            monitor.SetModeChoices(labels, targets, monitor.InGroup ? MonitorViewModel.KeepGroup : null);
        }

        loading = previous;
    }

    /// <summary>
    /// Displays that duplicate each other can only use the resolutions and refresh rates all of them support, as in
    /// Windows' display settings. Displays on their own can use everything they support.
    /// </summary>
    private void RefreshGroupModes()
    {
        bool previous = loading;
        loading = true;
        foreach (var root in all.Where(m => m.DuplicateOf is null))
        {
            var group = GroupOf(root);
            if (group.Count == 1)
            {
                root.LimitModes(null);
                continue;
            }

            var common = group.Select(m => m.SupportedModes).Aggregate((a, b) => [.. a.Where(x => b.Contains(x))]);
            foreach (var member in group)
            {
                member.LimitModes(common);
            }
        }

        loading = previous;
    }

    /// <summary>Gives every duplicate its original's place, mode, orientation and main-display flag.</summary>
    private void SyncGroups()
    {
        bool previous = loading;
        loading = true;
        foreach (var root in all.Where(m => m.DuplicateOf is null))
        {
            foreach (var member in all.Where(m => m.Enabled && m.DuplicateOf == root.Key))
            {
                member.SetPosition(root.X, root.Y);
                member.CopyModeFrom(root);
                member.IsPrimary = root.IsPrimary;
            }
        }

        loading = previous;
    }

    private void RefreshGroups()
    {
        RefreshGroupModes();
        SyncGroups();
        Renormalize();
        RefreshModeChoices();
    }
    /// <summary>
    /// Looks for the connected displays again (for example after a monitor was plugged in, switched on or put to sleep)
    /// and updates the list, keeping edits that aren't saved yet.
    /// </summary>
    public void RefreshDisplays()
    {
        var edited = BuildProfile();
        bool wasDirty = IsDirty;
        displays = service.Displays.GetDisplays();
        var selected = SelectedMonitor?.Key;
        LoadMonitors(edited ?? service.Data.Profiles.FirstOrDefault(p => p.Id == SelectedProfile?.Id));
        if (selected is not null)
        {
            Select(Monitors.FirstOrDefault(m => m.Key == selected));
        }

        if (wasDirty && SelectedProfile is { } item)
        {
            IsDirty = true;
            item.IsDirty = true;
        }

        RefreshActive();
    }

    /// <summary>Applies the "show disconnected displays" setting without touching unsaved edits.</summary>
    public void RefreshVisibility()
    {
        loading = true;
        RebuildVisibleList();
        if (SelectedMonitor is not null && !Monitors.Contains(SelectedMonitor))
        {
            SelectedMonitor = Monitors.FirstOrDefault(m => m.Enabled) ?? Monitors.FirstOrDefault();
        }

        loading = false;
        LayoutChanged?.Invoke();
    }

    /// <summary>The number shown for a connected display, so "Identify" matches the numbers in the editor.</summary>
    public int NumberFor(string key) => Monitors.FirstOrDefault(m => m.Key == key)?.Number ?? 0;

    public void Select(MonitorViewModel? monitor)
    {
        foreach (var m in all)
        {
            m.IsSelected = m == monitor;
        }

        SelectedMonitor = monitor;
    }

    private void OnMonitorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (loading || sender is not MonitorViewModel monitor)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(MonitorViewModel.Enabled):
                OnEnabledChanged(monitor);
                break;
            case nameof(MonitorViewModel.IsPrimary) when monitor.IsPrimary:
                MakePrimary(monitor);
                break;
            case nameof(MonitorViewModel.IsPrimary):
                // Can't un-main the main display; pick another one instead. A duplicate shows its original's flag.
                loading = true;
                var original = RootOf(monitor);
                if (monitor != original)
                {
                    monitor.IsPrimary = original.IsPrimary;
                }
                else if (monitor.Enabled && all.All(m => !m.IsPrimary || m.IsDuplicate || m == monitor))
                {
                    monitor.IsPrimary = true;
                }

                SyncGroups();
                loading = false;
                return;
            case nameof(MonitorViewModel.ModeIndex):
                OnModeChanged(monitor);
                break;
            case nameof(MonitorViewModel.X) or nameof(MonitorViewModel.Y) or nameof(MonitorViewModel.Width) or nameof(MonitorViewModel.Height):
            case nameof(MonitorViewModel.RefreshHz) or nameof(MonitorViewModel.OrientationIndex):
                ShareSettings(monitor);
                break;
            default:
                return;
        }

        MarkDirty();
        LayoutChanged?.Invoke();
    }

    private void OnEnabledChanged(MonitorViewModel monitor)
    {
        loading = true;

        // A switched-off display can't be duplicated, and one that is switched on starts out as part of the extended desktop.
        LeaveGroup(monitor);
        monitor.DuplicateOf = null;
        if (monitor.Enabled)
        {
            // Put a newly enabled monitor to the right of the others.
            var others = all.Where(m => m.Enabled && m != monitor).ToList();
            if (others.Count > 0)
            {
                monitor.SetPosition(others.Max(m => m.X + m.Width), others.First(m => m.IsPrimary).Y);
            }
            else
            {
                monitor.IsPrimary = true;
            }
        }
        else if (!all.Any(m => m.Enabled))
        {
            monitor.Enabled = true;
            ShowStatus("At least one display has to stay on.", InfoBarSeverity.Warning);
        }
        else if (monitor.IsPrimary)
        {
            monitor.IsPrimary = false;
            var next = all.First(m => m.Enabled);
            next.IsPrimary = true;
        }

        if (!all.Any(m => m.Enabled && m.IsPrimary) && all.FirstOrDefault(m => m.Enabled && !m.IsDuplicate) is { } first)
        {
            first.IsPrimary = true;
        }

        RefreshGroups();
        loading = false;
    }

    /// <summary>The user picked "Extend" or "Duplicate display N" for a display.</summary>
    private void OnModeChanged(MonitorViewModel monitor)
    {
        if (monitor.ModeIndex < 0 || monitor.ModeIndex >= monitor.ModeTargets.Count)
        {
            return;
        }

        string? target = monitor.ModeTargets[monitor.ModeIndex];
        if (target == MonitorViewModel.KeepGroup || (target is null && !monitor.InGroup))
        {
            return;
        }

        loading = true;
        if (target is null)
        {
            LeaveGroup(monitor);
        }
        else if (all.FirstOrDefault(m => m.Key == target) is { } chosen)
        {
            // Remember the place and mode it had on the extended desktop, so "Extend" can bring them back.
            var original = RootOf(chosen);
            monitor.OwnLayout ??= (monitor.X, monitor.Y, monitor.Width, monitor.Height, monitor.RefreshHz);
            monitor.DuplicateOf = original.Key;
            if (monitor.IsPrimary)
            {
                // The main display of the group is the original one.
                monitor.IsPrimary = false;
                original.IsPrimary = true;
            }
        }

        RefreshGroups();
        loading = false;
    }

    /// <summary>
    /// Takes a display out of its group of displays that show one picture. If it was the original, the next display of the
    /// group becomes the original and the group stays where it is. The display that left goes back to extending the desktop,
    /// to the right of the rest, with the mode it had before.
    /// </summary>
    private void LeaveGroup(MonitorViewModel monitor)
    {
        var duplicates = all.Where(m => m.Enabled && m.DuplicateOf == monitor.Key).ToList();
        if (duplicates.Count > 0)
        {
            var next = duplicates[0];
            next.DuplicateOf = null;
            next.OwnLayout = null;
            next.IsPrimary = monitor.IsPrimary;
            foreach (var other in duplicates.Skip(1))
            {
                other.DuplicateOf = next.Key;
            }
        }
        else if (monitor.DuplicateOf is null)
        {
            return;
        }

        monitor.DuplicateOf = null;
        monitor.IsPrimary = false;
        monitor.LimitModes(null);
        if (monitor.OwnLayout is { } own)
        {
            monitor.SetMode(own.Width, own.Height, own.RefreshHz);
            monitor.OwnLayout = null;
        }
        else
        {
            monitor.EnsureSupportedMode();
        }

        var others = all.Where(m => m.Enabled && m != monitor && !m.IsDuplicate).ToList();
        if (others.Count > 0)
        {
            monitor.SetPosition(others.Max(m => m.X + m.Width), others.FirstOrDefault(m => m.IsPrimary)?.Y ?? 0);
        }
    }

    /// <summary>
    /// A display's mode and orientation are shared with the displays that show the same picture, so a change made under
    /// any of them is handed to the original and from there to all of them.
    /// </summary>
    private void ShareSettings(MonitorViewModel monitor)
    {
        loading = true;
        var original = RootOf(monitor);
        if (monitor != original)
        {
            original.CopyModeFrom(monitor);
        }

        SyncGroups();
        loading = false;
    }
    private void MakePrimary(MonitorViewModel monitor)
    {
        loading = true;

        // The main display of a group is its original, whichever display of the group was ticked.
        var original = RootOf(monitor);
        foreach (var other in all.Where(m => m != original))
        {
            other.IsPrimary = false;
        }

        original.IsPrimary = true;
        SyncGroups();
        Renormalize();
        loading = false;
    }

    /// <summary>Re-anchors the layout so the main display is at (0,0), as Windows requires.</summary>
    private void Renormalize()
    {
        var normalized = LayoutMath.Normalize(LayoutMath.CloseGaps([.. all.Select(m => m.ToConfig())]));
        foreach (var config in normalized)
        {
            var monitor = all.First(m => m.Key == config.Key);
            if (monitor.Enabled)
            {
                monitor.SetPosition(config.X, config.Y);
            }
        }

        // Duplicates follow their original, which may just have moved.
        SyncGroups();
    }

    /// <summary>Called by the canvas after a drag, with the final (snapped) positions already in the view models.</summary>
    public void NotifyMoved()
    {
        SyncGroups();
        Renormalize();
        MarkDirty();
        LayoutChanged?.Invoke();
    }

    /// <summary>Takes a display that isn't connected out of the profile (it can reappear if it is connected later).</summary>
    public void RemoveMonitor(MonitorViewModel monitor)
    {
        if (monitor.IsConnected || !all.Contains(monitor))
        {
            return;
        }

        if (monitor.Enabled && !all.Any(m => m.Enabled && m != monitor))
        {
            ShowStatus("At least one display has to stay on.", InfoBarSeverity.Warning);
            return;
        }

        loading = true;
        LeaveGroup(monitor);
        monitor.PropertyChanged -= OnMonitorChanged;
        all.Remove(monitor);
        if (monitor.IsPrimary && all.FirstOrDefault(m => m.Enabled && !m.IsDuplicate) is { } next)
        {
            next.IsPrimary = true;
        }

        Renormalize();
        RebuildVisibleList();
        RefreshGroups();
        if (SelectedMonitor == monitor)
        {
            SelectedMonitor = Monitors.FirstOrDefault(m => m.Enabled) ?? Monitors.FirstOrDefault();
        }

        loading = false;
        MarkDirty();
        LayoutChanged?.Invoke();
    }

    partial void OnProfileNameChanged(string value)
    {
        if (!loading && SelectedProfile is { } profile && value.Trim().Length > 0)
        {
            profile.Name = value;
            MarkDirty();
        }
    }

    private void MarkDirty()
    {
        if (loading || SelectedProfile is null)
        {
            return;
        }

        IsDirty = true;
        SelectedProfile.IsDirty = true;
    }

    private void MarkClean()
    {
        IsDirty = false;
        foreach (var item in Profiles)
        {
            item.IsDirty = false;
        }
    }

    /// <summary>The profile as currently edited, saved or not.</summary>
    public Profile? BuildProfile()
    {
        if (SelectedProfile is not { } item || all.Count == 0)
        {
            return null;
        }

        string name = UniqueName(ProfileName.Trim().Length > 0 ? ProfileName.Trim() : item.Name, item.Id);
        return new Profile
        {
            Id = item.Id,
            Name = name,
            Monitors = LayoutMath.Normalize([.. all.Select(m => m.ToConfig())]),
        };
    }

    /// <summary>Writes the edits of the selected profile to disk.</summary>
    public bool Save()
    {
        var profile = BuildProfile();
        if (profile is null || SelectedProfile is not { } item)
        {
            return false;
        }

        service.Update(d => d with { Profiles = [.. d.Profiles.Select(p => p.Id == profile.Id ? profile : p)] });

        loading = true;
        item.Name = profile.Name;
        ProfileName = profile.Name;
        loading = false;
        IsDirty = false;
        item.IsDirty = false;
        RefreshActive();
        return true;
    }

    /// <summary>Throws away the unsaved edits of the selected profile.</summary>
    public void Discard() => Load(SelectedProfile?.Id);

    private string UniqueName(string name, Guid ownId)
    {
        string candidate = name;
        for (int n = 2; service.Data.Profiles.Any(p => p.Id != ownId && string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)); n++)
        {
            candidate = $"{name} ({n})";
        }

        return candidate;
    }

    // The operations below work on what is saved, so the caller has to have dealt with unsaved edits first.

    public void AddFromCurrent()
    {
        string name = UniqueName("New profile", Guid.Empty);
        var profile = Profile.Create(name, service.CaptureLayout());
        service.Update(d => d with { Profiles = [.. d.Profiles, profile] });
        Load(profile.Id);
    }

    public void DuplicateSelected()
    {
        var source = service.Data.Profiles.FirstOrDefault(p => p.Id == SelectedProfile?.Id);
        if (source is null)
        {
            return;
        }

        var copy = Profile.Create(UniqueName(source.Name + " copy", Guid.Empty), source.Monitors);
        service.Update(d => d with { Profiles = [.. d.Profiles, copy] });
        Load(copy.Id);
    }

    public void DeleteSelected()
    {
        if (SelectedProfile is not { } item)
        {
            return;
        }

        service.Update(d => d with
        {
            Profiles = [.. d.Profiles.Where(p => p.Id != item.Id)],
            Settings = d.Settings with
            {
                XboxEnterProfileId = d.Settings.XboxEnterProfileId == item.Id ? null : d.Settings.XboxEnterProfileId,
                XboxExitProfileId = d.Settings.XboxExitProfileId == item.Id ? null : d.Settings.XboxExitProfileId,
                LastAppliedProfileId = d.Settings.LastAppliedProfileId == item.Id ? null : d.Settings.LastAppliedProfileId,
            },
        });
        SelectedProfile = null;
        Load();
    }

    /// <summary>Moves the selected profile up or down in the list (the order used by "next profile").</summary>
    public void MoveSelected(int delta)
    {
        var list = service.Data.Profiles.ToList();
        int index = list.FindIndex(p => p.Id == SelectedProfile?.Id);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= list.Count)
        {
            return;
        }

        (list[index], list[target]) = (list[target], list[index]);
        service.Update(d => d with { Profiles = list });
        Load(SelectedProfile?.Id);
    }

    /// <summary>Applies the profile as it is being edited, without saving it.</summary>
    public async Task<ApplyResult?> ApplyAsync()
    {
        var profile = BuildProfile();
        if (profile is null)
        {
            return null;
        }

        IsBusy = true;
        try
        {
            return await Task.Run(() => service.Apply(profile));
        }
        finally
        {
            IsBusy = false;
            displays = service.Displays.GetDisplays();
            RefreshActive();
        }
    }

    public string CreateShortcut(string folder)
    {
        var profile = service.Data.Profiles.FirstOrDefault(p => p.Id == SelectedProfile?.Id)
            ?? throw new InvalidOperationException("No profile is selected.");
        return ShortcutService.CreateForProfile(profile, folder, AppServices.LaunchPath);
    }
}
