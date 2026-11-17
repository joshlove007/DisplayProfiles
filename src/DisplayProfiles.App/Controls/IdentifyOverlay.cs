using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DisplayProfiles.Core.Display;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DisplayProfiles.App.Controls;

/// <summary>
/// Briefly shows each display's number on that display, like "Identify" in Windows Settings: a dark rounded box in the
/// bottom-left corner of the display with the number drawn in thin white type. Each box is a plain native layered window
/// (a WinUI window takes hundreds of milliseconds to appear; this shows up at once). Displays that duplicate each other
/// share one box, labelled like "1|3".
/// </summary>
internal static unsafe partial class IdentifyOverlay
{
    // Measured from Windows' own boxes at 100% scale.
    private const int Size = 300;
    private const int Margin = 50;
    private const int CornerRadius = 8;
    private const double FontHeight = 0.47;
    private const uint Fill = 0x111111;

    private const uint CloseTimerId = 1;
    private const uint DurationMilliseconds = 2500;
    private const string ClassName = "DisplayProfilesIdentify";

    private static bool registered;

    /// <param name="numberOf">The number the editor shows for a display (by its key), so Identify and the editor always agree.</param>
    public static void Show(IEnumerable<DisplayInfo> displays, Func<string, int> numberOf)
    {
        Register();
        var module = PInvoke.GetModuleHandle((PCWSTR)null);

        var groups = displays
            .Where(d => d.Attached)
            .GroupBy(d => d.DuplicateOf ?? d.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var numbers = group.Select(d => numberOf(d.Key)).Where(n => n > 0).Distinct().Order().ToList();
            if (numbers.Count == 0)
            {
                continue;
            }

            var display = group.FirstOrDefault(d => d.DuplicateOf is null) ?? group.First();
            string label = string.Join("|", numbers.Select(n => n.ToString(CultureInfo.InvariantCulture)));

            var (workLeft, workBottom, scale) = WorkArea(display);
            int height = (int)Math.Round(Size * scale);
            int width = label.Length > 3 ? (int)Math.Round(Size * scale * label.Length / 3) : height;
            int x = workLeft + (int)Math.Round(Margin * scale);
            int y = workBottom - height;

            HWND window;
            fixed (char* className = ClassName)
            {
                window = PInvoke.CreateWindowEx(
                    WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_LAYERED,
                    className, className, WINDOW_STYLE.WS_POPUP,
                    x, y, width, height,
                    HWND.Null, HMENU.Null, (HINSTANCE)module, null);
            }

            if (window.IsNull)
            {
                continue;
            }

            Render(window, label, x, y, width, height, scale);
            PInvoke.SetTimer(window, CloseTimerId, DurationMilliseconds, null);
            PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }
    }

    private static void Register()
    {
        if (registered)
        {
            return;
        }

        registered = true;
        var module = PInvoke.GetModuleHandle((PCWSTR)null);
        fixed (char* className = ClassName)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WindowProc,
                hInstance = (HINSTANCE)module,
                lpszClassName = className,
            };
            PInvoke.RegisterClassEx(windowClass);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WindowProc(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == PInvoke.WM_TIMER)
        {
            PInvoke.KillTimer(window, CloseTimerId);
            PInvoke.DestroyWindow(window);
            return default;
        }

        return PInvoke.DefWindowProc(window, message, wParam, lParam);
    }

    /// <summary>The left edge and bottom edge of the display's work area (above the taskbar) and its scale (1.0 = 100%).</summary>
    private static (int Left, int Bottom, double Scale) WorkArea(DisplayInfo display)
    {
        var monitor = MonitorFromPoint(new Point { X = display.X + 1, Y = display.Y + 1 }, 2 /* MONITOR_DEFAULTTONEAREST */);
        int* info = stackalloc int[10];
        info[0] = 40;
        if (monitor == 0 || GetMonitorInfo(monitor, info) == 0)
        {
            return (display.X, display.Y + display.Height, 1.0);
        }

        uint dpiX = 96, dpiY = 96;
        if (GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, &dpiX, &dpiY) != 0 || dpiX == 0)
        {
            dpiX = 96;
        }

        return (info[5], info[8], dpiX / 96.0);
    }

    private static void Render(HWND window, string label, int x, int y, int width, int height, double scale)
    {
        var screen = GetDC(0);
        var memory = CreateCompatibleDC(screen);

        int* header = stackalloc int[10];
        header[0] = 40;
        header[1] = width;
        header[2] = -height;
        header[3] = 1 | (32 << 16); // one plane, 32 bits per pixel
        void* bits;
        var bitmap = CreateDIBSection(memory, header, 0, &bits, 0, 0);
        var oldBitmap = SelectObject(memory, bitmap);

        uint* pixels = (uint*)bits;
        new Span<uint>(pixels, width * height).Fill(0xFF000000 | Fill);

        // Segoe UI Light, white, grayscale-antialiased so the pixels stay colour-neutral.
        var font = CreateFont(-(int)Math.Round(height * FontHeight), 0, 0, 0, 300, 0, 0, 0, 1, 0, 0, 4 /* ANTIALIASED_QUALITY */, 0, "Segoe UI Light");
        var oldFont = SelectObject(memory, font);
        SetBkMode(memory, 1 /* TRANSPARENT */);
        SetTextColor(memory, 0xFFFFFF);
        int* area = stackalloc int[4] { 0, 0, width, height };
        DrawText(memory, label, label.Length, area, 0x1 | 0x4 | 0x20 | 0x800); // DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX
        SelectObject(memory, oldFont);
        DeleteObject(font);

        // GDI text can clear the alpha byte of the pixels it touches (depending on the display driver), which would make
        // the number see-through; the box is fully opaque, so put it back.
        for (int i = 0; i < width * height; i++)
        {
            pixels[i] |= 0xFF000000;
        }

        RoundCorners(pixels, width, height, CornerRadius * scale);

        int* position = stackalloc int[2] { x, y };
        int* extent = stackalloc int[2] { width, height };
        int* origin = stackalloc int[2] { 0, 0 };
        uint blend = 0x01FF0000; // AC_SRC_OVER, opaque, source alpha
        UpdateLayeredWindow((nint)window.Value, screen, position, extent, memory, origin, 0, &blend, 2 /* ULW_ALPHA */);

        SelectObject(memory, oldBitmap);
        DeleteObject(bitmap);
        DeleteDC(memory);
        ReleaseDC(0, screen);
    }

    /// <summary>Makes the corners transparent with antialiased edges (premultiplied alpha).</summary>
    private static void RoundCorners(uint* pixels, int width, int height, double radius)
    {
        int reach = (int)Math.Ceiling(radius);
        for (int row = 0; row < reach; row++)
        {
            for (int column = 0; column < reach; column++)
            {
                double dx = radius - (column + 0.5);
                double dy = radius - (row + 0.5);
                double coverage = Math.Clamp(radius + 0.5 - Math.Sqrt((dx * dx) + (dy * dy)), 0, 1);
                if (coverage >= 1)
                {
                    continue;
                }

                Fade(pixels + (row * width) + column, coverage);
                Fade(pixels + (row * width) + (width - 1 - column), coverage);
                Fade(pixels + ((height - 1 - row) * width) + column, coverage);
                Fade(pixels + ((height - 1 - row) * width) + (width - 1 - column), coverage);
            }
        }
    }

    private static void Fade(uint* pixel, double coverage)
    {
        uint value = (uint)Math.Round((*pixel & 0xFF) * coverage);
        uint alpha = (uint)Math.Round(255 * coverage);
        *pixel = (alpha << 24) | (value << 16) | (value << 8) | value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(Point point, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    private static partial int GetMonitorInfo(nint monitor, int* info);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int type, uint* dpiX, uint* dpiY);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint window);

    [LibraryImport("user32.dll")]
    private static partial void ReleaseDC(nint window, nint dc);

    [LibraryImport("user32.dll", EntryPoint = "DrawTextW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int DrawText(nint dc, string text, int count, int* area, uint format);

    [LibraryImport("user32.dll")]
    private static partial void UpdateLayeredWindow(nint window, nint destination, int* position, int* size, nint source, int* origin, uint key, uint* blend, uint flags);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateDIBSection(nint dc, int* header, uint usage, void** bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint item);

    [LibraryImport("gdi32.dll")]
    private static partial void DeleteObject(nint item);

    [LibraryImport("gdi32.dll")]
    private static partial void DeleteDC(nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial void SetBkMode(nint dc, int mode);

    [LibraryImport("gdi32.dll")]
    private static partial void SetTextColor(nint dc, uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateFontW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateFont(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);
}
