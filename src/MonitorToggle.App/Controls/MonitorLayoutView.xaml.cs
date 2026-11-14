using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MonitorToggle.App.ViewModels;
using MonitorToggle.Core.Model;
using Windows.Foundation;

namespace MonitorToggle.App.Controls;

/// <summary>Draggable map of the monitors in a profile, in the style of Windows Settings > Display.</summary>
public sealed partial class MonitorLayoutView : UserControl
{
    private const double EdgePadding = 16;
    private const double MaxScale = 0.12;
    private const double SnapDistance = 12;

    private readonly Dictionary<MonitorViewModel, (Border Tile, TextBlock Number)> tiles = [];
    private ProfileEditorViewModel? editor;
    private double scale = MaxScale;
    private double originX;
    private double originY;

    private MonitorViewModel? dragging;
    private Point dragStart;
    private (int X, int Y) dragStartPosition;
    private (int X, int Y) dragCurrent;

    public MonitorLayoutView()
    {
        InitializeComponent();
    }

    public void Attach(ProfileEditorViewModel viewModel)
    {
        editor = viewModel;
        viewModel.LayoutChanged += Render;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProfileEditorViewModel.SelectedMonitor))
            {
                UpdateSelection();
            }
        };
        Render();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Render();

    private void Render()
    {
        Surface.Children.Clear();
        tiles.Clear();
        if (editor is null || ActualWidth < 10 || ActualHeight < 10)
        {
            return;
        }

        var enabled = editor.Monitors.Where(m => m.Enabled).ToList();
        EmptyHint.Visibility = enabled.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (enabled.Count == 0)
        {
            return;
        }

        // Leave room around the monitors so one can be dragged to any side of the others.
        double marginX = enabled.Max(m => m.Width) * 0.5;
        double marginY = enabled.Max(m => m.Height) * 0.5;
        double minX = enabled.Min(m => m.X) - marginX;
        double minY = enabled.Min(m => m.Y) - marginY;
        double width = enabled.Max(m => m.X + m.Width) + marginX - minX;
        double height = enabled.Max(m => m.Y + m.Height) + marginY - minY;

        scale = Math.Min(MaxScale, Math.Min((ActualWidth - (2 * EdgePadding)) / width, (ActualHeight - (2 * EdgePadding)) / height));
        originX = ((ActualWidth - (width * scale)) / 2) - (minX * scale);
        originY = ((ActualHeight - (height * scale)) / 2) - (minY * scale);

        foreach (var monitor in enabled)
        {
            var number = new TextBlock
            {
                Text = monitor.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Style = (Style)Resources["TileNumberStyle"],
                FontSize = Math.Clamp(monitor.Height * scale * 0.38, 14, 40),
            };
            var tile = new Border
            {
                Width = monitor.Width * scale,
                Height = monitor.Height * scale,
                Child = new Grid { Children = { new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { number } } } },
                Tag = monitor,
            };
            ToolTipService.SetToolTip(tile, $"{monitor.Name}\n{monitor.Width} × {monitor.Height}");
            tile.PointerPressed += OnPointerPressed;
            tile.PointerMoved += OnPointerMoved;
            tile.PointerReleased += OnPointerReleased;
            tile.PointerCanceled += OnPointerReleased;
            tile.PointerEntered += (_, _) => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
            tile.PointerExited += (_, _) => ProtectedCursor = null;

            Canvas.SetLeft(tile, (monitor.X * scale) + originX);
            Canvas.SetTop(tile, (monitor.Y * scale) + originY);
            Surface.Children.Add(tile);
            tiles[monitor] = (tile, number);
        }

        UpdateSelection();
    }

    private void UpdateSelection()
    {
        foreach (var (monitor, (tile, number)) in tiles)
        {
            bool selected = monitor == editor?.SelectedMonitor;
            tile.Style = (Style)Resources[selected ? "SelectedTileStyle" : "TileStyle"];
            number.Style = (Style)Resources[selected ? "SelectedTileNumberStyle" : "TileNumberStyle"];
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border { Tag: MonitorViewModel monitor } tile || editor is null)
        {
            return;
        }

        editor.Select(monitor);
        dragging = monitor;
        dragStart = e.GetCurrentPoint(Surface).Position;
        dragStartPosition = (monitor.X, monitor.Y);
        dragCurrent = dragStartPosition;
        tile.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (dragging is null || editor is null || sender is not Border tile)
        {
            return;
        }

        var position = e.GetCurrentPoint(Surface).Position;
        int proposedX = dragStartPosition.X + (int)Math.Round((position.X - dragStart.X) / scale);
        int proposedY = dragStartPosition.Y + (int)Math.Round((position.Y - dragStart.Y) / scale);

        var moving = dragging.ToConfig();
        var others = editor.Monitors.Where(m => m.Enabled && m != dragging).Select(m => m.ToConfig());
        dragCurrent = LayoutMath.Snap(moving, proposedX, proposedY, others, (int)(SnapDistance / scale));

        // Only the tile moves while dragging; the layout is committed on release.
        Canvas.SetLeft(tile, (dragCurrent.X * scale) + originX);
        Canvas.SetTop(tile, (dragCurrent.Y * scale) + originY);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (dragging is null || editor is null || sender is not Border tile)
        {
            return;
        }

        var monitor = dragging;
        dragging = null;
        tile.ReleasePointerCapture(e.Pointer);
        if (dragCurrent != dragStartPosition)
        {
            monitor.SetPosition(dragCurrent.X, dragCurrent.Y);
            editor.NotifyMoved();
        }
    }
}
