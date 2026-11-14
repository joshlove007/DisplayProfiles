using DisplayProfiles.App.Controls;
using DisplayProfiles.App.Input;
using DisplayProfiles.App.Interop;
using DisplayProfiles.App.Pages;
using DisplayProfiles.Core.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using Windows.Win32;

namespace DisplayProfiles.App;

public sealed partial class MainWindow : Window
{
    private readonly bool controller = ControllerUi.IsActive;
    private readonly GamepadNavigator? gamepad;
    private readonly Windows.Win32.Foundation.HWND handle;

    public MainWindow(string? page = null)
    {
        InitializeComponent();
        handle = new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(this));

        AppWindow.SetIcon(AppServices.AssetPath("DisplayProfiles.ico"));
        TitleBarIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(AppServices.AssetPath("titlebar.png")));
        Overlay.Underlying = Navigation;

        if (controller)
        {
            SetUpControllerLook();
            gamepad = new GamepadNavigator(this);
            WindowRoot.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), handledEventsToo: true);
            WindowRoot.AddHandler(UIElement.PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp), handledEventsToo: true);
            Overlay.OpenChanged += _ => RefreshHints();
            Closed += (_, _) => gamepad.Stop();
        }
        else
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            SystemBackdrop = new MicaBackdrop();
            double scale = PInvoke.GetDpiForSystem() / 96.0;
            AppWindow.Resize(new SizeInt32((int)(1180 * scale), (int)(800 * scale)));
            WindowMinSize.Apply(handle, 480, 520);
        }

        // The built-in Settings item only exists once the NavigationView has loaded.
        Navigation.Loaded += (_, _) =>
        {
            Navigation.SelectedItem = page?.ToLowerInvariant() switch
            {
                "xbox" => Navigation.MenuItems[1],
                "settings" => Navigation.SettingsItem,
                _ => Navigation.MenuItems[0],
            };

            if (controller)
            {
                FocusNavigation();
            }
        };
    }

    /// <summary>True while this window is the one the user is looking at, so controller input is meant for it.</summary>
    internal bool IsForeground => AppWindow.IsVisible && PInvoke.GetForegroundWindow() == handle;

    internal bool IsController => controller;

    /// <summary>"profiles", "xbox" or "settings": the section currently shown.</summary>
    internal string CurrentTag { get; private set; } = "profiles";

    internal OverlayHost OverlayLayer => Overlay;

    internal IControllerPage? CurrentPage => ContentFrame.Content as IControllerPage;

    private void SetUpControllerLook()
    {
        // Xbox mode: dark, green, no title bar or caption buttons, and (unless --windowed) the whole screen.
        WindowRoot.RequestedTheme = ElementTheme.Dark;
        WindowRoot.Background = XboxTheme.CreateBackground();
        AppTitleBar.Visibility = Visibility.Collapsed;
        Hints.Visibility = Visibility.Visible;
        Navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
        Navigation.OpenPaneLength = 300;
        Navigation.IsPaneToggleButtonVisible = false;

        if (ControllerUi.Windowed)
        {
            double scale = PInvoke.GetDpiForSystem() / 96.0;
            AppWindow.Resize(new SizeInt32((int)(1280 * scale), (int)(800 * scale)));
        }
        else
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (controller)
        {
            Scaler.ScaleFactor = ControllerUi.ScaleFor(e.NewSize.Height);
        }
    }

    private void OnDisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        // In the narrowest mode the pane button floats over the content, so leave room for it.
        ContentFrame.Margin = new Thickness(0, args.DisplayMode == NavigationViewDisplayMode.Minimal ? 44 : 0, 0, 0);
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string? tag = args.IsSettingsSelected ? "settings" : args.SelectedItemContainer?.Tag as string;
        CurrentTag = tag ?? "profiles";
        ContentFrame.Navigate(tag switch
        {
            "xbox" => typeof(XboxModePage),
            "settings" => typeof(SettingsPage),
            _ => typeof(ProfilesPage),
        });
    }

    private void OnContentNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e) => RefreshHints();

    /// <summary>Updates the button prompts along the bottom to match what the controller can do right now.</summary>
    internal void RefreshHints()
    {
        if (!controller)
        {
            return;
        }

        if (Overlay.IsOpen)
        {
            Hints.SetHints([("A", "Select"), ("B", "Cancel")]);
            return;
        }

        Hints.SetHints(CurrentPage?.Hints ?? [("A", "Select"), ("B", "Back"), ("LB RB", "Sections")]);
    }

    /// <summary>Replaces the prompts temporarily, for example while a monitor is being moved.</summary>
    internal void ShowHints(IEnumerable<(string Button, string Text)> hints)
    {
        if (controller)
        {
            Hints.SetHints(hints);
        }
    }

    /// <summary>Switches to the previous or next section (LB / RB).</summary>
    internal void CycleSection(int delta)
    {
        var items = Navigation.MenuItems.OfType<NavigationViewItem>().Cast<object>().Append(Navigation.SettingsItem).ToList();
        int index = items.IndexOf(Navigation.SelectedItem);
        int next = (index + delta + items.Count) % items.Count;
        Navigation.SelectedItem = items[next];
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FocusNavigation);
    }

    internal void FocusNavigation()
    {
        if (Navigation.SelectedItem is object item &&
            (Navigation.ContainerFromMenuItem(item) as Control ?? Navigation.SettingsItem as Control) is { } container)
        {
            container.Focus(FocusState.Keyboard);
        }
    }

    /// <summary>B: from the page go back to the navigation list; from the navigation list leave to the Xbox home (hide the window).</summary>
    internal void GoBack(FrameworkElement? focused)
    {
        for (DependencyObject? d = focused; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (ReferenceEquals(d, ContentFrame))
            {
                FocusNavigation();
                return;
            }
        }

        AppWindow.Hide();
    }

    // The press was already acted on, so the release must not click whatever took focus in the meantime.
    private void OnPreviewKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Space && FocusManager.GetFocusedElement(WindowRoot.XamlRoot) is not TextBox)
        {
            e.Handled = true;
        }
    }

    // Keyboard stand-ins for the controller buttons, so controller mode also works with a keyboard.
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var focused = FocusManager.GetFocusedElement(WindowRoot.XamlRoot);
        bool typing = focused is TextBox;
        bool caretAtStart = focused is TextBox { SelectionStart: 0 };
        bool caretAtEnd = focused is TextBox box && box.SelectionStart + box.SelectionLength == box.Text.Length;
        GamepadAction? action = e.Key switch
        {
            VirtualKey.Escape => GamepadAction.Back,
            VirtualKey.PageUp => GamepadAction.PreviousSection,
            VirtualKey.PageDown => GamepadAction.NextSection,
            VirtualKey.Up when focused is not TextBox { AcceptsReturn: true } => GamepadAction.Up,
            VirtualKey.Down when focused is not TextBox { AcceptsReturn: true } => GamepadAction.Down,
            VirtualKey.Left when !typing || caretAtStart => GamepadAction.Left,
            VirtualKey.Right when !typing || caretAtEnd => GamepadAction.Right,
            VirtualKey.Enter or VirtualKey.Space when !typing => GamepadAction.Accept,
            VirtualKey.Y when !typing => GamepadAction.Primary,
            _ => null,
        };

        if (action is { } value && gamepad is not null)
        {
            e.Handled = true;
            gamepad.Dispatch(value);
        }
    }
}
