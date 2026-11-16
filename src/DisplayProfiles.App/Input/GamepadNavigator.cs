using System.Diagnostics;
using DisplayProfiles.Core.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DisplayProfiles.App.Input;

/// <summary>
/// Drives the UI with a controller: the D-pad and left stick move focus, A presses the focused control, B goes back,
/// LB/RB switch sections, Y applies the profile and the right stick scrolls. Presses are ignored unless the app's window is
/// in front, so a game running on top never also controls this app.
/// </summary>
internal sealed class GamepadNavigator
{
    private const double ScrollStep = 150;

    private readonly MainWindow window;
    private readonly DispatcherQueueTimer timer;
    private readonly GamepadRepeater repeater = new();
    private readonly XInputPoller poller = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private bool primed;

    public GamepadNavigator(MainWindow window)
    {
        this.window = window;
        timer = window.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(16);
        timer.IsRepeating = true;
        timer.Tick += (_, _) => Poll();
        timer.Start();
    }

    public void Stop() => timer.Stop();

    private void Poll()
    {
        if (!poller.TryRead(out var state))
        {
            primed = false;
            return;
        }

        if (!window.IsForeground)
        {
            primed = false;
            return;
        }

        var actions = repeater.Update(state, clock.Elapsed);

        // The first reading after coming to the front only records which buttons are already held.
        if (!primed)
        {
            primed = true;
            return;
        }

        foreach (var action in actions)
        {
            Dispatch(action);
        }
    }

    /// <summary>Performs an action as if the matching controller button was pressed. Keyboard shortcuts use this too.</summary>
    public void Dispatch(GamepadAction action)
    {
        var root = window.Content?.XamlRoot;
        if (root is null)
        {
            return;
        }

        var focused = FocusManager.GetFocusedElement(root) as FrameworkElement;
        var overlay = window.OverlayLayer;

        if (!overlay.IsOpen)
        {
            for (DependencyObject? d = focused; d is not null; d = VisualTreeHelper.GetParent(d))
            {
                if (d is IGamepadHandler handler && handler.HandleGamepad(action, focused))
                {
                    return;
                }
            }
        }

        switch (action)
        {
            case GamepadAction.Up:
            case GamepadAction.Down:
            case GamepadAction.Left:
            case GamepadAction.Right:
                MoveFocus(overlay.IsOpen ? overlay : (FrameworkElement)root.Content, focused, action);
                break;
            case GamepadAction.Accept:
                Activate(focused);
                break;
            case GamepadAction.Back:
                if (overlay.IsOpen)
                {
                    overlay.Cancel();
                }
                else
                {
                    window.GoBack(focused);
                }

                break;
            case GamepadAction.PreviousSection when !overlay.IsOpen:
                window.CycleSection(-1);
                break;
            case GamepadAction.NextSection when !overlay.IsOpen:
                window.CycleSection(1);
                break;
            case GamepadAction.Primary when !overlay.IsOpen:
                window.CurrentPage?.OnPrimaryAction();
                break;
            case GamepadAction.Secondary when !overlay.IsOpen:
                window.CurrentPage?.OnSecondaryAction();
                break;
            case GamepadAction.ScrollUp:
                Scroll(focused, -ScrollStep);
                break;
            case GamepadAction.ScrollDown:
                Scroll(focused, ScrollStep);
                break;
        }
    }

    private void MoveFocus(FrameworkElement root, FrameworkElement? focused, GamepadAction action)
    {
        var next = SpatialFocus.FindNext(root, focused, action);
        if (next is null)
        {
            return;
        }

        next.Focus(FocusState.Keyboard);
        next.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
    }

    /// <summary>Presses the focused control the way a click or tap would.</summary>
    internal static bool Activate(FrameworkElement? element)
    {
        if (element is null)
        {
            return false;
        }

        if (element is Microsoft.UI.Xaml.Controls.Primitives.SelectorItem selectorItem)
        {
            selectorItem.IsSelected = true;
            return true;
        }

        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(element);
        if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
        {
            invoke.Invoke();
            return true;
        }

        if (peer?.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle)
        {
            toggle.Toggle();
            return true;
        }

        if (peer?.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider selection)
        {
            selection.Select();
            return true;
        }

        if (peer?.GetPattern(PatternInterface.ExpandCollapse) is IExpandCollapseProvider expander)
        {
            if (expander.ExpandCollapseState == ExpandCollapseState.Collapsed)
            {
                expander.Expand();
            }
            else
            {
                expander.Collapse();
            }

            return true;
        }

        return false;
    }

    private static void Scroll(FrameworkElement? focused, double delta)
    {
        for (DependencyObject? d = focused; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is ScrollViewer { ScrollableHeight: > 0 } viewer)
            {
                viewer.ChangeView(null, viewer.VerticalOffset + delta, null, disableAnimation: true);
                return;
            }
        }
    }
}
