using DisplayProfiles.Core.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace DisplayProfiles.App.Input;

/// <summary>
/// Directional focus movement for the controller. Unlike the built-in XY focus this also considers controls that are
/// scrolled out of view, and it can step into and back out of container controls such as expanders.
/// </summary>
internal static class SpatialFocus
{
    private const double Tolerance = 4;

    public static Control? FindNext(FrameworkElement root, DependencyObject? current, GamepadAction action)
    {
        var candidates = new List<Control>();
        Collect(root, candidates);
        if (candidates.Count == 0)
        {
            return null;
        }

        if (current is not Control from || !candidates.Contains(from))
        {
            return candidates
                .Select(c => (Control: c, Bounds: BoundsOf(c, root)))
                .OrderBy(x => x.Bounds.Top)
                .ThenBy(x => x.Bounds.Left)
                .First().Control;
        }

        Rect origin = BoundsOf(from, root);
        Control? best = null;
        double bestScore = double.MaxValue;

        foreach (var candidate in candidates)
        {
            if (ReferenceEquals(candidate, from))
            {
                continue;
            }

            double score = Score(action, origin, BoundsOf(candidate, root), IsAncestor(candidate, from), IsAncestor(from, candidate));
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static double Score(GamepadAction action, Rect from, Rect to, bool candidateIsAncestor, bool candidateIsDescendant)
    {
        bool vertical = action is GamepadAction.Up or GamepadAction.Down;
        bool forward = action is GamepadAction.Down or GamepadAction.Right;

        if (candidateIsDescendant)
        {
            // Down or Right steps into a container, nearest child first.
            return forward ? 0.1 * (Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y)) : double.MaxValue;
        }

        if (candidateIsAncestor)
        {
            // Up or Left steps back out to the container.
            return forward ? double.MaxValue : vertical ? Math.Max(0, from.Y - to.Y) : Math.Max(0, from.X - to.X);
        }

        double along;
        double across;
        double centerOffset;
        if (vertical)
        {
            along = forward ? to.Top - from.Bottom : from.Top - to.Bottom;
            across = Math.Max(0, Math.Max(to.Left - from.Right, from.Left - to.Right));
            centerOffset = Math.Abs((to.X + (to.Width / 2)) - (from.X + (from.Width / 2)));
        }
        else
        {
            along = forward ? to.Left - from.Right : from.Left - to.Right;
            across = Math.Max(0, Math.Max(to.Top - from.Bottom, from.Top - to.Bottom));
            centerOffset = Math.Abs((to.Y + (to.Height / 2)) - (from.Y + (from.Height / 2)));
        }

        if (along < -Tolerance)
        {
            return double.MaxValue;
        }

        return Math.Max(0, along) + (6 * across) + (0.25 * centerOffset);
    }

    private static Rect BoundsOf(FrameworkElement element, UIElement root) =>
        element.TransformToVisual(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    private static bool IsAncestor(DependencyObject ancestor, DependencyObject node)
    {
        for (var parent = VisualTreeHelper.GetParent(node); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static void Collect(DependencyObject node, List<Control> result)
    {
        if (node is UIElement { Visibility: Visibility.Collapsed } or UIElement { Opacity: 0 })
        {
            return;
        }

        if (node is Control { IsTabStop: true, IsEnabled: true, ActualWidth: > 0, ActualHeight: > 0 } control && control is not ItemsControl)
        {
            result.Add(control);
        }

        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            Collect(VisualTreeHelper.GetChild(node, i), result);
        }
    }
}
