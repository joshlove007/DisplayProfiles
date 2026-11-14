using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DisplayProfiles.App.Controls;

/// <summary>
/// Picks one of a list of strings. On the desktop it is a normal ComboBox; in controller mode it is a button that opens a
/// large list in the in-window overlay, because ordinary drop-downs can't be scaled up or driven by a controller.
/// </summary>
public sealed partial class OptionPicker : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(object), typeof(OptionPicker), new PropertyMetadata(null, (d, _) => ((OptionPicker)d).Refresh()));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(OptionPicker), new PropertyMetadata(-1, (d, _) => ((OptionPicker)d).Refresh()));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(OptionPicker), new PropertyMetadata(string.Empty, (d, e) => ((OptionPicker)d).OnTitleChanged((string)e.NewValue)));

    private bool updating;

    public OptionPicker()
    {
        InitializeComponent();
        bool controller = ControllerUi.IsActive;
        Combo.Visibility = controller ? Visibility.Collapsed : Visibility.Visible;
        PickButton.Visibility = controller ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Raised when the user picks a different item.</summary>
    public event EventHandler? SelectionChanged;

    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Heading of the overlay list, and the accessible name of the control.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private IReadOnlyList<string> Items => (ItemsSource as IEnumerable<string>)?.ToList() ?? [];

    private void OnTitleChanged(string title)
    {
        AutomationHelper.SetName(Combo, title);
        AutomationHelper.SetName(PickButton, title);
    }

    private void Refresh()
    {
        updating = true;
        var items = Items;
        Combo.ItemsSource = items;
        Combo.SelectedIndex = SelectedIndex < items.Count ? SelectedIndex : -1;
        ValueText.Text = SelectedIndex >= 0 && SelectedIndex < items.Count ? items[SelectedIndex] : string.Empty;
        updating = false;
    }

    private void OnComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating || Combo.SelectedIndex < 0)
        {
            return;
        }

        SelectedIndex = Combo.SelectedIndex;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnPickClick(object sender, RoutedEventArgs e)
    {
        var overlay = OverlayHost.Current;
        if (overlay is null)
        {
            return;
        }

        int? choice = await overlay.ShowChoiceAsync(Title, Items, SelectedIndex);
        if (choice is { } index && index != SelectedIndex)
        {
            SelectedIndex = index;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
