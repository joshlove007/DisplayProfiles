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
        Panel.Children.Clear();
        foreach (var (button, text) in hints)
        {
            Panel.Children.Add(Chip(button, text));
        }
    }

    private static StackPanel Chip(string button, string text)
    {
        var (fill, foreground) = button switch
        {
            "A" => (Color.FromArgb(255, 16, 124, 16), Colors.White),
            "B" => (Color.FromArgb(255, 209, 52, 56), Colors.White),
            "X" => (Color.FromArgb(255, 0, 120, 212), Colors.White),
            "Y" => (Color.FromArgb(255, 247, 181, 0), Colors.Black),
            _ => (Color.FromArgb(255, 90, 90, 90), Colors.White),
        };

        bool round = button.Length == 1;
        var badge = new Border
        {
            MinWidth = 30,
            Height = 30,
            Padding = round ? new Thickness(0) : new Thickness(8, 0, 8, 0),
            CornerRadius = new CornerRadius(round ? 15 : 8),
            Background = new SolidColorBrush(fill),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = button,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 15,
                Foreground = new SolidColorBrush(foreground),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var label = new TextBlock
        {
            Text = text,
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 235, 235, 235)),
        };

        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { badge, label } };
    }
}
