using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using DisplayProfiles.App.Interop;
using DisplayProfiles.App.Pages;
using Windows.Graphics;
using Windows.Win32;

namespace DisplayProfiles.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(string? page = null)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();
        AppWindow.SetIcon(AppServices.AssetPath("DisplayProfiles.ico"));
        TitleBarIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(AppServices.AssetPath("titlebar.png")));

        double scale = PInvoke.GetDpiForSystem() / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1180 * scale), (int)(800 * scale)));
        WindowMinSize.Apply(new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(this)), 480, 520);

        // The built-in Settings item only exists once the NavigationView has loaded.
        Navigation.Loaded += (_, _) => Navigation.SelectedItem = page?.ToLowerInvariant() switch
        {
            "xbox" => Navigation.MenuItems[1],
            "settings" => Navigation.SettingsItem,
            _ => Navigation.MenuItems[0],
        };
    }

    private void OnDisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        // In the narrowest mode the pane button floats over the content, so leave room for it.
        ContentFrame.Margin = new Thickness(0, args.DisplayMode == NavigationViewDisplayMode.Minimal ? 44 : 0, 0, 0);
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string? tag = args.IsSettingsSelected ? "settings" : args.SelectedItemContainer?.Tag as string;
        ContentFrame.Navigate(tag switch
        {
            "xbox" => typeof(XboxModePage),
            "settings" => typeof(SettingsPage),
            _ => typeof(ProfilesPage),
        });
    }
}
