using DisplayProfiles.Core.Display;

namespace DisplayProfiles.Core.Model;

/// <summary>
/// Decides which monitors the profile editor lists and in what order. The numbers shown in the editor are simply the
/// position in this list, so they are always 1, 2, 3 ... with no gaps or repeats. Monitors are tied to each other by their
/// hardware id (<see cref="MonitorConfig.Key"/>), never by that number.
/// </summary>
public static class MonitorList
{
    /// <summary>Connected monitors first, in the order Windows lists their outputs, then the ones that aren't connected.</summary>
    public static List<MonitorConfig> Order(IEnumerable<MonitorConfig> configs, IReadOnlyList<DisplayInfo> displays)
    {
        var adapterByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var display in displays)
        {
            adapterByKey.TryAdd(display.Key, display.AdapterName);
        }

        var all = configs.ToList();
        var connected = all
            .Where(c => adapterByKey.ContainsKey(c.Key))
            .OrderBy(c => AdapterOrder(adapterByKey[c.Key]))
            .ThenBy(c => c.Key, StringComparer.Ordinal);
        var others = all.Where(c => !adapterByKey.ContainsKey(c.Key));
        return [.. connected, .. others];
    }

    /// <summary>
    /// A monitor that is connected, or switched on in the profile, is always listed. One that is neither is only a
    /// leftover (a TV that is unplugged, a remote-desktop display) and is listed only when the user asks to see them.
    /// </summary>
    public static bool IsVisible(MonitorConfig config, bool isConnected, bool showDisconnectedOff) =>
        isConnected || config.Enabled || showDisconnectedOff;

    // "\\.\DISPLAY10" has to sort after "\\.\DISPLAY2".
    private static int AdapterOrder(string adapterName)
    {
        const string Prefix = @"\\.\DISPLAY";
        return adapterName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(adapterName.AsSpan(Prefix.Length), out int n)
            ? n
            : int.MaxValue;
    }
}
