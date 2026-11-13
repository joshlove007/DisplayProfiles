using System.Runtime.InteropServices;

namespace MonitorToggle.App.Interop;

/// <summary>
/// Opts the process into dark-mode menus. Explorer uses the same (undocumented) uxtheme entry points,
/// so native popup menus follow the system theme like the built-in tray menus do.
/// </summary>
internal static unsafe partial class MenuTheme
{
    private const nint SetPreferredAppModeOrdinal = 135;
    private const nint FlushMenuThemesOrdinal = 136;
    private const int AllowDark = 1;

    public static void FollowSystemTheme()
    {
        if (!NativeLibrary.TryLoad("uxtheme.dll", out var library))
        {
            return;
        }

        nint setMode = GetProcAddressByOrdinal(library, SetPreferredAppModeOrdinal);
        nint flush = GetProcAddressByOrdinal(library, FlushMenuThemesOrdinal);
        if (setMode != 0 && flush != 0)
        {
            ((delegate* unmanaged[Stdcall]<int, int>)setMode)(AllowDark);
            ((delegate* unmanaged[Stdcall]<void>)flush)();
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetProcAddress")]
    private static partial nint GetProcAddressByOrdinal(nint module, nint ordinal);
}
