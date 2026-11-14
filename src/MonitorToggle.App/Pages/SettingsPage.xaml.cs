using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MonitorToggle.Core.Integration;
using MonitorToggle.Core.Model;

namespace MonitorToggle.App.Pages;

public sealed partial class SettingsPage : Page
{
    private bool loading;

    public SettingsPage()
    {
        InitializeComponent();
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        AboutCard.Description = $"Version {version} · MIT license";
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var service = App.Instance.Profiles;
        service.Reload();
        loading = true;
        StartupSwitch.IsOn = StartupRegistration.IsEnabled();
        TimeoutBox.Value = service.Data.Settings.RevertTimeoutSeconds;
        loading = false;
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

    private void OnTimeoutChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (loading || double.IsNaN(args.NewValue))
        {
            return;
        }

        int seconds = (int)args.NewValue;
        App.Instance.Profiles.Update(d => d with { Settings = d.Settings with { RevertTimeoutSeconds = seconds } });
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
