using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MonitorToggle.App.Controls;
using MonitorToggle.App.ViewModels;
using MonitorToggle.Core.Integration;
using CommunityToolkit.WinUI.Controls;

namespace MonitorToggle.App.Pages;

public sealed partial class ProfilesPage : Page
{
    public ProfilesPage()
    {
        ViewModel = new ProfileEditorViewModel(App.Instance.Profiles);
        InitializeComponent();
        LayoutView.Attach(ViewModel);
    }

    public ProfileEditorViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Load();

    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Persist();

    private void OnNewProfileClick(object sender, RoutedEventArgs e) => ViewModel.AddFromCurrent();

    private void OnDuplicateClick(object sender, RoutedEventArgs e) => ViewModel.DuplicateSelected();

    private void OnMoveUpClick(object sender, RoutedEventArgs e) => ViewModel.MoveSelected(-1);

    private void OnMoveDownClick(object sender, RoutedEventArgs e) => ViewModel.MoveSelected(1);

    private void OnIdentifyClick(object sender, RoutedEventArgs e) =>
        IdentifyOverlay.Show(ViewModel.Service.Displays.GetDisplays());

    private void OnMonitorExpanded(object sender, EventArgs e)
    {
        if (sender is SettingsExpander { DataContext: MonitorViewModel monitor })
        {
            ViewModel.Select(monitor);
        }
    }

    private void OnDesktopShortcutClick(object sender, RoutedEventArgs e) => CreateShortcut(ShortcutService.DesktopFolder, "the desktop");

    private void OnStartMenuShortcutClick(object sender, RoutedEventArgs e) => CreateShortcut(ShortcutService.StartMenuFolder, "the Start menu");

    private void CreateShortcut(string folder, string where)
    {
        try
        {
            ViewModel.CreateShortcut(folder);
            ViewModel.ShowStatus($"Created a shortcut on {where}. Pin it to the Game Bar LaunchDeck widget to switch with a controller.", InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ViewModel.ShowStatus($"Couldn't create the shortcut: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedProfile is not { } profile)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Delete \"{profile.Name}\"?",
            Content = "This removes the profile from MonitorToggle. Your displays aren't changed.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.DeleteSelected();
        }
    }

    private async void OnApplyClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowStatus(string.Empty, InfoBarSeverity.Informational);
        string name = ViewModel.SelectedProfile?.Name ?? string.Empty;
        var result = await ViewModel.ApplyAsync();
        if (result is null)
        {
            return;
        }

        if (!result.Success)
        {
            ViewModel.ShowStatus(result.Error ?? "Windows did not apply the display settings.", InfoBarSeverity.Error);
            return;
        }

        int timeout = Math.Clamp(ViewModel.Service.Data.Settings.RevertTimeoutSeconds, 5, 120);
        bool keep = await KeepSettingsDialog.ShowAsync(XamlRoot, timeout);
        if (keep)
        {
            string warnings = string.Join(" ", result.Warnings);
            ViewModel.ShowStatus(
                warnings.Length > 0 ? $"Switched to {name}. {warnings}" : $"Switched to {name}.",
                warnings.Length > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
            return;
        }

        var revert = await Task.Run(() => ViewModel.Service.ApplyLayout(result.PreviousLayout));
        ViewModel.RefreshActive();
        ViewModel.ShowStatus(
            revert.Success ? "Reverted to your previous display settings." : $"Couldn't revert: {revert.Error}",
            revert.Success ? InfoBarSeverity.Informational : InfoBarSeverity.Error);
    }
}
