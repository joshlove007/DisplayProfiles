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
    // The button artwork is the gamepad set in Segoe Fluent Icons, which ships with Windows: a ring or pill with the label
    // drawn inside, so the letters are centred by the font itself.
    private static readonly Dictionary<string, string> Glyphs = new()
    {
        ["A"] = "\uF093",
        ["B"] = "\uF094",
        ["Y"] = "\uF095",
        ["X"] = "\uF096",
        ["LB"] = "\uF10C",
        ["RB"] = "\uF10D",
        ["LT"] = "\uF10A",
        ["RT"] = "\uF10B",
        ["DPAD"] = "\uF10E",
    };

    private const double GlyphSize = 26;

    public ControllerHints()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the prompts. A button can name several controls separated by spaces ("LB RB"), which are drawn as separate
    /// buttons. "◄▲▼►" stands for the D-pad.
    /// </summary>
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
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        foreach (string token in button == "◄▲▼►" ? ["DPAD"] : button.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            buttons.Children.Add(Button(token, theme));
        }

        var label = new TextBlock
        {
            Text = text,
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center,
        };

        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { buttons, label } };
    }

    private static Grid Button(string token, ControllerThemePreference theme)
    {
        bool classic = theme == ControllerThemePreference.Classic;
        bool light = theme == ControllerThemePreference.Light;
        var icon = new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Glyph = Glyphs.GetValueOrDefault(token, token),
            FontSize = GlyphSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        bool isFace = token is "A" or "B" or "X" or "Y";
        if (classic && isFace)
        {
            // Classic: a solid coloured disc with the letter ring inside it.
            var (fill, ink) = FaceColors(token, light: false, classic: true);
            icon.Foreground = new SolidColorBrush(ink);
            return new Grid
            {
                Width = 32,
                Height = 32,
                Children =
                {
                    new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 30, Height = 30, Fill = new SolidColorBrush(fill) },
                    icon,
                },
            };
        }

        // Dark and Light: the glyph itself, as a coloured ring and letter.
        icon.Foreground = new SolidColorBrush(isFace ? FaceColors(token, light, classic: false).Fill : Neutral(light));
        return new Grid { Height = 32, MinWidth = 32, Children = { icon } };
    }

    private static (Color Fill, Color Ink) FaceColors(string token, bool light, bool classic) => (token, light, classic) switch
    {
        ("A", _, true) => (Color.FromArgb(255, 16, 124, 16), Colors.White),
        ("B", _, true) => (Color.FromArgb(255, 209, 52, 56), Colors.White),
        ("X", _, true) => (Color.FromArgb(255, 0, 120, 212), Colors.White),
        ("Y", _, true) => (Color.FromArgb(255, 247, 181, 0), Colors.Black),
        ("A", false, _) => (Color.FromArgb(255, 110, 200, 90), Colors.White),
        ("B", false, _) => (Color.FromArgb(255, 235, 60, 60), Colors.White),
        ("X", false, _) => (Color.FromArgb(255, 80, 160, 240), Colors.White),
        ("Y", false, _) => (Color.FromArgb(255, 250, 190, 20), Colors.White),
        ("A", true, _) => (Color.FromArgb(255, 16, 124, 16), Colors.White),
        ("B", true, _) => (Color.FromArgb(255, 200, 30, 35), Colors.White),
        ("X", true, _) => (Color.FromArgb(255, 0, 100, 190), Colors.White),
        _ => (Color.FromArgb(255, 190, 130, 0), Colors.White),
    };

    private static Color Neutral(bool light) => light ? Color.FromArgb(255, 40, 40, 40) : Color.FromArgb(255, 235, 235, 235);
}
