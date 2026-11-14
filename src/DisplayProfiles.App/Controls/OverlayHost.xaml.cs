using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DisplayProfiles.App.Controls;

/// <summary>
/// An in-window replacement for pop-up lists and dialogs. It lives inside the (scaled) window, so it is large enough for
/// a TV and can be driven entirely with a controller; ordinary pop-ups can't be scaled with the rest of the UI.
/// </summary>
public sealed partial class OverlayHost : UserControl
{
    private TaskCompletionSource<int>? pending;
    private int cancelResult = -1;
    private UIElement? returnFocus;
    private DispatcherQueueTimer? countdownTimer;

    public OverlayHost()
    {
        InitializeComponent();
        Current = this;
    }

    public static OverlayHost? Current { get; private set; }

    /// <summary>The part of the window that must stop reacting while the overlay is open.</summary>
    public UIElement? Underlying { get; set; }

    public bool IsOpen => pending is not null;

    /// <summary>Raised with true when the overlay opens and false when it closes.</summary>
    public event Action<bool>? OpenChanged;

    /// <summary>Shows a list of choices and returns the chosen index, or null if it was cancelled.</summary>
    public async Task<int?> ShowChoiceAsync(string title, IReadOnlyList<string> items, int selected)
    {
        BeginShow(title, message: null);
        ChoicePanel.Children.Clear();
        Button? focusTarget = null;

        for (int i = 0; i < items.Count; i++)
        {
            int index = i;
            bool isSelected = i == selected;
            var content = new Grid { ColumnSpacing = 12 };
            content.ColumnDefinitions.Add(new ColumnDefinition());
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.Children.Add(new TextBlock { Text = items[i], FontSize = 18, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            if (isSelected)
            {
                var check = new FontIcon { Glyph = "\uE73E", FontSize = 18 };
                Grid.SetColumn(check, 1);
                content.Children.Add(check);
            }

            var button = new Button
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(18, 12, 18, 12),
                MinHeight = 52,
            };
            AutomationHelper.SetName(button, items[i]);
            if (isSelected)
            {
                button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                focusTarget = button;
            }

            button.Click += (_, _) => Complete(index);
            ChoicePanel.Children.Add(button);
        }

        ChoiceScroll.Visibility = Visibility.Visible;
        ButtonRow.Visibility = Visibility.Collapsed;
        cancelResult = -1;
        int result = await Open(focusTarget ?? ChoicePanel.Children.OfType<Button>().FirstOrDefault());
        return result < 0 ? null : result;
    }

    /// <summary>Shows a message with a primary and a secondary button; returns true if the primary one was chosen.</summary>
    /// <param name="countdownSeconds">When set, the secondary choice is taken automatically after this long.</param>
    /// <param name="countdownMessage">Message text for the remaining seconds (replaces <paramref name="message"/> while counting).</param>
    public async Task<bool> ShowConfirmAsync(
        string title, string message, string primaryText, string secondaryText, bool defaultToSecondary = false,
        int? countdownSeconds = null, Func<int, string>? countdownMessage = null)
    {
        BeginShow(title, message);
        ChoiceScroll.Visibility = Visibility.Collapsed;
        ButtonRow.Children.Clear();

        var primary = new Button { Content = primaryText, Style = (Style)Application.Current.Resources["AccentButtonStyle"], MinWidth = 150, MinHeight = 48, FontSize = 18 };
        var secondary = new Button { Content = secondaryText, MinWidth = 150, MinHeight = 48, FontSize = 18 };
        primary.Click += (_, _) => Complete(1);
        secondary.Click += (_, _) => Complete(0);
        ButtonRow.Children.Add(secondary);
        ButtonRow.Children.Add(primary);
        ButtonRow.Visibility = Visibility.Visible;
        cancelResult = 0;

        if (countdownSeconds is { } seconds && countdownMessage is not null)
        {
            int remaining = seconds;
            MessageText.Text = countdownMessage(remaining);
            MessageText.Visibility = Visibility.Visible;
            countdownTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.IsRepeating = true;
            countdownTimer.Tick += (_, _) =>
            {
                remaining--;
                if (remaining <= 0)
                {
                    Complete(0);
                }
                else
                {
                    MessageText.Text = countdownMessage(remaining);
                }
            };
            countdownTimer.Start();
        }

        int result = await Open(defaultToSecondary ? secondary : primary);
        return result == 1;
    }

    /// <summary>B / Escape: closes the overlay with its "cancel" answer.</summary>
    public void Cancel()
    {
        if (pending is not null)
        {
            Complete(cancelResult);
        }
    }

    private void BeginShow(string title, string? message)
    {
        TitleText.Text = title;
        MessageText.Text = message ?? string.Empty;
        MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task<int> Open(UIElement? focusTarget)
    {
        returnFocus = FocusManager.GetFocusedElement(XamlRoot) as UIElement;
        pending = new TaskCompletionSource<int>();
        if (Underlying is not null)
        {
            Underlying.IsHitTestVisible = false;
            if (Underlying is Control control)
            {
                control.IsEnabled = false;
            }
        }

        Layer.Visibility = Visibility.Visible;
        OpenChanged?.Invoke(true);
        UpdateLayout();
        if (focusTarget is Control target)
        {
            // Focus again once layout has run so the focus ring is drawn on the freshly shown item.
            target.Focus(FocusState.Keyboard);
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (pending is not null)
                {
                    target.Focus(FocusState.Keyboard);
                }
            });
        }

        return await pending.Task;
    }

    private void Complete(int result)
    {
        countdownTimer?.Stop();
        countdownTimer = null;
        var task = pending;
        pending = null;
        Layer.Visibility = Visibility.Collapsed;
        if (Underlying is not null)
        {
            Underlying.IsHitTestVisible = true;
            if (Underlying is Control control)
            {
                control.IsEnabled = true;
            }
        }

        (returnFocus as Control)?.Focus(FocusState.Keyboard);
        OpenChanged?.Invoke(false);
        task?.TrySetResult(result);
    }
}

internal static class AutomationHelper
{
    public static void SetName(DependencyObject element, string name) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, name);
}
