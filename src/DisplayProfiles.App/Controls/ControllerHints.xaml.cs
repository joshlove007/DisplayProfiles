using DisplayProfiles.Core.Model;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DisplayProfiles.App.Controls;

/// <summary>The row of controller button prompts along the bottom of the screen in controller mode ("A Select  B Back ...").</summary>
public sealed partial class ControllerHints : UserControl
{
    public ControllerHints()
    {
        InitializeComponent();
    }

    public void SetHints(IEnumerable<(string Button, string Text)> hints)
    {
        var theme = App.Instance.Profiles.Data.Settings.ControllerTheme;
        Panel.Children.Clear();
        foreach (var (button, text) in hints)
        {
            Panel.Children.Add(Chip(button, text, theme));
        }
    }

    private static StackPanel Chip(string button, string text, ControllerThemePreference theme)
    {
        bool round = button.Length == 1;
        var badge = theme == ControllerThemePreference.Classic
            ? FilledBadge(button, round)
            : OutlinedBadge(button, round, light: theme == ControllerThemePreference.Light);

        var label = new TextBlock
        {
            Text = text,
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center,
        };

        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { badge, label } };
    }

    // Classic: solid coloured discs, like the original Xbox dashboard.
    private static Border FilledBadge(string button, bool round)
    {
        var (fill, foreground) = button switch
        {
            "A" => (Color.FromArgb(255, 16, 124, 16), Colors.White),
            "B" => (Color.FromArgb(255, 209, 52, 56), Colors.White),
            "X" => (Color.FromArgb(255, 0, 120, 212), Colors.White),
            "Y" => (Color.FromArgb(255, 247, 181, 0), Colors.Black),
            _ => (Color.FromArgb(255, 90, 90, 90), Colors.White),
        };

        return Badge(button, round, new SolidColorBrush(fill), null, new SolidColorBrush(foreground));
    }

    // Dark and Light: a coloured ring and letter on a transparent disc, like the current Xbox app.
    private static Border OutlinedBadge(string button, bool round, bool light)
    {
        var color = (button, light) switch
        {
            ("A", false) => Color.FromArgb(255, 110, 200, 90),
            ("B", false) => Color.FromArgb(255, 235, 60, 60),
            ("X", false) => Color.FromArgb(255, 80, 160, 240),
            ("Y", false) => Color.FromArgb(255, 250, 190, 20),
            ("A", true) => Color.FromArgb(255, 16, 124, 16),
            ("B", true) => Color.FromArgb(255, 200, 30, 35),
            ("X", true) => Color.FromArgb(255, 0, 100, 190),
            ("Y", true) => Color.FromArgb(255, 190, 130, 0),
            (_, false) => Color.FromArgb(255, 235, 235, 235),
            _ => Color.FromArgb(255, 40, 40, 40),
        };

        var brush = new SolidColorBrush(color);
        return Badge(button, round, null, brush, brush);
    }

    private static Border Badge(string button, bool round, Brush? fill, Brush? ring, Brush foreground) => new()
    {
        MinWidth = 30,
        Height = 30,
        Padding = round ? new Thickness(0) : new Thickness(8, 0, 8, 0),
        CornerRadius = new CornerRadius(round ? 15 : 8),
        Background = fill,
        BorderBrush = ring,
        BorderThickness = ring is null ? new Thickness(0) : new Thickness(2),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = button,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 15,
            Foreground = foreground,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };
}
