using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using DisplayProfiles.Core.Display;
using Windows.Graphics;

namespace DisplayProfiles.App.Controls;

/// <summary>Briefly shows each display's number on that display, like "Identify" in Windows Settings.</summary>
internal static class IdentifyOverlay
{
    private const int Size = 200;

    /// <param name="numberOf">The number the editor shows for a display, so Identify and the editor always agree.</param>
    public static void Show(IEnumerable<DisplayInfo> displays, Func<DisplayInfo, int> numberOf)
    {
        foreach (var display in displays.Where(d => d.Attached))
        {
            int displayNumber = numberOf(display);
            if (displayNumber <= 0)
            {
                continue;
            }

            var number = new TextBlock
            {
                Text = displayNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontSize = 96,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var window = new Window
            {
                SystemBackdrop = new DesktopAcrylicBackdrop(),
                Content = new Grid { Children = { number } },
            };

            var presenter = (OverlappedPresenter)window.AppWindow.Presenter;
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            window.AppWindow.IsShownInSwitchers = false;
            window.AppWindow.MoveAndResize(new RectInt32(display.X + 32, display.Y + 32, Size, Size));
            window.Activate();

            var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(2.5);
            timer.IsRepeating = false;
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                window.Close();
            };
            timer.Start();
        }
    }
}
