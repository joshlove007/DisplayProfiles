using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using DisplayProfiles.Core.Model;
using DisplayProfiles.Core.Services;
using Windows.UI.ViewManagement;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DisplayProfiles.App.Tray;

/// <summary>Owns the notification-area icon: themed glyph, profile menu, and balloon feedback.</summary>
internal sealed class TrayHost : IDisposable
{
    private const int OpenId = 1;
    private const int ExitId = 2;
    private const int FirstProfileId = 100;

    private readonly ProfileService profiles;
    private readonly DispatcherQueue ui;
    private readonly Action openWindow;
    private readonly Action exit;
    private readonly TrayIcon tray = new();
    private readonly UISettings uiSettings = new();
    private bool applying;

    public TrayHost(ProfileService profiles, DispatcherQueue ui, Action openWindow, Action exit)
    {
        this.profiles = profiles;
        this.ui = ui;
        this.openWindow = openWindow;
        this.exit = exit;

        tray.Selected += ShowMenu;
        tray.ContextRequested += ShowMenu;
        uiSettings.ColorValuesChanged += (_, _) => ui.TryEnqueue(UpdateIcon);
        UpdateIcon();
        tray.SetTooltip("Display Profiles");
    }

    public void Notify(string title, string message) => tray.ShowBalloon(title, message);

    public void Dispose()
    {
        tray.Dispose();
    }

    private void UpdateIcon()
    {
        bool lightTaskbar = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", 0) is int and not 0;

        int size = (int)PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, PInvoke.GetDpiForSystem());
        tray.SetIcon(AppServices.AssetPath(lightTaskbar ? "tray-light.ico" : "tray-dark.ico"), size);
    }

    private void ShowMenu(int x, int y)
    {
        profiles.Reload();
        var active = profiles.FindActiveProfile();
        var list = profiles.Data.Profiles;

        var entries = new List<MenuEntry>();
        for (int i = 0; i < list.Count; i++)
        {
            entries.Add(new MenuEntry(FirstProfileId + i, list[i].Name, IsChecked: list[i].Id == active?.Id, IsDisabled: applying));
        }

        if (list.Count == 0)
        {
            entries.Add(new MenuEntry(0, "No profiles yet", IsDisabled: true));
        }

        entries.Add(MenuEntry.Separator);
        entries.Add(new MenuEntry(OpenId, "Open Display Profiles"));
        entries.Add(new MenuEntry(ExitId, "Exit"));

        tray.SetTooltip(active is null ? "Display Profiles" : $"Display Profiles – {active.Name}");

        int choice = NativeMenu.Show(tray.Handle, entries, x, y);
        switch (choice)
        {
            case 0:
                break;
            case OpenId:
                openWindow();
                break;
            case ExitId:
                exit();
                break;
            default:
                _ = ApplyAsync(list[choice - FirstProfileId]);
                break;
        }
    }

    private async Task ApplyAsync(Profile profile)
    {
        if (applying)
        {
            return;
        }

        applying = true;
        try
        {
            var result = await Task.Run(() => profiles.Apply(profile));
            if (!result.Success)
            {
                tray.ShowBalloon("Couldn't switch to " + profile.Name, result.Error ?? "Windows did not apply the display settings.");
            }
            else
            {
                tray.SetTooltip($"Display Profiles – {profile.Name}");
            }
        }
        finally
        {
            applying = false;
        }
    }
}
