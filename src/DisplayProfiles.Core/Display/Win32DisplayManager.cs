using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Core.Display;

/// <summary>
/// Enumerates and reconfigures monitors through ChangeDisplaySettingsEx (the same mechanism the original
/// PowerShell script used), with the display-config (CCD) API supplying friendly monitor names.
/// </summary>
public sealed unsafe class Win32DisplayManager : IDisplayManager
{
    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var friendlyNames = QueryFriendlyNames();
        var found = new List<(string DevicePath, DisplayInfo Info)>();

        var adapter = NewDisplayDevice();
        for (uint i = 0; PInvoke.EnumDisplayDevices(null, i, ref adapter, 0); i++)
        {
            string adapterName = adapter.DeviceName.ToString();
            var state = adapter.StateFlags;

            // EDD_GET_DEVICE_INTERFACE_NAME makes DeviceID the same device-interface path that CCD reports.
            var monitor = NewDisplayDevice();
            if (!PInvoke.EnumDisplayDevices(adapterName, 0, ref monitor, EddGetDeviceInterfaceName) ||
                string.IsNullOrEmpty(monitor.DeviceID.ToString()))
            {
                adapter = NewDisplayDevice();
                continue;
            }

            string devicePath = monitor.DeviceID.ToString();
            string hardwareId = ExtractHardwareId(devicePath);
            bool attached = state.HasFlag(DISPLAY_DEVICE_STATE_FLAGS.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP);

            var current = NewDevMode();
            bool haveCurrent = attached &&
                PInvoke.EnumDisplaySettings(adapterName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref current);

            string friendly = friendlyNames.TryGetValue(devicePath, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : hardwareId;

            found.Add((devicePath, new DisplayInfo
            {
                AdapterName = adapterName,
                Key = hardwareId,
                FriendlyName = friendly,
                Attached = attached,
                IsPrimary = state.HasFlag(DISPLAY_DEVICE_STATE_FLAGS.DISPLAY_DEVICE_PRIMARY_DEVICE),
                X = haveCurrent ? current.dmPosition.x : 0,
                Y = haveCurrent ? current.dmPosition.y : 0,
                Width = haveCurrent ? (int)current.dmPelsWidth : 0,
                Height = haveCurrent ? (int)current.dmPelsHeight : 0,
                RefreshHz = haveCurrent ? (int)current.dmDisplayFrequency : 0,
                Orientation = haveCurrent ? FromDmdo(current.dmDisplayOrientation) : DisplayOrientation.Landscape,
            }));

            adapter = NewDisplayDevice();
        }

        return DisambiguateKeys(CollapseDuplicateOutputs(found));
    }

    public IReadOnlyList<DisplayMode> GetModes(string adapterName)
    {
        var modes = new HashSet<DisplayMode>();
        var dm = NewDevMode();
        for (int i = 0; PInvoke.EnumDisplaySettings(adapterName, (ENUM_DISPLAY_SETTINGS_MODE)i, ref dm); i++)
        {
            if (dm.dmPelsWidth > 0 && dm.dmDisplayFrequency > 1)
            {
                modes.Add(new DisplayMode((int)dm.dmPelsWidth, (int)dm.dmPelsHeight, (int)dm.dmDisplayFrequency));
            }

            dm = NewDevMode();
        }

        return modes
            .OrderByDescending(m => (long)m.Width * m.Height)
            .ThenByDescending(m => m.RefreshHz)
            .ToList();
    }

    public void Apply(ApplyPlan plan)
    {
        foreach (var placement in plan.Place)
        {
            Place(placement.AdapterName, placement.Config);
        }

        foreach (string adapterName in plan.DetachAdapters)
        {
            Detach(adapterName);
        }

        var result = PInvoke.ChangeDisplaySettingsEx(null, null, 0, null);
        if (result != DISP_CHANGE.DISP_CHANGE_SUCCESSFUL)
        {
            throw new DisplayApplyException($"Windows could not apply the display settings ({Describe(result)}).");
        }
    }

    private const uint EddGetDeviceInterfaceName = 1;

    private static void Place(string adapterName, MonitorConfig config)
    {
        var dm = NewDevMode();
        bool haveMode = PInvoke.EnumDisplaySettings(adapterName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_REGISTRY_SETTINGS, ref dm) && dm.dmPelsWidth > 0;
        if (!haveMode)
        {
            dm = NewDevMode();
            haveMode = PInvoke.EnumDisplaySettings(adapterName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref dm) && dm.dmPelsWidth > 0;
        }

        if (!haveMode)
        {
            dm = NewDevMode();
        }

        if (config.Width > 0)
        {
            dm.dmPelsWidth = (uint)config.Width;
        }

        if (config.Height > 0)
        {
            dm.dmPelsHeight = (uint)config.Height;
        }

        if (config.RefreshHz > 0)
        {
            dm.dmDisplayFrequency = (uint)config.RefreshHz;
        }

        if (dm.dmBitsPerPel == 0)
        {
            dm.dmBitsPerPel = 32;
        }

        dm.dmPosition = new POINTL { x = config.X, y = config.Y };
        dm.dmDisplayOrientation = ToDmdo(config.Orientation);
        dm.dmFields =
            DEVMODE_FIELD_FLAGS.DM_POSITION |
            DEVMODE_FIELD_FLAGS.DM_BITSPERPEL |
            DEVMODE_FIELD_FLAGS.DM_PELSWIDTH |
            DEVMODE_FIELD_FLAGS.DM_PELSHEIGHT |
            DEVMODE_FIELD_FLAGS.DM_DISPLAYORIENTATION;
        if (dm.dmDisplayFrequency > 0)
        {
            dm.dmFields |= DEVMODE_FIELD_FLAGS.DM_DISPLAYFREQUENCY;
        }

        var flags = CDS_TYPE.CDS_UPDATEREGISTRY | CDS_TYPE.CDS_NORESET;
        if (config.IsPrimary)
        {
            flags |= CDS_TYPE.CDS_SET_PRIMARY;
        }

        var result = PInvoke.ChangeDisplaySettingsEx(adapterName, dm, flags, null);
        if (result != DISP_CHANGE.DISP_CHANGE_SUCCESSFUL)
        {
            throw new DisplayApplyException(
                $"Windows rejected {config.Width} × {config.Height} @ {config.RefreshHz} Hz for {config.Name} ({Describe(result)}).");
        }
    }

    private static void Detach(string adapterName)
    {
        var dm = NewDevMode();
        dm.dmFields = DEVMODE_FIELD_FLAGS.DM_POSITION | DEVMODE_FIELD_FLAGS.DM_PELSWIDTH | DEVMODE_FIELD_FLAGS.DM_PELSHEIGHT;

        var result = PInvoke.ChangeDisplaySettingsEx(
            adapterName, dm, CDS_TYPE.CDS_UPDATEREGISTRY | CDS_TYPE.CDS_NORESET, null);
        if (result != DISP_CHANGE.DISP_CHANGE_SUCCESSFUL)
        {
            throw new DisplayApplyException($"Windows could not disconnect {adapterName} ({Describe(result)}).");
        }
    }

    private static DISPLAY_DEVICEW NewDisplayDevice() => new() { cb = (uint)sizeof(DISPLAY_DEVICEW) };

    private static DEVMODEW NewDevMode() => new() { dmSize = (ushort)sizeof(DEVMODEW) };

    internal static string ExtractHardwareId(string devicePath)
    {
        // \\?\DISPLAY#GSMC0C8#5&...#{guid}  or  MONITOR\GSMC0C8\{guid}\0004
        if (devicePath.StartsWith(@"\\?\DISPLAY#", StringComparison.OrdinalIgnoreCase))
        {
            return devicePath.Split('#')[1].ToUpperInvariant();
        }

        if (devicePath.StartsWith(@"MONITOR\", StringComparison.OrdinalIgnoreCase))
        {
            return devicePath.Split('\\')[1].ToUpperInvariant();
        }

        return devicePath.ToUpperInvariant();
    }

    // Windows lists one physical monitor once per GPU output it could be driven from, all with the same
    // device path. Keep the output it is attached to, or else the lowest-numbered one.
    private static List<(string DevicePath, DisplayInfo Info)> CollapseDuplicateOutputs(List<(string DevicePath, DisplayInfo Info)> found) =>
        found
            .GroupBy(f => f.DevicePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(f => f.Info.Attached).ThenBy(f => f.Info.AdapterName, StringComparer.OrdinalIgnoreCase).First())
            .ToList();

    // Two identical monitors share a hardware id; number them (#2, #3 ...) in a stable order.
    private static List<DisplayInfo> DisambiguateKeys(List<(string DevicePath, DisplayInfo Info)> found)
    {
        var result = new List<DisplayInfo>(found.Count);
        foreach (var group in found.GroupBy(f => f.Info.Key, StringComparer.Ordinal))
        {
            int n = 0;
            foreach (var item in group.OrderBy(f => f.DevicePath, StringComparer.OrdinalIgnoreCase))
            {
                n++;
                result.Add(n == 1 ? item.Info : item.Info with { Key = $"{item.Info.Key}#{n}" });
            }
        }

        return result.OrderBy(d => d.AdapterName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Dictionary<string, string> QueryFriendlyNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (PInvoke.GetDisplayConfigBufferSizes(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ALL_PATHS, out uint pathCount, out uint modeCount) != WIN32_ERROR.ERROR_SUCCESS)
        {
            return names;
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        if (PInvoke.QueryDisplayConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ALL_PATHS, ref pathCount, paths, ref modeCount, modes) != WIN32_ERROR.ERROR_SUCCESS)
        {
            return names;
        }

        var seen = new HashSet<(uint, int, uint)>();
        for (int i = 0; i < pathCount; i++)
        {
            var target = paths[i].targetInfo;
            if (!seen.Add((target.adapterId.LowPart, target.adapterId.HighPart, target.id)))
            {
                continue;
            }

            var request = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
            request.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
            request.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
            request.header.adapterId = target.adapterId;
            request.header.id = target.id;

            if (PInvoke.DisplayConfigGetDeviceInfo((DISPLAYCONFIG_DEVICE_INFO_HEADER*)&request) == 0)
            {
                names[request.monitorDevicePath.ToString()] = request.monitorFriendlyDeviceName.ToString();
            }
        }

        return names;
    }

    // Windows labels the two portrait modes the other way round from the raw DMDO angles.
    private static DEVMODE_DISPLAY_ORIENTATION ToDmdo(DisplayOrientation o) => o switch
    {
        DisplayOrientation.Portrait => DEVMODE_DISPLAY_ORIENTATION.DMDO_270,
        DisplayOrientation.LandscapeFlipped => DEVMODE_DISPLAY_ORIENTATION.DMDO_180,
        DisplayOrientation.PortraitFlipped => DEVMODE_DISPLAY_ORIENTATION.DMDO_90,
        _ => DEVMODE_DISPLAY_ORIENTATION.DMDO_DEFAULT,
    };

    private static DisplayOrientation FromDmdo(DEVMODE_DISPLAY_ORIENTATION o) => o switch
    {
        DEVMODE_DISPLAY_ORIENTATION.DMDO_270 => DisplayOrientation.Portrait,
        DEVMODE_DISPLAY_ORIENTATION.DMDO_180 => DisplayOrientation.LandscapeFlipped,
        DEVMODE_DISPLAY_ORIENTATION.DMDO_90 => DisplayOrientation.PortraitFlipped,
        _ => DisplayOrientation.Landscape,
    };

    private static string Describe(DISP_CHANGE result) => result switch
    {
        DISP_CHANGE.DISP_CHANGE_BADMODE => "unsupported mode",
        DISP_CHANGE.DISP_CHANGE_BADFLAGS => "bad flags",
        DISP_CHANGE.DISP_CHANGE_BADPARAM => "bad parameter",
        DISP_CHANGE.DISP_CHANGE_BADDUALVIEW => "dual-view conflict",
        DISP_CHANGE.DISP_CHANGE_NOTUPDATED => "settings could not be saved",
        DISP_CHANGE.DISP_CHANGE_RESTART => "restart required",
        _ => "failed",
    };
}
