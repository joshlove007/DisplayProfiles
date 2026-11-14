using DisplayProfiles.App.ViewModels;
using DisplayProfiles.Core.Input;
using DisplayProfiles.Core.Model;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace DisplayProfiles.App.Controls;

/// <summary>
/// Map of the monitors in a profile, in the style of Windows Settings > Display. Tiles can be dragged with a mouse or touch,
/// or picked up with the keyboard/controller (A), moved one valid position at a time with the arrows/D-pad, and dropped (A).
/// </summary>
public sealed partial class MonitorLayoutView : UserControl, IGamepadHandler
{
    private const double EdgePadding = 16;
    private const double MaxScale = 0.12;
    private const double SnapDistance = 12;

    // Tiles are drawn slightly smaller than their monitors so touching displays show a thin gap, as in Windows Settings.
    private const double TileGap = 3;

    private readonly Dictionary<MonitorViewModel, (ContentControl Tile, TextBlock Number)> tiles = [];
    private ProfileEditorViewModel? editor;
    private double scale = MaxScale;
    private double originX;
    private double originY;

    private MonitorViewModel? dragging;
    private Point dragStart;
    private (int X, int Y) dragStartPosition;
    private (int X, int Y) dragCurrent;

    private MonitorViewModel? grabbed;
    private (int X, int Y) grabStart;
    private (int X, int Y) grabSlot;
    private MonitorViewModel? focusAfterRender;

    public MonitorLayoutView()
    {
        InitializeComponent();
    }

    /// <summary>Raised with true when a monitor is picked up with the keyboard/controller and false when it is put down.</summary>
    public event Action<bool>? GrabChanged;

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
        // Rendering replaces the tiles, so keep focus on the same display if it was on one.
        focusAfterRender ??= tiles.FirstOrDefault(t => t.Value.Tile.FocusState != FocusState.Unfocused).Key;
        Surface.Children.Clear();
        tiles.Clear();
        if (grabbed is not null)
        {
            grabbed = null;
            GrabChanged?.Invoke(false);
        }

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
        // A controller moves a display to slots touching its neighbours, which can be a whole display away.
        double margin = ControllerUi.IsActive ? 1.0 : 0.5;
        double marginX = enabled.Max(m => m.Width) * margin;
        double marginY = enabled.Max(m => m.Height) * margin;
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
            var tile = new ContentControl
            {
                Width = Math.Max(1, (monitor.Width * scale) - TileGap),
                Height = Math.Max(1, (monitor.Height * scale) - TileGap),
                Content = number,
                Tag = monitor,
            };
            AutomationHelper.SetName(tile, $"Display {monitor.Number}, {monitor.Name}");
            if (!ControllerUi.IsActive)
            {
                ToolTipService.SetToolTip(tile, $"{monitor.Name}\n{monitor.Width} × {monitor.Height}");
            }
            tile.PointerPressed += OnPointerPressed;
            tile.PointerMoved += OnPointerMoved;
            tile.PointerReleased += OnPointerReleased;
            tile.PointerCanceled += OnPointerReleased;
            tile.PointerEntered += (_, _) => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
            tile.PointerExited += (_, _) => ProtectedCursor = null;
            tile.GotFocus += (_, _) => editor.Select(monitor);
            tile.KeyDown += OnTileKeyDown;

            Canvas.SetLeft(tile, (monitor.X * scale) + originX + (TileGap / 2));
            Canvas.SetTop(tile, (monitor.Y * scale) + originY + (TileGap / 2));
            Surface.Children.Add(tile);
            tiles[monitor] = (tile, number);
        }

        UpdateSelection();

        if (focusAfterRender is { } target && tiles.TryGetValue(target, out var focusTile))
        {
            // Freshly added tiles can only take focus after layout, and a later render may replace them first.
            var toFocus = focusTile.Tile;
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                bool ok = toFocus.Focus(FocusState.Keyboard);
                if (ok && focusAfterRender == target)
                {
                    focusAfterRender = null;
                }
            });
        }
    }

    private void UpdateSelection()
    {
        foreach (var (monitor, (tile, number)) in tiles)
        {
            bool selected = monitor == editor?.SelectedMonitor;
            string tileStyle = monitor == grabbed ? "GrabbedTileStyle" : selected ? "SelectedTileStyle" : "TileStyle";
            tile.Style = (Style)Resources[tileStyle];
            number.Style = (Style)Resources[selected ? "SelectedTileNumberStyle" : "TileNumberStyle"];
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ContentControl { Tag: MonitorViewModel monitor } tile || editor is null)
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
        if (dragging is null || editor is null || sender is not ContentControl tile)
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
        Place(tile, dragCurrent);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (dragging is null || editor is null || sender is not ContentControl tile)
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

    private void Place(ContentControl tile, (int X, int Y) position)
    {
        Canvas.SetLeft(tile, (position.X * scale) + originX + (TileGap / 2));
        Canvas.SetTop(tile, (position.Y * scale) + originY + (TileGap / 2));
    }

    // Keyboard on the desktop: Enter/Space picks up and drops, arrows move, Escape cancels.
    private void OnTileKeyDown(object sender, KeyRoutedEventArgs e)
    {
        GamepadAction? action = e.Key switch
        {
            VirtualKey.Enter or VirtualKey.Space => GamepadAction.Accept,
            VirtualKey.Escape when grabbed is not null => GamepadAction.Back,
            VirtualKey.Up when grabbed is not null => GamepadAction.Up,
            VirtualKey.Down when grabbed is not null => GamepadAction.Down,
            VirtualKey.Left when grabbed is not null => GamepadAction.Left,
            VirtualKey.Right when grabbed is not null => GamepadAction.Right,
            _ => null,
        };

        if (action is { } value && HandleGamepad(value, sender as FrameworkElement))
        {
            e.Handled = true;
        }
    }

    public bool HandleGamepad(GamepadAction action, FrameworkElement? focused)
    {
        if (grabbed is not null)
        {
            switch (action)
            {
                case GamepadAction.Up: MoveGrab(MoveDirection.Up); break;
                case GamepadAction.Down: MoveGrab(MoveDirection.Down); break;
                case GamepadAction.Left: MoveGrab(MoveDirection.Left); break;
                case GamepadAction.Right: MoveGrab(MoveDirection.Right); break;
                case GamepadAction.Accept: DropGrab(); break;
                case GamepadAction.Back: CancelGrab(); break;
                case GamepadAction.ScrollUp or GamepadAction.ScrollDown: return false;
            }

            return true;
        }

        if (action == GamepadAction.Accept && tiles.FirstOrDefault(t => ReferenceEquals(t.Value.Tile, focused)).Key is { } monitor)
        {
            BeginGrab(monitor);
            return true;
        }

        return false;
    }

    private void BeginGrab(MonitorViewModel monitor)
    {
        grabbed = monitor;
        grabStart = (monitor.X, monitor.Y);
        grabSlot = grabStart;
        UpdateSelection();
        GrabChanged?.Invoke(true);
    }

    private void MoveGrab(MoveDirection direction)
    {
        if (grabbed is null || editor is null)
        {
            return;
        }

        var others = editor.Monitors.Where(m => m.Enabled && m != grabbed).Select(m => m.ToConfig());
        var slots = LayoutMath.AttachSlots(grabbed.ToConfig(), others);
        if (LayoutMath.NextSlot(grabSlot, slots, direction) is { } next)
        {
            grabSlot = next;
            Place(tiles[grabbed].Tile, next);
        }
    }

    private void DropGrab()
    {
        if (grabbed is null || editor is null)
        {
            return;
        }

        var monitor = grabbed;
        focusAfterRender = monitor;
        grabbed = null;
        GrabChanged?.Invoke(false);
        if (grabSlot != grabStart)
        {
            monitor.SetPosition(grabSlot.X, grabSlot.Y);
            editor.NotifyMoved();
        }
        else
        {
            UpdateSelection();
            focusAfterRender = null;
        }
    }

    private void CancelGrab()
    {
        if (grabbed is null)
        {
            return;
        }

        Place(tiles[grabbed].Tile, grabStart);
        grabbed = null;
        UpdateSelection();
        GrabChanged?.Invoke(false);
    }
}
