namespace DisplayProfiles.Core.Display;

/// <summary>
/// Names refresh rates the way Windows Settings does. The classic display API reports whole numbers, so the video rates
/// that are really 23.976, 29.97, 59.94 or 119.88 Hz come back as 23, 29, 59 and 119. Windows lists both 60 and 59.94, so a
/// whole number is one of these when the next whole number up is also offered at that resolution.
/// </summary>
public static class RefreshRates
{
    private static readonly int[] VideoRates = [24, 30, 48, 60, 120, 240];

    public static bool IsFractional(int hz, IEnumerable<int> offeredAtResolution) =>
        Array.IndexOf(VideoRates, hz + 1) >= 0 && offeredAtResolution.Contains(hz + 1);

    public static string Label(int hz, IEnumerable<int> offeredAtResolution)
    {
        if (!IsFractional(hz, offeredAtResolution))
        {
            return $"{hz} Hz";
        }

        double exact = (hz + 1) * 1000.0 / 1001.0;
        return $"{Math.Round(exact, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} Hz";
    }
}
