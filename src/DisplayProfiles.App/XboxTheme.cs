using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DisplayProfiles.App;

/// <summary>Xbox look for controller mode: dark, with Xbox green as the accent colour. Removed again when leaving the mode.</summary>
internal static class XboxTheme
{
    private static readonly Dictionary<string, Color> AccentColors = new()
    {
        ["SystemAccentColor"] = Color.FromArgb(255, 16, 124, 16),
        ["SystemAccentColorLight1"] = Color.FromArgb(255, 45, 158, 45),
        ["SystemAccentColorLight2"] = Color.FromArgb(255, 84, 176, 84),
        ["SystemAccentColorLight3"] = Color.FromArgb(255, 134, 205, 134),
        ["SystemAccentColorDark1"] = Color.FromArgb(255, 14, 108, 14),
        ["SystemAccentColorDark2"] = Color.FromArgb(255, 11, 89, 11),
        ["SystemAccentColorDark3"] = Color.FromArgb(255, 8, 70, 8),
    };

    // With no Mica behind the window, the navigation view needs see-through surfaces so the gradient shows.
    private static readonly Dictionary<string, Color> SurfaceColors = new()
    {
        ["NavigationViewContentBackground"] = Color.FromArgb(0, 0, 0, 0),
        ["NavigationViewContentGridBorderBrush"] = Color.FromArgb(0, 0, 0, 0),
        ["NavigationViewDefaultPaneBackground"] = Color.FromArgb(0xB0, 0x0B, 0x12, 0x0B),
        ["NavigationViewExpandedPaneBackground"] = Color.FromArgb(0xB0, 0x0B, 0x12, 0x0B),
    };

    private static bool applied;

    public static void Apply()
    {
        if (applied)
        {
            return;
        }

        var resources = Application.Current.Resources;
        foreach (var (key, color) in AccentColors)
        {
            resources[key] = color;
        }

        foreach (var (key, color) in SurfaceColors)
        {
            resources[key] = new SolidColorBrush(color);
        }

        applied = true;
    }

    public static void Remove()
    {
        if (!applied)
        {
            return;
        }

        var resources = Application.Current.Resources;
        foreach (string key in AccentColors.Keys.Concat(SurfaceColors.Keys))
        {
            resources.Remove(key);
        }

        applied = false;
    }

    /// <summary>A dark, slightly green backdrop like the Xbox dashboard.</summary>
    public static Brush CreateBackground() => new LinearGradientBrush
    {
        StartPoint = new Windows.Foundation.Point(0, 0),
        EndPoint = new Windows.Foundation.Point(1, 1),
        GradientStops =
        {
            new GradientStop { Color = Color.FromArgb(255, 14, 26, 14), Offset = 0.0 },
            new GradientStop { Color = Color.FromArgb(255, 16, 16, 16), Offset = 0.55 },
            new GradientStop { Color = Color.FromArgb(255, 9, 22, 9), Offset = 1.0 },
        },
    };
}
