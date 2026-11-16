using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using DisplayProfiles.Core.Integration;
using DisplayProfiles.Core.Model;

namespace DisplayProfiles.App.Pages;

public sealed partial class SettingsPage : Page, IControllerPage
{
    private static readonly int[] TimeoutChoices = [5, 10, 15, 20, 30, 45, 60, 120];

    private static readonly string[] ControllerChoices = ["Automatic (in Xbox mode)", "Always on", "Always off"];

    // The order shown in the drop-down, mapped to the stored values.
    private static readonly (string Name, DesktopThemePreference Value)[] DesktopThemes =
        [("Dark", DesktopThemePreference.Dark), ("Light", DesktopThemePreference.Light), ("Automatic (System)", DesktopThemePreference.System)];

    private static readonly (string Name, ControllerThemePreference Value)[] ControllerThemes =
        [("Light", ControllerThemePreference.Light), ("Dark", ControllerThemePreference.Dark), ("Classic", ControllerThemePreference.Classic)];

    private bool loading;
    private List<int> timeouts = [.. TimeoutChoices];

    public SettingsPage()
    {
        InitializeComponent();
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        AboutCard.Description = $"Version {version} · MIT license";
    }

    public IReadOnlyList<(string Button, string Text)> Hints { get; } = [("A", "Select"), ("B", "Back"), ("LB RB", "Sections")];

    public bool OnPrimaryAction() => false;

    public bool OnSecondaryAction() => false;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var service = App.Instance.Profiles;
        service.Reload();
        loading = true;
        StartupSwitch.IsOn = StartupRegistration.IsEnabled();
        ShowDisconnectedSwitch.IsOn = service.Data.Settings.ShowDisconnectedDisplays;

        int saved = service.Data.Settings.RevertTimeoutSeconds;
        timeouts = [.. TimeoutChoices];
        if (!timeouts.Contains(saved))
        {
            timeouts.Add(saved);
            timeouts.Sort();
        }

        TimeoutBox.ItemsSource = timeouts.Select(s => $"{s} seconds").ToList();
        TimeoutBox.SelectedIndex = timeouts.IndexOf(saved);
        ControllerBox.ItemsSource = ControllerChoices;
        ControllerBox.SelectedIndex = (int)service.Data.Settings.ControllerMode;

        // The theme choices depend on which layout is showing.
        if (ControllerUi.IsActive)
        {
            ThemeCard.Description = "Colour scheme of the Xbox mode layout. Classic is the original green look.";
            ThemeBox.ItemsSource = ControllerThemes.Select(t => t.Name).ToList();
            ThemeBox.SelectedIndex = Array.FindIndex(ControllerThemes, t => t.Value == service.Data.Settings.ControllerTheme);
        }
        else
        {
            ThemeCard.Description = "Colour scheme of the app window.";
            ThemeBox.ItemsSource = DesktopThemes.Select(t => t.Name).ToList();
            ThemeBox.SelectedIndex = Array.FindIndex(DesktopThemes, t => t.Value == service.Data.Settings.DesktopTheme);
        }

        loading = false;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (loading || ThemeBox.SelectedIndex < 0)
        {
            return;
        }

        if (ControllerUi.IsActive)
        {
            var theme = ControllerThemes[ThemeBox.SelectedIndex].Value;
            App.Instance.Profiles.Update(d => d with { Settings = d.Settings with { ControllerTheme = theme } });

            // The look is applied when the window is created, and rebuilding destroys this page, so let the event finish first.
            DispatcherQueue.TryEnqueue(App.Instance.RebuildWindow);
        }
        else
        {
            var theme = DesktopThemes[ThemeBox.SelectedIndex].Value;
            App.Instance.Profiles.Update(d => d with { Settings = d.Settings with { DesktopTheme = theme } });
            App.Instance.MainWindow?.ApplyDesktopTheme();
        }
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (loading)
        {
            return;
        }

        StartupRegistration.SetEnabled(StartupSwitch.IsOn, Environment.ProcessPath!);
        App.Instance.Profiles.Update(d => d with { Settings = d.Settings with { StartWithWindows = StartupSwitch.IsOn } });
    }

    private void OnShowDisconnectedToggled(object sender, RoutedEventArgs e)
    {
        if (loading)
        {
            return;
        }

        bool show = ShowDisconnectedSwitch.IsOn;
        App.Instance.Profiles.Update(d => d with { Settings = d.Settings with { ShowDisconnectedDisplays = show } });
    }

    private void OnTimeoutChanged(object? sender, EventArgs e)
    {
        if (loading || TimeoutBox.SelectedIndex < 0)
        {
            return;
        }

        int seconds = timeouts[TimeoutBox.SelectedIndex];
        App.Instance.Profiles.Update(d => d with { Settings = d.Settings with { RevertTimeoutSeconds = seconds } });
    }

    private void OnControllerModeChanged(object? sender, EventArgs e)
    {
        if (loading || ControllerBox.SelectedIndex < 0)
        {
            return;
        }

        var mode = (ControllerModePreference)ControllerBox.SelectedIndex;
        App.Instance.Profiles.Update(d => d with { Settings = d.Settings with { ControllerMode = mode } });

        // Rebuilding the window destroys this page, so let the current event finish first.
        DispatcherQueue.TryEnqueue(App.Instance.RefreshUiMode);
    }

    private void OnProfileShortcutsDesktop(object sender, RoutedEventArgs e) => CreateProfileShortcuts(ShortcutService.DesktopFolder, "the desktop");

    private void OnProfileShortcutsStartMenu(object sender, RoutedEventArgs e) => CreateProfileShortcuts(ShortcutService.StartMenuFolder, "the Start menu");

    private void OnNextShortcutDesktop(object sender, RoutedEventArgs e) => Run(() =>
    {
        ShortcutService.CreateForNextProfile(ShortcutService.DesktopFolder, Environment.ProcessPath!);
        return "Created the \"Next profile\" shortcut on the desktop.";
    });

    private void OnNextShortcutStartMenu(object sender, RoutedEventArgs e) => Run(() =>
    {
        ShortcutService.CreateForNextProfile(ShortcutService.StartMenuFolder, Environment.ProcessPath!);
        return "Created the \"Next profile\" shortcut in the Start menu.";
    });

    private void CreateProfileShortcuts(string folder, string where) => Run(() =>
    {
        var profiles = App.Instance.Profiles.Data.Profiles;
        foreach (var profile in profiles)
        {
            ShortcutService.CreateForProfile(profile, folder, Environment.ProcessPath!);
        }

        return $"Created {profiles.Count} shortcut{(profiles.Count == 1 ? string.Empty : "s")} in {where}.";
    });

    private void Run(Func<string> action)
    {
        try
        {
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Message = action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Message = $"Couldn't create the shortcut: {ex.Message}";
        }

        ResultBar.IsOpen = true;
    }

    private void OnOpenDataFolder(object sender, RoutedEventArgs e)
    {
        string folder = Path.GetDirectoryName(ProfileStore.DefaultPath)!;
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }
}
