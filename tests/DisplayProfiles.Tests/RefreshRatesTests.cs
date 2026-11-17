using DisplayProfiles.Core.Display;

namespace DisplayProfiles.Tests;

public class RefreshRatesTests
{
    [Theory]
    [InlineData(119, "119.88 Hz")]
    [InlineData(59, "59.94 Hz")]
    [InlineData(29, "29.97 Hz")]
    [InlineData(23, "23.98 Hz")]
    [InlineData(120, "120 Hz")]
    [InlineData(60, "60 Hz")]
    [InlineData(48, "48 Hz")]
    [InlineData(100, "100 Hz")]
    public void Names_rates_like_Windows_Settings(int hz, string expected)
    {
        int[] offered = [120, 119, 100, 75, 60, 59, 50, 48, 30, 29, 25, 24, 23];

        Assert.Equal(expected, RefreshRates.Label(hz, offered));
    }

    [Fact]
    public void A_lone_whole_number_stays_whole()
    {
        Assert.Equal("59 Hz", RefreshRates.Label(59, [59, 50]));
    }
}
