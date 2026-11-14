using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using DisplayProfiles.App.Interop;
using DisplayProfiles.App.Tray;
using DisplayProfiles.Core.Model;
using DisplayProfiles.Core.Services;
using DisplayProfiles.Core.XboxMode;

namespace DisplayProfiles.App;

[SuppressMessage("Design", "CA1001", Justification = "The tray host lives as long as the process and is disposed in ExitApp.")]
public partial class App : Application
{
    private readonly bool startHidden;
    private string? initialPage;
    private readonly AppInstance mainInstance;
    private readonly DispatcherQueue ui = DispatcherQueue.GetForCurrentThread();
    private ProfileService? profiles;
    private TrayHost? tray;
    private FullScreenExperienceMonitor? xboxMonitor;
    private MainWindow? window;
    private bool exiting;
    private bool closingForReal;

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

    internal MainWindow? MainWindow => window;

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

        // Entering or leaving Xbox mode may switch the app between its desktop and controller looks.
        xboxMonitor.StateChanged += _ => ui.TryEnqueue(RefreshUiMode);

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

    /// <summary>Whether the large, controller-friendly look should be used right now.</summary>
    private bool WantsController()
    {
        if (ControllerUi.Forced is { } forced)
        {
            return forced;
        }

        profiles!.Reload();
        return profiles.Data.Settings.ControllerMode switch
        {
            ControllerModePreference.Always => true,
            ControllerModePreference.Never => false,
            _ => xboxMonitor?.IsActive == true,
        };
    }

    /// <summary>Rebuilds the window if the desired look (desktop or controller) no longer matches the one on screen.</summary>
    internal void RefreshUiMode()
    {
        if (window is null || window.IsController == WantsController())
        {
            return;
        }

        RebuildWindow();
    }

    /// <summary>Recreates the window, keeping the page and visibility, so a changed look is applied from scratch.</summary>
    internal void RebuildWindow()
    {
        if (window is null)
        {
            return;
        }

        bool visible = window.AppWindow.IsVisible;
        string page = window.CurrentTag;
        CloseWindow();
        if (visible)
        {
            initialPage = page;
            ShowWindow();
        }
    }

    private void CloseWindow()
    {
        closingForReal = true;
        window?.Close();
        window = null;
        closingForReal = false;
    }

    internal void ShowWindow()
    {
        if (window is not null && window.IsController != WantsController())
        {
            initialPage = window.CurrentTag;
            CloseWindow();
        }

        if (window is null)
        {
            bool controller = WantsController();
            ControllerUi.SetActive(controller);
            if (controller)
            {
                profiles!.Reload();
                XboxTheme.Apply(profiles.Data.Settings.ControllerTheme);
            }
            else
            {
                XboxTheme.Remove();
            }

            window = new MainWindow(initialPage);
            window.AppWindow.Closing += (_, e) =>
            {
                if (!exiting && !closingForReal)
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
