using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using DisplayProfiles.App.Controls;
using DisplayProfiles.App.ViewModels;
using DisplayProfiles.Core.Integration;
using CommunityToolkit.WinUI.Controls;

namespace DisplayProfiles.App.Pages;

public sealed partial class ProfilesPage : Page, IControllerPage
{
    public ProfilesPage()
    {
        ViewModel = new ProfileEditorViewModel(App.Instance.Profiles);
        InitializeComponent();
        LayoutView.Attach(ViewModel);
        LayoutView.GrabChanged += OnGrabChanged;

        if (ControllerUi.IsActive)
        {
            // The pop-up menu can't be scaled or reached with a controller; the overlay list replaces it.
            MoreButton.Flyout = null;
        }
    }

    public ProfileEditorViewModel ViewModel { get; }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e) => CenterEditor();

    private void OnLayoutStateChanged(object sender, VisualStateChangedEventArgs e) => CenterEditor();

    // The list stays at the left. In the two-column layout the editor is centred in the whole window rather than in the
    // space beside the list, but never closer to the list than its normal padding and never wider than ContentWidth.
    private void CenterEditor()
    {
        const double ContentWidth = 920;
        const double ListEdge = 280;
        const double PadLeft = 24;
        const double PadRight = 36;

        double page = PageRoot.ActualWidth;
        if (Grid.GetColumnSpan(EditorScroll) != 1 || page <= 0)
        {
            EditorPanel.Margin = new Thickness(0);
            return;
        }

        double minLeft = ListEdge + PadLeft;
        double width = Math.Max(0, Math.Min(ContentWidth, page - minLeft - PadRight));
        double left = Math.Max(minLeft, (page - width) / 2);
        double right = Math.Max(0, page - left - width - PadRight);
        EditorPanel.Margin = new Thickness(left - minLeft, 0, right, 0);
    }

    public IReadOnlyList<(string Button, string Text)> Hints { get; } = [("A", "Select"), ("B", "Back"), ("Y", "Apply"), ("LB RB", "Sections")];

    public bool OnPrimaryAction()
    {
        if (!ViewModel.HasProfile || !ViewModel.IsNotBusy)
        {
            return false;
        }

        OnApplyClick(this, new RoutedEventArgs());
        return true;
    }

    private void OnGrabChanged(bool grabbing) =>
        App.Instance.MainWindow?.ShowHints(grabbing
            ? [("◄▲▼►", "Move display"), ("A", "Drop here"), ("B", "Cancel")]
            : Hints);

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Load();

    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Persist();

    private async void OnMoreClick(object sender, RoutedEventArgs e)
    {
        var overlay = OverlayHost.Current;
        if (!ControllerUi.IsActive || overlay is null)
        {
            return;
        }

        string[] actions = ["Create desktop shortcut", "Create Start menu shortcut", "Duplicate profile", "Move up in list", "Move down in list", "Delete profile"];
        switch (await overlay.ShowChoiceAsync("Profile options", actions, -1))
        {
            case 0: OnDesktopShortcutClick(this, e); break;
            case 1: OnStartMenuShortcutClick(this, e); break;
            case 2: OnDuplicateClick(this, e); break;
            case 3: OnMoveUpClick(this, e); break;
            case 4: OnMoveDownClick(this, e); break;
            case 5: OnDeleteClick(this, e); break;
        }
    }

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

        bool confirmed;
        if (ControllerUi.IsActive && OverlayHost.Current is { } overlay)
        {
            confirmed = await overlay.ShowConfirmAsync(
                $"Delete \"{profile.Name}\"?",
                "This removes the profile from Display Profiles. Your displays aren't changed.",
                "Delete", "Cancel", defaultToSecondary: true);
        }
        else
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = $"Delete \"{profile.Name}\"?",
                Content = "This removes the profile from Display Profiles. Your displays aren't changed.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
            };
            confirmed = await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        if (confirmed)
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
