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
    private bool suppressSelection;

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

    public IReadOnlyList<(string Button, string Text)> Hints { get; } = [("A", "Select"), ("B", "Back"), ("X", "Save"), ("Y", "Apply"), ("LB RB", "Sections")];

    public bool OnPrimaryAction()
    {
        if (!ViewModel.HasProfile || !ViewModel.IsNotBusy)
        {
            return false;
        }

        OnApplyClick(this, new RoutedEventArgs());
        return true;
    }

    public bool OnSecondaryAction()
    {
        if (!ViewModel.CanSave)
        {
            return false;
        }

        OnSaveClick(this, new RoutedEventArgs());
        return true;
    }

    private void OnGrabChanged(bool grabbing) =>
        App.Instance.MainWindow?.ShowHints(grabbing
            ? [("◄▲▼►", "Move display"), ("A", "Drop here"), ("B", "Cancel")]
            : Hints);

    // The page is cached, so unsaved edits are still here when you come back from another page.
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (ViewModel.IsDirty)
        {
            ViewModel.RefreshVisibility();
        }
        else
        {
            ViewModel.Load();
        }
    }

    private async void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelection || ProfileListView.SelectedItem is not ProfileItem target || target == ViewModel.SelectedProfile)
        {
            return;
        }

        if (ViewModel.IsDirty)
        {
            // Put the highlight back on the profile being edited while we ask what to do with its changes.
            suppressSelection = true;
            ProfileListView.SelectedItem = ViewModel.SelectedProfile;
            suppressSelection = false;
            if (!await ConfirmLeaveAsync())
            {
                return;
            }
        }

        // Discarding reloads the list, so look the profile up again.
        ViewModel.SelectedProfile = ViewModel.Profiles.FirstOrDefault(p => p.Id == target.Id) ?? ViewModel.SelectedProfile;
    }

    /// <summary>If the selected profile has unsaved edits, asks to save or discard them. Returns false if the user cancels.</summary>
    private async Task<bool> ConfirmLeaveAsync()
    {
        if (!ViewModel.IsDirty)
        {
            return true;
        }

        string title = $"Save changes to \"{ViewModel.SelectedProfile?.Name}\"?";
        int choice;
        if (ControllerUi.IsActive && OverlayHost.Current is { } overlay)
        {
            choice = await overlay.ShowChoiceAsync(title, ["Save", "Don't save", "Cancel"], 0) ?? 2;
        }
        else
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title,
                Content = "Your changes to this profile haven't been saved.",
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Don't save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
            };
            choice = await dialog.ShowAsync() switch
            {
                ContentDialogResult.Primary => 0,
                ContentDialogResult.Secondary => 1,
                _ => 2,
            };
        }

        switch (choice)
        {
            case 0:
                ViewModel.Save();
                return true;
            case 1:
                ViewModel.Discard();
                return true;
            default:
                return false;
        }
    }

    private async void OnMoreClick(object sender, RoutedEventArgs e)
    {
        var overlay = OverlayHost.Current;
        if (!ControllerUi.IsActive || overlay is null)
        {
            return;
        }

        string[] actions = ["Discard unsaved changes", "Create desktop shortcut", "Create Start menu shortcut", "Duplicate profile", "Move up in list", "Move down in list", "Delete profile"];
        switch (await overlay.ShowChoiceAsync("Profile options", actions, -1))
        {
            case 0: OnDiscardClick(this, e); break;
            case 1: OnDesktopShortcutClick(this, e); break;
            case 2: OnStartMenuShortcutClick(this, e); break;
            case 3: OnDuplicateClick(this, e); break;
            case 4: OnMoveUpClick(this, e); break;
            case 5: OnMoveDownClick(this, e); break;
            case 6: OnDeleteClick(this, e); break;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Save())
        {
            ViewModel.ShowStatus("Saved.", InfoBarSeverity.Success);
        }
    }

    private void OnDiscardClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsDirty)
        {
            return;
        }

        ViewModel.Discard();
        ViewModel.ShowStatus("Unsaved changes discarded.", InfoBarSeverity.Informational);
    }

    private void OnRemoveMonitorClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MonitorViewModel monitor })
        {
            ViewModel.RemoveMonitor(monitor);
        }
    }

    private async void OnNewProfileClick(object sender, RoutedEventArgs e)
    {
        if (await ConfirmLeaveAsync())
        {
            ViewModel.AddFromCurrent();
        }
    }

    private async void OnDuplicateClick(object sender, RoutedEventArgs e)
    {
        if (await ConfirmLeaveAsync())
        {
            ViewModel.DuplicateSelected();
        }
    }

    private async void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        if (await ConfirmLeaveAsync())
        {
            ViewModel.MoveSelected(-1);
        }
    }

    private async void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (await ConfirmLeaveAsync())
        {
            ViewModel.MoveSelected(1);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshDisplays();
        ViewModel.ShowStatus("Looked for the connected displays again.", InfoBarSeverity.Success);
    }

    private void OnIdentifyClick(object sender, RoutedEventArgs e) =>
        IdentifyOverlay.Show(ViewModel.Service.Displays.GetDisplays(), key => ViewModel.NumberFor(key));

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
            string unsaved = ViewModel.IsDirty ? " The changes aren't saved yet." : string.Empty;
            ViewModel.ShowStatus(
                warnings.Length > 0 ? $"Switched to {name}. {warnings}{unsaved}" : $"Switched to {name}.{unsaved}",
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
