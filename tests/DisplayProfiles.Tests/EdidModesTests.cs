using DisplayProfiles.Core.Display;
using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Tests;

public class EdidModesTests
{
    [Fact]
    public void Reads_detailed_standard_and_cta_video_modes()
    {
        var edid = new byte[256];
        new byte[] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 }.CopyTo(edid, 0);
        edid.AsSpan(38, 16).Fill(0x01); // unused standard timings
        edid[38] = 0xD1; // (0xD1 + 31) * 8 = 1920 wide
        edid[39] = 0xC0; // 16:9, 60 Hz
        Detailed(3840, 2160, 560, 90, 594_000_000).CopyTo(edid, 54); // 3840 x 2160 at 60 Hz

        edid[128] = 0x02; // CTA extension
        edid[130] = 8;    // detailed timings start after the data blocks
        edid[132] = 0x43; // video data block, three codes
        edid[133] = 97;   // 3840 x 2160 at 60 Hz
        edid[134] = 118;  // 3840 x 2160 at 120 Hz
        edid[135] = 0x80 | 16; // 1920 x 1080 at 60 Hz, marked native

        var modes = EdidModes.Parse(edid);

        Assert.Contains(new DisplayMode(3840, 2160, 60), modes);
        Assert.Contains(new DisplayMode(3840, 2160, 120), modes);
        Assert.Contains(new DisplayMode(1920, 1080, 60), modes);
        Assert.Equal(new DisplayMode(3840, 2160, 120), modes[0]);
    }

    [Fact]
    public void Ignores_data_that_is_not_an_edid()
    {
        Assert.Empty(EdidModes.Parse(new byte[256]));
        Assert.Empty(EdidModes.Parse([1, 2, 3]));
    }

    private static byte[] Detailed(int width, int height, int horizontalBlank, int verticalBlank, long pixelClockHz)
    {
        var d = new byte[18];
        int clock = (int)(pixelClockHz / 10_000);
        d[0] = (byte)(clock & 0xFF);
        d[1] = (byte)(clock >> 8);
        d[2] = (byte)(width & 0xFF);
        d[3] = (byte)(horizontalBlank & 0xFF);
        d[4] = (byte)(((width >> 8) << 4) | (horizontalBlank >> 8));
        d[5] = (byte)(height & 0xFF);
        d[6] = (byte)(verticalBlank & 0xFF);
        d[7] = (byte)(((height >> 8) << 4) | (verticalBlank >> 8));
        return d;
    }
}
