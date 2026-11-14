using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using MonitorToggle.Core.Display;
using MonitorToggle.Core.Integration;
using MonitorToggle.Core.Model;
using MonitorToggle.Core.Services;

namespace MonitorToggle.App.ViewModels;

public sealed partial class ProfileItem(Guid id, string name) : ObservableObject
{
    public Guid Id { get; } = id;

    [ObservableProperty]
    public partial string Name { get; set; } = name;

    [ObservableProperty]
    public partial bool IsActive { get; set; }
}

/// <summary>State and operations behind the profile editor page. Edits are saved automatically.</summary>
public sealed partial class ProfileEditorViewModel : ObservableObject
{
    private readonly ProfileService service;
    private readonly DispatcherQueueTimer saveTimer;
    private IReadOnlyList<DisplayInfo> displays = [];
    private bool loading;

    public ProfileEditorViewModel(ProfileService service)
    {
        this.service = service;
        saveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        saveTimer.Interval = TimeSpan.FromMilliseconds(400);
        saveTimer.IsRepeating = false;
        saveTimer.Tick += (_, _) => Persist();
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
    public partial bool IsBusy { get; set; }

    public bool IsNotBusy => !IsBusy;

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
            Monitors.Clear();
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

    partial void OnSelectedProfileChanging(ProfileItem? value)
    {
        // Save the profile being left while the old selection and its monitors are still in place.
        if (!loading)
        {
            Persist();
        }
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
        foreach (var old in Monitors)
        {
            old.PropertyChanged -= OnMonitorChanged;
        }

        Monitors.Clear();
        SelectedMonitor = null;

        if (profile is not null)
        {
            // Monitors connected now but missing from the profile appear switched off, ready to be enabled.
            var configs = profile.Monitors.ToList();
            double right = configs.Where(c => c.Enabled).Select(c => (double)c.Right).DefaultIfEmpty(0).Max();
            foreach (var display in displays.Where(d => configs.All(c => c.Key != d.Key)))
            {
                var best = service.Displays.GetModes(display.AdapterName).Cast<DisplayMode?>().FirstOrDefault();
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

            int index = 0;
            foreach (var config in configs.OrderBy(c => DisplayNumber(displays.FirstOrDefault(d => d.Key == c.Key), int.MaxValue)))
            {
                index++;
                var display = displays.FirstOrDefault(d => d.Key == config.Key);
                var modes = display is null ? [] : service.Displays.GetModes(display.AdapterName);
                var monitor = new MonitorViewModel(config, DisplayNumber(display, index), display is not null, modes);
                monitor.PropertyChanged += OnMonitorChanged;
                Monitors.Add(monitor);
            }

            SelectedMonitor = Monitors.FirstOrDefault(m => m.Enabled) ?? Monitors.FirstOrDefault();
            if (SelectedMonitor is not null)
            {
                SelectedMonitor.IsSelected = true;
            }
        }

        loading = false;
        LayoutChanged?.Invoke();
    }

    public void Select(MonitorViewModel? monitor)
    {
        foreach (var m in Monitors)
        {
            m.IsSelected = m == monitor;
        }

        SelectedMonitor = monitor;
    }

    internal static int DisplayNumber(DisplayInfo? display, int fallback)
    {
        string? name = display?.AdapterName;
        return name is not null && name.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(name.AsSpan(@"\\.\DISPLAY".Length), out int n)
            ? n
            : fallback;
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
                // Can't un-main the main display; pick another one instead.
                if (monitor.Enabled && Monitors.All(m => !m.IsPrimary || m == monitor))
                {
                    loading = true;
                    monitor.IsPrimary = true;
                    loading = false;
                }

                return;
            case nameof(MonitorViewModel.X) or nameof(MonitorViewModel.Y) or nameof(MonitorViewModel.Width) or nameof(MonitorViewModel.Height):
                break;
            case nameof(MonitorViewModel.RefreshHz) or nameof(MonitorViewModel.OrientationIndex):
                break;
            default:
                return;
        }

        ScheduleSave();
        LayoutChanged?.Invoke();
    }

    private void OnEnabledChanged(MonitorViewModel monitor)
    {
        loading = true;
        if (monitor.Enabled)
        {
            // Put a newly enabled monitor to the right of the others.
            var others = Monitors.Where(m => m.Enabled && m != monitor).ToList();
            if (others.Count > 0)
            {
                monitor.SetPosition(others.Max(m => m.X + m.Width), others.First(m => m.IsPrimary).Y);
            }
            else
            {
                monitor.IsPrimary = true;
            }
        }
        else if (!Monitors.Any(m => m.Enabled))
        {
            monitor.Enabled = true;
            ShowStatus("At least one display has to stay on.", InfoBarSeverity.Warning);
        }
        else if (monitor.IsPrimary)
        {
            monitor.IsPrimary = false;
            var next = Monitors.First(m => m.Enabled);
            next.IsPrimary = true;
        }

        if (!Monitors.Any(m => m.Enabled && m.IsPrimary) && Monitors.FirstOrDefault(m => m.Enabled) is { } first)
        {
            first.IsPrimary = true;
        }

        Renormalize();
        loading = false;
    }

    private void MakePrimary(MonitorViewModel monitor)
    {
        loading = true;
        foreach (var other in Monitors.Where(m => m != monitor))
        {
            other.IsPrimary = false;
        }

        Renormalize();
        loading = false;
    }

    /// <summary>Re-anchors the layout so the main display is at (0,0), as Windows requires.</summary>
    private void Renormalize()
    {
        var normalized = LayoutMath.Normalize([.. Monitors.Select(m => m.ToConfig())]);
        foreach (var config in normalized)
        {
            var monitor = Monitors.First(m => m.Key == config.Key);
            if (monitor.Enabled)
            {
                monitor.SetPosition(config.X, config.Y);
            }
        }
    }

    /// <summary>Called by the canvas after a drag, with the final (snapped) positions already in the view models.</summary>
    public void NotifyMoved()
    {
        Renormalize();
        ScheduleSave();
        LayoutChanged?.Invoke();
    }

    partial void OnProfileNameChanged(string value)
    {
        // Typing in the name box saves as you go.
        if (!loading && SelectedProfile is { } profile && value.Trim().Length > 0)
        {
            profile.Name = value;
            ScheduleSave();
        }
    }

    private void ScheduleSave()
    {
        saveTimer.Stop();
        saveTimer.Start();
    }

    public void Persist()
    {
        saveTimer.Stop();
        if (loading || SelectedProfile is not { } item || Monitors.Count == 0)
        {
            return;
        }

        string name = UniqueName(ProfileName.Trim().Length > 0 ? ProfileName.Trim() : item.Name, item.Id);
        var profile = new Profile
        {
            Id = item.Id,
            Name = name,
            Monitors = LayoutMath.Normalize([.. Monitors.Select(m => m.ToConfig())]),
        };

        service.Update(d => d with { Profiles = [.. d.Profiles.Select(p => p.Id == profile.Id ? profile : p)] });
    }

    private string UniqueName(string name, Guid ownId)
    {
        string candidate = name;
        for (int n = 2; service.Data.Profiles.Any(p => p.Id != ownId && string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)); n++)
        {
            candidate = $"{name} ({n})";
        }

        return candidate;
    }

    public void AddFromCurrent()
    {
        Persist();
        string name = UniqueName("New profile", Guid.Empty);
        var profile = Profile.Create(name, service.CaptureLayout());
        service.Update(d => d with { Profiles = [.. d.Profiles, profile] });
        Load(profile.Id);
    }

    public void DuplicateSelected()
    {
        Persist();
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
        Persist();
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

    public Profile? CurrentProfile()
    {
        Persist();
        return service.Data.Profiles.FirstOrDefault(p => p.Id == SelectedProfile?.Id);
    }

    public async Task<ApplyResult?> ApplyAsync()
    {
        var profile = CurrentProfile();
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
        var profile = CurrentProfile() ?? throw new InvalidOperationException("No profile is selected.");
        return ShortcutService.CreateForProfile(profile, folder, Environment.ProcessPath!);
    }
}
