using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using DisplayProfiles.App.Controls;
using DisplayProfiles.Core.Integration;

namespace DisplayProfiles.App.Pages;

public sealed partial class XboxModePage : Page, IControllerPage
{
    private const string NoChange = "Don't change the displays";

    private bool loading;

    public XboxModePage()
    {
        InitializeComponent();
    }

    public IReadOnlyList<(string Button, string Text)> Hints { get; } = [("A", "Select"), ("B", "Back"), ("LB RB", "Sections")];

    public bool OnPrimaryAction() => false;

    public bool OnSecondaryAction() => false;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var service = App.Instance.Profiles;
        service.Reload();
        var settings = service.Data.Settings;
        var names = new List<string> { NoChange };
        names.AddRange(service.Data.Profiles.Select(p => p.Name));

        loading = true;
        EnterBox.ItemsSource = names;
        ExitBox.ItemsSource = names.ToList();
        EnterBox.SelectedIndex = IndexOf(service.Data.Profiles, settings.XboxEnterProfileId);
        ExitBox.SelectedIndex = IndexOf(service.Data.Profiles, settings.XboxExitProfileId);
        loading = false;

        var monitor = App.Instance.XboxMonitor;
        if (monitor is null)
        {
            SupportBar.Severity = InfoBarSeverity.Warning;
            SupportBar.Title = "Xbox mode isn't available";
            SupportBar.Message = "This version of Windows doesn't have the Xbox full screen experience.";
        }
        else
        {
            SupportBar.Severity = InfoBarSeverity.Informational;
            SupportBar.Title = monitor.IsActive ? "Xbox mode is on" : "Xbox mode is available";
            SupportBar.Message = "Display Profiles watches for Xbox mode in the background and switches profiles automatically.";
        }

        StartupBar.IsOpen = !StartupRegistration.IsEnabled();
    }

    private static int IndexOf(IReadOnlyList<Core.Model.Profile> profiles, Guid? id)
    {
        int index = id is { } value ? profiles.ToList().FindIndex(p => p.Id == value) : -1;
        return index + 1;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (loading)
        {
            return;
        }

        var service = App.Instance.Profiles;
        Guid? Pick(OptionPicker box) => box.SelectedIndex > 0 ? service.Data.Profiles[box.SelectedIndex - 1].Id : null;
        var enter = Pick(EnterBox);
        var exit = Pick(ExitBox);
        service.Update(d => d with { Settings = d.Settings with { XboxEnterProfileId = enter, XboxExitProfileId = exit } });
    }
}
