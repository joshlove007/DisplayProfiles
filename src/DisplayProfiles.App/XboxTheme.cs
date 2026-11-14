using DisplayProfiles.Core.Model;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DisplayProfiles.App;

/// <summary>
/// Looks for the controller (Xbox mode) layout. Each look overrides a few application resources; they are removed again
/// when the desktop layout is shown.
/// </summary>
internal static class XboxTheme
{
    private sealed record Look(
        ElementTheme Theme,
        Color[] Accent,
        Color PaneBackground,
        Color? CardBackground,
        Func<Brush> Background);

    private static readonly string[] AccentKeys =
    [
        "SystemAccentColor",
        "SystemAccentColorLight1",
        "SystemAccentColorLight2",
        "SystemAccentColorLight3",
        "SystemAccentColorDark1",
        "SystemAccentColorDark2",
        "SystemAccentColorDark3",
    ];

    private static readonly string[] BrushKeys =
    [
        "NavigationViewContentBackground",
        "NavigationViewContentGridBorderBrush",
        "NavigationViewDefaultPaneBackground",
        "NavigationViewExpandedPaneBackground",
        "CardBackgroundFillColorDefaultBrush",
        "CardBackgroundFillColorSecondaryBrush",
    ];

    private static readonly Dictionary<ControllerThemePreference, Look> Looks = new()
    {
        // Xbox green on a dark green-tinted gradient, like the original Xbox dashboard.
        [ControllerThemePreference.Classic] = new(
            ElementTheme.Dark,
            [Rgb(16, 124, 16), Rgb(45, 158, 45), Rgb(84, 176, 84), Rgb(134, 205, 134), Rgb(14, 108, 14), Rgb(11, 89, 11), Rgb(8, 70, 8)],
            Color.FromArgb(0xB0, 0x0B, 0x12, 0x0B),
            null,
            () => new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Rgb(14, 26, 14), Offset = 0.0 },
                    new GradientStop { Color = Rgb(16, 16, 16), Offset = 0.55 },
                    new GradientStop { Color = Rgb(9, 22, 9), Offset = 1.0 },
                },
            }),

        // Charcoal with light-grey controls, like the current Xbox app on PC.
        [ControllerThemePreference.Dark] = new(
            ElementTheme.Dark,
            [Rgb(209, 212, 217), Rgb(223, 226, 230), Rgb(209, 212, 217), Rgb(232, 234, 237), Rgb(168, 172, 178), Rgb(130, 134, 140), Rgb(96, 100, 106)],
            Rgb(23, 25, 28),
            null,
            () => new SolidColorBrush(Rgb(31, 34, 38))),

        // Light grey with white cards, like the Xbox console's light theme.
        [ControllerThemePreference.Light] = new(
            ElementTheme.Light,
            [Rgb(58, 61, 66), Rgb(80, 84, 90), Rgb(104, 108, 114), Rgb(138, 142, 148), Rgb(48, 51, 56), Rgb(38, 41, 45), Rgb(28, 30, 33)],
            Rgb(206, 206, 206),
            Colors.White,
            () => new SolidColorBrush(Rgb(218, 218, 218))),
    };

    private static ControllerThemePreference current = ControllerThemePreference.Classic;
    private static bool applied;

    /// <summary>The element theme the window content should use for a look.</summary>
    public static ElementTheme ElementThemeFor(ControllerThemePreference theme) => Looks[theme].Theme;

    public static void Apply(ControllerThemePreference theme)
    {
        Remove();

        var look = Looks[theme];
        var resources = Application.Current.Resources;
        for (int i = 0; i < AccentKeys.Length; i++)
        {
            resources[AccentKeys[i]] = look.Accent[i];
        }

        // With no Mica behind the window, the navigation view needs see-through content so the backdrop shows.
        var clear = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        resources["NavigationViewContentBackground"] = clear;
        resources["NavigationViewContentGridBorderBrush"] = clear;
        resources["NavigationViewDefaultPaneBackground"] = new SolidColorBrush(look.PaneBackground);
        resources["NavigationViewExpandedPaneBackground"] = new SolidColorBrush(look.PaneBackground);
        if (look.CardBackground is { } card)
        {
            resources["CardBackgroundFillColorDefaultBrush"] = new SolidColorBrush(card);
            resources["CardBackgroundFillColorSecondaryBrush"] = new SolidColorBrush(Rgb(246, 246, 246));
        }

        current = theme;
        applied = true;
    }

    public static void Remove()
    {
        if (!applied)
        {
            return;
        }

        var resources = Application.Current.Resources;
        foreach (string key in AccentKeys.Concat(BrushKeys))
        {
            resources.Remove(key);
        }

        applied = false;
    }

    /// <summary>The backdrop behind the window content for the active look.</summary>
    public static Brush CreateBackground() => Looks[current].Background();

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);
}
