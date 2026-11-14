using System.Text.Json;
using Microsoft.Gaming.XboxGameBar;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MonitorToggle.Widget;

public sealed class ProfileEntry(Guid id, string name, bool isActive)
{
    public Guid Id { get; } = id;

    public string Name { get; } = name;

    public bool IsActive { get; } = isActive;

    public Visibility ActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>
/// Lists the profiles MonitorToggle published for this widget and asks it, through the monitortoggle: URI, to switch.
/// The widget runs in a sandbox, so it never touches the displays itself.
/// </summary>
public sealed partial class WidgetPage : Page
{
    private const string SnapshotFile = "profiles.json";

    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private XboxGameBarWidget? widget;
    private DateTimeOffset lastWrite;
    private Guid? pendingProfile;
    private DateTimeOffset pendingSince;

    public WidgetPage()
    {
        InitializeComponent();
        timer.Tick += (_, _) => Refresh();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        widget = (XboxGameBarWidget)e.Parameter;
        ApplyTheme();
        widget.RequestedThemeChanged += OnThemeChanged;
        widget.VisibleChanged += OnVisibleChanged;

        Refresh();
        timer.Start();

        // Ask the app to publish fresh data (and make sure it is running).
        _ = RequestAsync(new Uri("monitortoggle:refresh"));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        timer.Stop();
        if (widget is not null)
        {
            widget.RequestedThemeChanged -= OnThemeChanged;
            widget.VisibleChanged -= OnVisibleChanged;
        }
    }

    private async void OnThemeChanged(XboxGameBarWidget sender, object args) =>
        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, ApplyTheme);

    private async void OnVisibleChanged(XboxGameBarWidget sender, object args) =>
        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
            if (sender.Visible)
            {
                Refresh();
                timer.Start();
            }
            else
            {
                timer.Stop();
            }
        });

    private void ApplyTheme()
    {
        if (widget is not null)
        {
            Root.RequestedTheme = widget.RequestedTheme;
        }
    }

    private void Refresh()
    {
        string path = Path.Combine(ApplicationData.Current.LocalFolder.Path, SnapshotFile);
        if (!File.Exists(path))
        {
            ShowEmpty("MonitorToggle hasn't set up profiles for Game Bar yet. Open MonitorToggle once to get started.");
            return;
        }

        var written = File.GetLastWriteTimeUtc(path);
        if (written == lastWrite && ProfileList.ItemsSource is not null)
        {
            CheckPending();
            return;
        }

        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var profiles = new List<ProfileEntry>();
            foreach (var p in json.RootElement.GetProperty("profiles").EnumerateArray())
            {
                profiles.Add(new ProfileEntry(
                    p.GetProperty("id").GetGuid(),
                    p.GetProperty("name").GetString() ?? string.Empty,
                    p.GetProperty("isActive").GetBoolean()));
            }

            lastWrite = written;
            if (profiles.Count == 0)
            {
                ShowEmpty("There are no profiles yet. Create one in MonitorToggle.");
                return;
            }

            EmptyPanel.Visibility = Visibility.Collapsed;
            ProfileList.Visibility = Visibility.Visible;
            ProfileList.ItemsSource = profiles;
            CheckPending();
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException)
        {
            // The app may be rewriting the file; try again on the next tick.
            App.Log($"snapshot read failed: {ex.Message}");
        }
    }

    private void ShowEmpty(string message)
    {
        ProfileList.Visibility = Visibility.Collapsed;
        EmptyPanel.Visibility = Visibility.Visible;
        EmptyText.Text = message;
    }

    private void CheckPending()
    {
        if (pendingProfile is not { } id)
        {
            return;
        }

        var entries = (ProfileList.ItemsSource as IEnumerable<ProfileEntry>) ?? [];
        if (entries.Any(p => p.Id == id && p.IsActive))
        {
            pendingProfile = null;
            SetStatus(null);
        }
        else if (DateTimeOffset.UtcNow - pendingSince > TimeSpan.FromSeconds(25))
        {
            pendingProfile = null;
            SetStatus("MonitorToggle didn't confirm the switch. Check the MonitorToggle app for details.");
        }
    }

    private void SetStatus(string? text)
    {
        StatusText.Text = text ?? string.Empty;
        StatusText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnProfileClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ProfileEntry profile || widget is null)
        {
            return;
        }

        if (profile.IsActive)
        {
            SetStatus($"{profile.Name} is already active.");
            return;
        }

        pendingProfile = profile.Id;
        pendingSince = DateTimeOffset.UtcNow;
        SetStatus($"Switching to {profile.Name}…");

        bool started = await RequestAsync(new Uri($"monitortoggle:apply/{profile.Id}"));
        if (!started)
        {
            pendingProfile = null;
            SetStatus("Couldn't start MonitorToggle. Open the MonitorToggle app once, then try again.");
        }
    }

    private async void OnOpenAppClick(object sender, RoutedEventArgs e)
    {
        if (!await RequestAsync(new Uri("monitortoggle:refresh")))
        {
            SetStatus("Couldn't start MonitorToggle. Is it installed?");
        }
    }

    private async Task<bool> RequestAsync(Uri uri)
    {
        try
        {
            return widget is not null && await widget.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            App.Log($"launch {uri} failed: {ex.Message}");
            return false;
        }
    }
}
