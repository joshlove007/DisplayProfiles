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
    public IReadOnlyList<DisplayInfo> GetDisplays() => [.. Snapshot().Select(s => s.Info)];

    /// <summary>Every connected monitor with its device path (the same path CCD reports for its target).</summary>
    private static List<(string DevicePath, DisplayInfo Info)> Snapshot()
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

        return DisambiguateKeys(AddHidden(MarkMirrored(CollapseDuplicateOutputs(found))));
    }

    public IReadOnlyList<DisplayMode> GetModes(string adapterName)
    {
        if (adapterName.Length == 0)
        {
            return [];
        }

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
        if (plan.Duplicates.Count > 0)
        {
            // The topology (which displays are on, and which of them show the same picture) comes first; the desktop
            // positions and modes are set afterwards, on what it produced.
            plan = ArrangeDuplicates(plan);
        }

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

    public void BreakDuplicates()
    {
        var result = PInvoke.SetDisplayConfig([], [], SET_DISPLAY_CONFIG_FLAGS.SDC_APPLY | SET_DISPLAY_CONFIG_FLAGS.SDC_TOPOLOGY_EXTEND);
        if (result != 0)
        {
            throw new DisplayApplyException($"Windows could not separate the duplicated displays (error {result}).");
        }

        // The displays reappear one by one; give Windows time to finish.
        Thread.Sleep(3000);
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

    // Two identical monitors share a hardware id; number them (#2, #3 ...) in a stable order. Mirrored monitors name the
    // device path of the monitor they mirror (in DuplicateOf) until the final keys are known.
    private static List<(string DevicePath, DisplayInfo Info)> DisambiguateKeys(List<(string DevicePath, DisplayInfo Info)> found)
    {
        var result = new List<(string DevicePath, DisplayInfo Info)>(found.Count);
        foreach (var group in found.GroupBy(f => f.Info.Key, StringComparer.Ordinal))
        {
            int n = 0;
            foreach (var item in group.OrderBy(f => f.DevicePath, StringComparer.OrdinalIgnoreCase))
            {
                n++;
                result.Add(n == 1 ? item : (item.DevicePath, item.Info with { Key = $"{item.Info.Key}#{n}" }));
            }
        }

        var keyByPath = result.ToDictionary(r => r.DevicePath, r => r.Info.Key, StringComparer.OrdinalIgnoreCase);
        return [.. result
            .Select(r => r.Info.DuplicateOf is { } parentPath && keyByPath.TryGetValue(parentPath, out var parentKey)
                ? (r.DevicePath, Info: r.Info with { DuplicateOf = parentKey })
                : r)
            .OrderBy(r => r.Info.AdapterName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.DevicePath, StringComparer.OrdinalIgnoreCase)];
    }
    private const uint PathActive = 0x1; // DISPLAYCONFIG_PATH_ACTIVE
    private const uint ModeIndexInvalid = 0xFFFFFFFF; // DISPLAYCONFIG_PATH_MODE_IDX_INVALID

    private static (DISPLAYCONFIG_PATH_INFO[] Paths, DISPLAYCONFIG_MODE_INFO[] Modes) QueryConfig(QUERY_DISPLAY_CONFIG_FLAGS flags)
    {
        if (PInvoke.GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount) != WIN32_ERROR.ERROR_SUCCESS)
        {
            return ([], []);
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        if (PInvoke.QueryDisplayConfig(flags, ref pathCount, paths, ref modeCount, modes) != WIN32_ERROR.ERROR_SUCCESS)
        {
            return ([], []);
        }

        return (paths[..(int)pathCount], modes[..(int)modeCount]);
    }

    private static bool TryGetTargetName(LUID adapter, uint targetId, out string devicePath, out string friendlyName)
    {
        var request = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
        request.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
        request.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
        request.header.adapterId = adapter;
        request.header.id = targetId;

        if (PInvoke.DisplayConfigGetDeviceInfo((DISPLAYCONFIG_DEVICE_INFO_HEADER*)&request) != 0)
        {
            devicePath = friendlyName = string.Empty;
            return false;
        }

        devicePath = request.monitorDevicePath.ToString();
        friendlyName = request.monitorFriendlyDeviceName.ToString();
        return true;
    }

    private static Dictionary<string, string> QueryFriendlyNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<(uint, int, uint)>();
        foreach (var path in QueryConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ALL_PATHS).Paths)
        {
            var target = path.targetInfo;
            if (seen.Add((target.adapterId.LowPart, target.adapterId.HighPart, target.id)) &&
                TryGetTargetName(target.adapterId, target.id, out string devicePath, out string friendly))
            {
                names[devicePath] = friendly;
            }
        }

        return names;
    }

    /// <summary>
    /// Windows shows a duplicated (mirrored) display only under the display it mirrors, and the mirrored one can be
    /// missing from the adapter list altogether. The display configuration knows better: monitors that share one source
    /// show the same picture. The monitor Windows still lists as attached is the original; the others are marked as its
    /// duplicates (naming its device path for now, see <see cref="DisambiguateKeys"/>).
    /// </summary>
    private static List<(string DevicePath, DisplayInfo Info)> MarkMirrored(List<(string DevicePath, DisplayInfo Info)> found)
    {
        var bySource = new Dictionary<(uint Low, int High, uint Id), List<(string DevicePath, string Name)>>();
        foreach (var path in QueryConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS).Paths)
        {
            if (!TryGetTargetName(path.targetInfo.adapterId, path.targetInfo.id, out string devicePath, out string name))
            {
                continue;
            }

            var source = (path.sourceInfo.adapterId.LowPart, path.sourceInfo.adapterId.HighPart, path.sourceInfo.id);
            if (!bySource.TryGetValue(source, out var members))
            {
                bySource[source] = members = [];
            }

            members.Add((devicePath, name));
        }

        foreach (var members in bySource.Values.Where(m => m.Count > 1))
        {
            int parentAt = found.FindIndex(f => f.Info.Attached &&
                members.Any(m => string.Equals(m.DevicePath, f.DevicePath, StringComparison.OrdinalIgnoreCase)));
            if (parentAt < 0)
            {
                continue;
            }

            var parent = found[parentAt];
            foreach (var member in members.Where(m => !string.Equals(m.DevicePath, parent.DevicePath, StringComparison.OrdinalIgnoreCase)))
            {
                int at = found.FindIndex(f => string.Equals(f.DevicePath, member.DevicePath, StringComparison.OrdinalIgnoreCase));
                string hardwareId = ExtractHardwareId(member.DevicePath);
                var info = at >= 0
                    ? found[at].Info
                    : new DisplayInfo
                    {
                        AdapterName = parent.Info.AdapterName,
                        Key = hardwareId,
                        FriendlyName = string.IsNullOrWhiteSpace(member.Name) ? hardwareId : member.Name,
                    };
                var mirrored = info with
                {
                    Attached = true,
                    IsPrimary = false,
                    X = parent.Info.X,
                    Y = parent.Info.Y,
                    Width = parent.Info.Width,
                    Height = parent.Info.Height,
                    RefreshHz = parent.Info.RefreshHz,
                    Orientation = parent.Info.Orientation,
                    DuplicateOf = parent.DevicePath,
                };

                if (at >= 0)
                {
                    found[at] = (member.DevicePath, mirrored);
                }
                else
                {
                    found.Add((member.DevicePath, mirrored));
                }
            }
        }

        return found;
    }

    /// <summary>
    /// In some states Windows leaves a connected monitor that isn't on the desktop out of the adapter list. The display
    /// configuration still knows it, so it is listed here without an adapter (an empty <see cref="DisplayInfo.AdapterName"/>)
    /// and can't be placed until the desktop has been extended again.
    /// </summary>
    private static List<(string DevicePath, DisplayInfo Info)> AddHidden(List<(string DevicePath, DisplayInfo Info)> found)
    {
        foreach (var path in QueryConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ALL_PATHS).Paths)
        {
            if (!(bool)path.targetInfo.targetAvailable ||
                !TryGetTargetName(path.targetInfo.adapterId, path.targetInfo.id, out string devicePath, out string name) ||
                found.Any(f => string.Equals(f.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string hardwareId = ExtractHardwareId(devicePath);
            found.Add((devicePath, new DisplayInfo
            {
                AdapterName = string.Empty,
                Key = hardwareId,
                FriendlyName = string.IsNullOrWhiteSpace(name) ? hardwareId : name,
            }));
        }

        return found;
    }

    /// <summary>
    /// Sets up the topology for a plan with duplicates. Windows can only describe a duplicate that spans graphics adapters
    /// (a TV on the graphics card next to monitors on the processor's graphics) through the "virtual" paths it creates
    /// itself when it clones, so this asks Windows to clone every display and then keeps only the paths that are wanted:
    /// each group of displays that show the same picture shares one source, every other display gets a source of its own,
    /// and displays that are not in the plan are left out (switched off). Returns the plan with the adapter names that are
    /// valid afterwards, and nothing left to detach.
    /// </summary>
    private static ApplyPlan ArrangeDuplicates(ApplyPlan plan)
    {
        // Keys come from before the clone, while every monitor is still listed on its own.
        var keyByPath = KeysByDevicePath();
        string? KeyOf(DISPLAYCONFIG_PATH_TARGET_INFO target) =>
            TryGetTargetName(target.adapterId, target.id, out string devicePath, out _) && keyByPath.TryGetValue(devicePath, out var key)
                ? key
                : null;

        var result = PInvoke.SetDisplayConfig([], [], SET_DISPLAY_CONFIG_FLAGS.SDC_APPLY | SET_DISPLAY_CONFIG_FLAGS.SDC_TOPOLOGY_CLONE);
        if (result != 0)
        {
            throw new DisplayApplyException($"Windows could not set up the duplicate displays (error {result}).");
        }

        Thread.Sleep(3000);
        var all = QueryConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ALL_PATHS).Paths;

        // Every part of the desktop: a display with the displays that mirror it.
        var units = plan.Place
            .Select(p => (Keys: new[] { p.Config.Key }
                .Concat(plan.Duplicates.Where(d => d.ParentKey == p.Config.Key).Select(d => d.Key)).ToList(), Placement: p))
            .Select(u => (u.Keys, u.Placement, Options: SourcesOfferedForAll(u.Keys, all, KeyOf)))
            .OrderBy(u => u.Options.Count)
            .ToList();

        var used = new HashSet<(uint Low, int High, uint Id)>();
        var chosen = new List<DISPLAYCONFIG_PATH_INFO>();
        foreach (var unit in units)
        {
            var source = unit.Options.FirstOrDefault(s => !used.Contains(s));
            if (source == default)
            {
                throw new DisplayApplyException(
                    $"Windows can't set up {string.Join(" and ", unit.Keys)} as requested: it doesn't offer a free output for it.");
            }

            used.Add(source);
            foreach (string key in unit.Keys)
            {
                var path = all.First(p => KeyOf(p.targetInfo) == key &&
                    (p.sourceInfo.adapterId.LowPart, p.sourceInfo.adapterId.HighPart, p.sourceInfo.id) == source);
                path.flags |= PathActive;
                path.sourceInfo.Anonymous.modeInfoIdx = ModeIndexInvalid;
                path.targetInfo.Anonymous.modeInfoIdx = ModeIndexInvalid;
                chosen.Add(path);
            }
        }

        result = PInvoke.SetDisplayConfig(
            chosen.ToArray(),
            [],
            SET_DISPLAY_CONFIG_FLAGS.SDC_APPLY | SET_DISPLAY_CONFIG_FLAGS.SDC_USE_SUPPLIED_DISPLAY_CONFIG |
            SET_DISPLAY_CONFIG_FLAGS.SDC_ALLOW_CHANGES | SET_DISPLAY_CONFIG_FLAGS.SDC_SAVE_TO_DATABASE);
        if (result != 0)
        {
            throw new DisplayApplyException($"Windows could not duplicate the displays (error {result}).");
        }

        Thread.Sleep(2000);

        // The adapter names can have changed with the topology.
        var adapterByKey = Snapshot().ToDictionary(s => s.Info.Key, s => s.Info.AdapterName, StringComparer.Ordinal);
        var place = plan.Place
            .Select(p => adapterByKey.TryGetValue(p.Config.Key, out string? adapter) && adapter.Length > 0
                ? p with { AdapterName = adapter }
                : throw new DisplayApplyException($"{p.Config.Name} is no longer available after changing the display setup."))
            .ToList();
        return plan with { Place = place, DetachAdapters = [] };
    }

    // The sources (picture outputs) that every one of the displays has a path to, so they can all show that picture.
    private static List<(uint Low, int High, uint Id)> SourcesOfferedForAll(
        List<string> keys, DISPLAYCONFIG_PATH_INFO[] all, Func<DISPLAYCONFIG_PATH_TARGET_INFO, string?> keyOf)
    {
        IEnumerable<(uint Low, int High, uint Id)>? common = null;
        foreach (string key in keys)
        {
            var sources = all
                .Where(p => keyOf(p.targetInfo) == key)
                .Select(p => (p.sourceInfo.adapterId.LowPart, p.sourceInfo.adapterId.HighPart, p.sourceInfo.id))
                .Distinct();
            common = common is null ? sources : common.Intersect(sources);
        }

        return [.. common ?? []];
    }

    // Keys for every monitor path the display configuration knows. Where Windows' monitor list knows the monitor, its key is
    // used (keys can differ for identical monitors); the rest, such as a monitor missing from that list, are numbered the same way.
    private static Dictionary<string, string> KeysByDevicePath()
    {
        var devicePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in QueryConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ALL_PATHS).Paths)
        {
            if (TryGetTargetName(path.targetInfo.adapterId, path.targetInfo.id, out string devicePath, out _))
            {
                devicePaths.Add(devicePath);
            }
        }

        var keyByPath = devicePaths
            .GroupBy(ExtractHardwareId)
            .SelectMany(g => g.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select((p, i) => (Path: p, Key: i == 0 ? g.Key : $"{g.Key}#{i + 1}")))
            .ToDictionary(x => x.Path, x => x.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var (path, info) in Snapshot())
        {
            keyByPath[path] = info.Key;
        }

        return keyByPath;
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
