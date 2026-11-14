using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DisplayProfiles.App.Tray;

internal readonly record struct MenuEntry(int Id, string Text, bool IsChecked = false, bool IsSeparator = false, bool IsDisabled = false)
{
    public static MenuEntry Separator => new(0, string.Empty, IsSeparator: true);
}

/// <summary>A native Win32 popup menu, shown at the tray icon like the menus of built-in Windows tray apps.</summary>
internal static unsafe class NativeMenu
{
    /// <summary>Shows the menu and returns the chosen entry id, or 0 if it was dismissed.</summary>
    public static int Show(HWND owner, IReadOnlyList<MenuEntry> entries, int x, int y)
    {
        var menu = PInvoke.CreatePopupMenu();
        try
        {
            foreach (var entry in entries)
            {
                if (entry.IsSeparator)
                {
                    PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, (PCWSTR)null);
                    continue;
                }

                var flags = MENU_ITEM_FLAGS.MF_STRING;
                if (entry.IsChecked)
                {
                    flags |= MENU_ITEM_FLAGS.MF_CHECKED;
                }

                if (entry.IsDisabled)
                {
                    flags |= MENU_ITEM_FLAGS.MF_GRAYED;
                }

                fixed (char* text = entry.Text)
                {
                    PInvoke.AppendMenu(menu, flags, (nuint)entry.Id, text);
                }
            }

            // Without a foreground owner the menu would not close when the user clicks elsewhere.
            PInvoke.SetForegroundWindow(owner);
            var command = PInvoke.TrackPopupMenuEx(
                menu,
                (uint)(TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_NONOTIFY |
                       TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN),
                x, y, owner, null);
            PInvoke.PostMessage(owner, PInvoke.WM_NULL, 0, 0);
            return command.Value;
        }
        finally
        {
            PInvoke.DestroyMenu(menu);
        }
    }
}
