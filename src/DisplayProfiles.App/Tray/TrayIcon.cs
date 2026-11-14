using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DisplayProfiles.App.Tray;

/// <summary>Notification-area icon: a message-only window plus Shell_NotifyIcon. Events fire on the UI thread.</summary>
internal sealed unsafe class TrayIcon : IDisposable
{
    private const uint CallbackMessage = PInvoke.WM_USER + 1;
    private const uint IconId = 1;

    private static TrayIcon? instance;

    private readonly uint taskbarCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarCreated");
    private HICON icon;
    private string tooltip = string.Empty;
    private bool added;
    private bool disposed;

    public TrayIcon()
    {
        instance = this;
        var module = PInvoke.GetModuleHandle((PCWSTR)null);

        fixed (char* className = "DisplayProfilesTrayWindow")
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &StaticWindowProc,
                hInstance = (HINSTANCE)module,
                lpszClassName = className,
            };
            PInvoke.RegisterClassEx(windowClass);

            // HWND_MESSAGE parent (-3) makes this an invisible message-only window.
            Handle = PInvoke.CreateWindowEx(
                0, className, className, 0, 0, 0, 0, 0, new HWND(-3), HMENU.Null, (HINSTANCE)module, null);
        }
    }

    public HWND Handle { get; }

    /// <summary>Left click on the icon, with the screen position of the cursor.</summary>
    public event Action<int, int>? Selected;

    /// <summary>Right click on the icon, with the screen position of the cursor.</summary>
    public event Action<int, int>? ContextRequested;

    public void SetIcon(string iconFilePath, int size)
    {
        HICON newIcon;
        fixed (char* path = iconFilePath)
        {
            newIcon = (HICON)PInvoke.LoadImage(
                HINSTANCE.Null, path, GDI_IMAGE_TYPE.IMAGE_ICON, size, size, IMAGE_FLAGS.LR_LOADFROMFILE).Value;
        }

        var old = icon;
        icon = newIcon;
        Update();
        if (!old.IsNull)
        {
            PInvoke.DestroyIcon(old);
        }
    }

    public void SetTooltip(string text)
    {
        tooltip = text;
        Update();
    }

    public void ShowBalloon(string title, string message)
    {
        var data = NewData(NOTIFY_ICON_DATA_FLAGS.NIF_INFO);
        Copy(title, data.szInfoTitle.AsSpan());
        Copy(message, data.szInfo.AsSpan());
        data.dwInfoFlags = NOTIFY_ICON_INFOTIP_FLAGS.NIIF_INFO;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, data);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (added)
        {
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, NewData(0));
        }

        PInvoke.DestroyWindow(Handle);
        if (!icon.IsNull)
        {
            PInvoke.DestroyIcon(icon);
        }

        instance = null;
    }

    private void Update()
    {
        if (disposed || icon.IsNull)
        {
            return;
        }

        var data = NewData(NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP);
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = icon;
        Copy(tooltip, data.szTip.AsSpan());

        PInvoke.Shell_NotifyIcon(added ? NOTIFY_ICON_MESSAGE.NIM_MODIFY : NOTIFY_ICON_MESSAGE.NIM_ADD, data);
        added = true;
    }

    private NOTIFYICONDATAW NewData(NOTIFY_ICON_DATA_FLAGS flags) => new()
    {
        cbSize = (uint)sizeof(NOTIFYICONDATAW),
        hWnd = Handle,
        uID = IconId,
        uFlags = flags,
    };

    private static void Copy(string text, Span<char> destination)
    {
        int length = Math.Min(text.Length, destination.Length - 1);
        text.AsSpan(0, length).CopyTo(destination);
        destination[length] = '\0';
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT StaticWindowProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        var self = instance;
        if (self is not null)
        {
            if (message == CallbackMessage)
            {
                // Legacy (version 0) callbacks pass the mouse message in lParam.
                uint mouseMessage = (uint)(lParam.Value & 0xFFFF);
                if (mouseMessage == PInvoke.WM_LBUTTONUP || mouseMessage == PInvoke.WM_RBUTTONUP)
                {
                    PInvoke.GetCursorPos(out var cursor);
                    var handler = mouseMessage == PInvoke.WM_LBUTTONUP ? self.Selected : self.ContextRequested;
                    handler?.Invoke(cursor.X, cursor.Y);
                }

                return default;
            }

            if (message == self.taskbarCreatedMessage)
            {
                // Explorer restarted; the icon has to be added again.
                self.added = false;
                self.Update();
                return default;
            }
        }

        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }
}
