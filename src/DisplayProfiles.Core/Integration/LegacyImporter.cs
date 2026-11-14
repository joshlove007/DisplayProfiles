using System.Globalization;
using System.Text.RegularExpressions;
using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Core.Integration;

/// <summary>
/// Builds "Desk" and "TV" profiles from the original toggle-monitor.ps1 script and its saved state, so existing
/// users get their two layouts back without re-creating them.
/// </summary>
public static partial class LegacyImporter
{
    public static string DefaultFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorToggle");

    /// <summary>Returns the imported profiles, or an empty list if the legacy script isn't there or doesn't match.</summary>
    public static IReadOnlyList<Profile> TryImport(string folder, IReadOnlyList<MonitorConfig> live)
    {
        string scriptPath = Path.Combine(folder, "toggle-monitor.ps1");
        if (!File.Exists(scriptPath))
        {
            return [];
        }

        string script = File.ReadAllText(scriptPath);
        string? tvId = Match(TvIdPattern(), script);
        string? deskPrimaryId = Match(PrimaryOffIdPattern(), script);
        string? otherId = Match(SecondaryIdPattern(), script);
        if (tvId is null || deskPrimaryId is null)
        {
            return [];
        }

        var tv = Find(live, tvId);
        var deskPrimary = Find(live, deskPrimaryId);
        var other = otherId is null ? null : Find(live, otherId);
        if (tv is null || deskPrimary is null)
        {
            return [];
        }

        var state = ReadState(Path.Combine(folder, ".monitor-toggle-state"));
        var everythingElse = live.Where(m => m.Key != tv.Key && m.Key != deskPrimary.Key && m.Key != other?.Key)
            .Select(m => m with { Enabled = false, IsPrimary = false });

        // Desk: the second monitor on the right of the primary one, the TV off (what the script did when turning the TV off).
        var desk = new List<MonitorConfig> { deskPrimary with { Enabled = true, IsPrimary = true, X = 0, Y = 0 } };
        if (other is not null)
        {
            desk.Add(other with { Enabled = true, IsPrimary = false, X = deskPrimary.Width, Y = 0 });
        }

        desk.Add(tv with { Enabled = false, IsPrimary = false });
        desk.AddRange(everythingElse);

        // TV: the TV is primary; the others sit where they were when the TV was last switched off,
        // or to the left of it by default (what the script did).
        int tvWidth = state.Width > 0 ? state.Width : tv.Width;
        int tvHeight = state.Height > 0 ? state.Height : tv.Height;
        var tvLayout = new List<MonitorConfig>
        {
            tv with { Enabled = true, IsPrimary = true, X = 0, Y = 0, Width = tvWidth, Height = tvHeight, RefreshHz = state.Hz > 0 ? state.Hz : tv.RefreshHz },
        };

        int defaultX = other is null ? -deskPrimary.Width : -(deskPrimary.Width + other.Width);
        foreach (var monitor in new[] { other, deskPrimary }.OfType<MonitorConfig>())
        {
            string token = monitor.Key.Split('#')[0];
            var offset = state.Offsets.TryGetValue(token, out var o) ? o : (X: defaultX, Y: 0);
            tvLayout.Add(monitor with { Enabled = true, IsPrimary = false, X = offset.X, Y = offset.Y });
            defaultX += monitor.Width;
        }

        tvLayout.AddRange(everythingElse);

        return
        [
            Profile.Create("Desk", LayoutMath.Normalize(desk)),
            Profile.Create("TV", LayoutMath.Normalize(tvLayout)),
        ];
    }

    private static MonitorConfig? Find(IReadOnlyList<MonitorConfig> live, string hardwareId) =>
        live.FirstOrDefault(m => m.Key.StartsWith(hardwareId, StringComparison.OrdinalIgnoreCase));

    private static string? Match(Regex pattern, string text) =>
        pattern.Match(text) is { Success: true } m ? m.Groups[1].Value : null;

    private static (int Width, int Height, int Hz, Dictionary<string, (int X, int Y)> Offsets) ReadState(string path)
    {
        int width = 0, height = 0, hz = 0;
        var offsets = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return (width, height, hz, offsets);
        }

        foreach (string line in File.ReadAllLines(path))
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts is ["RES", var w, var h, var f] &&
                int.TryParse(w, CultureInfo.InvariantCulture, out int rw) &&
                int.TryParse(h, CultureInfo.InvariantCulture, out int rh) &&
                int.TryParse(f, CultureInfo.InvariantCulture, out int rf))
            {
                (width, height, hz) = (rw, rh, rf);
            }
            else if (parts is ["POS", var token, var dx, var dy] &&
                     int.TryParse(dx, CultureInfo.InvariantCulture, out int x) &&
                     int.TryParse(dy, CultureInfo.InvariantCulture, out int y))
            {
                offsets[token] = (x, y);
            }
        }

        return (width, height, hz, offsets);
    }

    [GeneratedRegex(@"\$TvId\s*=\s*'([^']+)'")]
    private static partial Regex TvIdPattern();

    [GeneratedRegex(@"\$PrimaryOffId\s*=\s*'([^']+)'")]
    private static partial Regex PrimaryOffIdPattern();

    [GeneratedRegex(@"\$SamsungId\s*=\s*'([^']+)'")]
    private static partial Regex SecondaryIdPattern();
}
