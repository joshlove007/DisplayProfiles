using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Core.Display;

/// <summary>
/// Reads the display modes a monitor announces in its EDID (the data block every monitor reports about itself). Windows
/// can only list a monitor's modes through a graphics output that is currently driving it; for a monitor that is off the
/// desktop it often returns a short generic list or nothing, so the EDID is the source for those.
/// </summary>
public static class EdidModes
{
    private const int BlockSize = 128;
    private const byte CtaExtensionTag = 0x02;

    // CTA-861 video identification codes that are not interlaced, by code: width, height, refresh rate.
    private static readonly Dictionary<int, DisplayMode> Vics = new()
    {
        [1] = new(640, 480, 60),
        [2] = new(720, 480, 60),
        [3] = new(720, 480, 60),
        [4] = new(1280, 720, 60),
        [16] = new(1920, 1080, 60),
        [17] = new(720, 576, 50),
        [18] = new(720, 576, 50),
        [19] = new(1280, 720, 50),
        [31] = new(1920, 1080, 50),
        [32] = new(1920, 1080, 24),
        [33] = new(1920, 1080, 25),
        [34] = new(1920, 1080, 30),
        [47] = new(1280, 720, 120),
        [60] = new(1280, 720, 24),
        [61] = new(1280, 720, 25),
        [62] = new(1280, 720, 30),
        [63] = new(1920, 1080, 120),
        [64] = new(1920, 1080, 100),
        [93] = new(3840, 2160, 24),
        [94] = new(3840, 2160, 25),
        [95] = new(3840, 2160, 30),
        [96] = new(3840, 2160, 50),
        [97] = new(3840, 2160, 60),
        [98] = new(4096, 2160, 24),
        [99] = new(4096, 2160, 25),
        [100] = new(4096, 2160, 30),
        [101] = new(4096, 2160, 50),
        [102] = new(4096, 2160, 60),
        [103] = new(3840, 2160, 24),
        [104] = new(3840, 2160, 25),
        [105] = new(3840, 2160, 30),
        [106] = new(3840, 2160, 50),
        [107] = new(3840, 2160, 60),
        [114] = new(3840, 2160, 48),
        [115] = new(4096, 2160, 48),
        [116] = new(3840, 2160, 48),
        [117] = new(3840, 2160, 100),
        [118] = new(3840, 2160, 120),
        [119] = new(3840, 2160, 100),
        [120] = new(3840, 2160, 120),
    };

    /// <summary>The modes the EDID announces, largest first. Returns an empty list for data that isn't an EDID.</summary>
    public static IReadOnlyList<DisplayMode> Parse(ReadOnlySpan<byte> edid)
    {
        var modes = new HashSet<DisplayMode>();
        if (edid.Length < BlockSize || !HasHeader(edid))
        {
            return [];
        }

        for (int offset = 54; offset <= 108; offset += 18)
        {
            AddDetailedTiming(edid.Slice(offset, 18), modes);
        }

        AddStandardTimings(edid.Slice(38, 16), modes);

        for (int block = BlockSize; block + BlockSize <= edid.Length; block += BlockSize)
        {
            if (edid[block] == CtaExtensionTag)
            {
                AddCtaBlock(edid.Slice(block, BlockSize), modes);
            }
        }

        return [.. modes
            .OrderByDescending(m => m.Width)
            .ThenByDescending(m => m.Height)
            .ThenByDescending(m => m.RefreshHz)];
    }

    private static bool HasHeader(ReadOnlySpan<byte> edid) =>
        edid[0] == 0x00 && edid[1] == 0xFF && edid[2] == 0xFF && edid[3] == 0xFF &&
        edid[4] == 0xFF && edid[5] == 0xFF && edid[6] == 0xFF && edid[7] == 0x00;

    private static void AddDetailedTiming(ReadOnlySpan<byte> descriptor, HashSet<DisplayMode> modes)
    {
        int pixelClock = descriptor[0] | (descriptor[1] << 8); // units of 10 kHz; zero marks another kind of descriptor
        if (pixelClock == 0 || (descriptor[17] & 0x80) != 0) // interlaced timings aren't offered as desktop modes
        {
            return;
        }

        int horizontalActive = descriptor[2] | ((descriptor[4] & 0xF0) << 4);
        int horizontalBlank = descriptor[3] | ((descriptor[4] & 0x0F) << 8);
        int verticalActive = descriptor[5] | ((descriptor[7] & 0xF0) << 4);
        int verticalBlank = descriptor[6] | ((descriptor[7] & 0x0F) << 8);
        long total = (long)(horizontalActive + horizontalBlank) * (verticalActive + verticalBlank);
        if (horizontalActive == 0 || verticalActive == 0 || total == 0)
        {
            return;
        }

        int refresh = (int)Math.Round(pixelClock * 10_000.0 / total);
        modes.Add(new DisplayMode(horizontalActive, verticalActive, refresh));
    }

    private static void AddStandardTimings(ReadOnlySpan<byte> timings, HashSet<DisplayMode> modes)
    {
        for (int i = 0; i < timings.Length; i += 2)
        {
            if (timings[i] == 0x01 && timings[i + 1] == 0x01)
            {
                continue; // unused slot
            }

            int width = (timings[i] + 31) * 8;
            int height = (timings[i + 1] >> 6) switch
            {
                0 => width * 10 / 16,
                1 => width * 3 / 4,
                2 => width * 4 / 5,
                _ => width * 9 / 16,
            };
            modes.Add(new DisplayMode(width, height, (timings[i + 1] & 0x3F) + 60));
        }
    }

    private static void AddCtaBlock(ReadOnlySpan<byte> block, HashSet<DisplayMode> modes)
    {
        int detailedStart = block[2];
        if (detailedStart < 4 || detailedStart > BlockSize)
        {
            return;
        }

        for (int i = 4; i < detailedStart;)
        {
            int tag = block[i] >> 5;
            int length = block[i] & 0x1F;
            if (i + 1 + length > detailedStart)
            {
                break;
            }

            // Tag 2 is the video data block; extended tag 14 lists the codes of modes only offered as YCbCr 4:2:0.
            bool video = tag == 2;
            bool video420 = tag == 7 && length > 0 && block[i + 1] == 14;
            if (video || video420)
            {
                for (int j = i + (video ? 1 : 2); j <= i + length; j++)
                {
                    int code = block[j];
                    int vic = code is >= 129 and <= 192 ? code - 128 : code; // 129-192 mark a native mode
                    if (Vics.TryGetValue(vic, out var mode))
                    {
                        modes.Add(mode);
                    }
                }
            }

            i += 1 + length;
        }

        for (int i = detailedStart; i + 18 <= BlockSize - 1; i += 18)
        {
            AddDetailedTiming(block.Slice(i, 18), modes);
        }
    }
}
