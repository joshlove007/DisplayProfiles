using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WinRT;

namespace DisplayProfiles.Widget;

sealed partial class App : Application
{
    // Must stay referenced for the widget's lifetime: it holds the connection with Game Bar.
    private XboxGameBarWidget? widget;

    public App()
    {
        // The projection assembly is not named after its root namespace, so CsWinRT can't find it on its own when it
        // materializes Game Bar objects (such as the activation arguments); registering it makes typed wrappers resolve.
        ComWrappersSupport.RegisterProjectionAssembly(typeof(XboxGameBarWidget).Assembly);
        InitializeComponent();
    }

    internal static void Log(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(ApplicationData.Current.LocalFolder.Path, "widget.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}\r\n");
        }
        catch
        {
            // Logging must never take the widget down.
        }
    }

    protected override void OnActivated(IActivatedEventArgs args)
    {
        try
        {
            XboxGameBarWidgetActivatedEventArgs? widgetArgs = null;
            if (args.Kind == ActivationKind.Protocol &&
                args is IProtocolActivatedEventArgs protocolArgs &&
                protocolArgs.Uri.Scheme == "ms-gamebarwidget")
            {
                widgetArgs = args as XboxGameBarWidgetActivatedEventArgs
                    ?? XboxGameBarWidgetActivatedEventArgs.FromAbi(((IWinRTObject)args).NativeObject.ThisPtr);
            }

            if (widgetArgs is null || !widgetArgs.IsLaunchActivation)
            {
                return;
            }

            var rootFrame = new Frame();
            Window.Current.Content = rootFrame;
            widget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
            rootFrame.Navigate(typeof(WidgetPage), widget);
            Window.Current.Closed += (_, _) => widget = null;
            Window.Current.Activate();
            Log("widget activated");
        }
        catch (Exception ex)
        {
            Log($"OnActivated failed: {ex}");
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        // Started outside Game Bar: show a hint.
        if (Window.Current.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            Window.Current.Content = rootFrame;
        }

        if (!e.PrelaunchActivated)
        {
            if (rootFrame.Content is null)
            {
                rootFrame.Navigate(typeof(MainPage));
            }

            Window.Current.Activate();
        }
    }
}
