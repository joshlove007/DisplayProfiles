using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DisplayProfiles.App.Interop;

/// <summary>Stops a window from being resized below a minimum size (WinUI has no built-in minimum).</summary>
internal static unsafe class WindowMinSize
{
    private static readonly Dictionary<nint, (int Width, int Height)> Sizes = [];

    /// <summary>Sizes are in device-independent pixels at 100% scale and are scaled to the window's current DPI.</summary>
    public static void Apply(HWND window, int minWidth, int minHeight)
    {
        Sizes[(nint)window.Value] = (minWidth, minHeight);
        PInvoke.SetWindowSubclass(window, &SubclassProc, 1, 0);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT SubclassProc(HWND window, uint message, WPARAM wParam, LPARAM lParam, nuint id, nuint data)
    {
        if (message == PInvoke.WM_GETMINMAXINFO && Sizes.TryGetValue((nint)window.Value, out var min))
        {
            double scale = PInvoke.GetDpiForWindow(window) / 96.0;
            var info = (MINMAXINFO*)lParam.Value;
            info->ptMinTrackSize.X = (int)(min.Width * scale);
            info->ptMinTrackSize.Y = (int)(min.Height * scale);
            return default;
        }

        return PInvoke.DefSubclassProc(window, message, wParam, lParam);
    }
}
