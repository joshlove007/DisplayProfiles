using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using DisplayProfiles.App.Interop;
using DisplayProfiles.App.Tray;
using DisplayProfiles.Core.Services;
using DisplayProfiles.Core.XboxMode;

namespace DisplayProfiles.App;

[SuppressMessage("Design", "CA1001", Justification = "The tray host lives as long as the process and is disposed in ExitApp.")]
public partial class App : Application
{
    private readonly bool startHidden;
    private readonly string? initialPage;
    private readonly AppInstance mainInstance;
    private readonly DispatcherQueue ui = DispatcherQueue.GetForCurrentThread();
    private ProfileService? profiles;
    private TrayHost? tray;
    private FullScreenExperienceMonitor? xboxMonitor;
    private MainWindow? window;
    private bool exiting;

    public App(bool startHidden, AppInstance mainInstance, string? initialPage = null)
    {
        this.startHidden = startHidden;
        this.mainInstance = mainInstance;
        this.initialPage = initialPage;
        Instance = this;
        InitializeComponent();
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
    }

    internal static App Instance { get; private set; } = null!;

    internal ProfileService Profiles => profiles!;

    internal FullScreenExperienceMonitor? XboxMonitor => xboxMonitor;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MenuTheme.FollowSystemTheme();
        profiles = AppServices.CreateProfiles();
        tray = new TrayHost(profiles, ui, ShowWindow, ExitApp);
        mainInstance.Activated += (_, _) => ui.TryEnqueue(ShowWindow);
        StartXboxModeWatcher();

        if (!startHidden)
        {
            ShowWindow();
        }
    }

    private void StartXboxModeWatcher()
    {
        xboxMonitor = FullScreenExperienceMonitor.TryCreate();
        if (xboxMonitor is null || !xboxMonitor.Start())
        {
            return;
        }

        // Its own service instance: it applies profiles from a background thread.
        var controller = new XboxModeController(xboxMonitor, AppServices.CreateProfiles());
        controller.Applied += (name, result) => ui.TryEnqueue(() =>
        {
            if (!result.Success)
            {
                tray?.Notify("Couldn't switch to " + name, result.Error ?? "Windows did not apply the display settings.");
            }
        });
        controller.Start();
    }

    internal void ShowWindow()
    {
        if (window is null)
        {
            window = new MainWindow(initialPage);
            window.AppWindow.Closing += (_, e) =>
            {
                if (!exiting)
                {
                    // Closing the window keeps the tray icon running.
                    e.Cancel = true;
                    window.AppWindow.Hide();
                }
            };
        }

        window.AppWindow.Show();
        window.Activate();
    }

    private void ExitApp()
    {
        exiting = true;
        xboxMonitor?.Dispose();
        tray?.Dispose();
        window?.Close();
        Exit();
    }
}
