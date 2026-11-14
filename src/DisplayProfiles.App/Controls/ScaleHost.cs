using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace DisplayProfiles.App.Controls;

/// <summary>
/// Lays its content out as if the window were smaller and then scales it up, so every control, font and icon is enlarged
/// together (a "10-foot" layout) while still being drawn sharply and receiving pointer input at the right place.
/// </summary>
public sealed class ScaleHost : Panel
{
    private double scale = 1;

    public double ScaleFactor
    {
        get => scale;
        set
        {
            if (Math.Abs(value - scale) > 0.001)
            {
                scale = value;
                InvalidateMeasure();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width / scale, availableSize.Height / scale));
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.RenderTransform = new ScaleTransform { ScaleX = scale, ScaleY = scale };
            child.Arrange(new Rect(0, 0, finalSize.Width / scale, finalSize.Height / scale));
        }

        return finalSize;
    }
}
