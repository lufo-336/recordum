using FluentAssertions;
using Recordum.Presentation.Formatting;
using Xunit;

namespace Recordum.Presentation.Tests.Formatting;

public class DurationFormatTests
{
    [Theory]
    [InlineData("45", 45)]          // solo cifre: ultime 2 = secondi
    [InlineData("130", 90)]         // "1:30"
    [InlineData("0:45", 45)]
    [InlineData("12:05", 725)]
    [InlineData("1:02:05", 3725)]
    [InlineData(" 1:02:05 ", 3725)]
    public void Parses(string text, int expected)
    { Assert.True(DurationFormat.TryParse(text, out var s)); Assert.Equal(expected, s); }

    [Theory]
    [InlineData("")] [InlineData("  ")] [InlineData("abc")] [InlineData("1:99")] [InlineData("1:2:3:4")] [InlineData("-10")]
    [InlineData("99999999999")]   // long digit string — must not throw
    [InlineData("999999999:00")] // colon-branch silent overflow
    public void Rejects(string text) => Assert.False(DurationFormat.TryParse(text, out _));

    [Theory]
    [InlineData(45, "0:45")]
    [InlineData(725, "12:05")]
    [InlineData(3725, "1:02:05")]
    [InlineData(90061, "1g 1:01:01")]
    [InlineData(31536000 + 86400 * 41 + 60, "1a 41g")]
    public void Formats(int seconds, string expected) => Assert.Equal(expected, DurationFormat.Format(seconds));

    [Theory]
    [InlineData(0, "0g")] [InlineData(41, "41g")] [InlineData(365, "1a 0g")] [InlineData(771, "2a 41g")]
    public void FormatsDays(int days, string expected) => Assert.Equal(expected, DurationFormat.FormatDays(days));

    [Theory]
    [InlineData(0, 0, 0, 0, "0:00:00")]
    [InlineData(0, 6, 14, 9, "6:14:09")]
    [InlineData(0, 0, 3, 7, "0:03:07")]
    [InlineData(21, 6, 14, 9, "21g 6:14:09")]
    [InlineData(1, 0, 0, 0, "1g 0:00:00")]
    public void FormatStopwatch_formats_days_h_mm_ss(int d, int h, int m, int s, string expected)
        => DurationFormat.FormatStopwatch(new TimeSpan(d, h, m, s)).Should().Be(expected);

    /// <summary>B7: una registrazione sola si legge in parole, come le cifre in testa ("20 min"): "20:00" sembrava un orario.</summary>
    [Theory]
    [InlineData(0, "0 s")]
    [InlineData(45, "45 s")]
    [InlineData(1200, "20 min")]
    [InlineData(1230, "20 min 30 s")]
    [InlineData(3600, "1 h")]
    [InlineData(3900, "1 h 05 min")]
    [InlineData(3930, "1 h 05 min")]
    [InlineData(27000, "7 h 30 min")]
    [InlineData(86400 + 7200, "1g 2h")]
    [InlineData(-5, "0 s")]
    public void FormatWords_reads_a_single_duration_without_a_clock(int seconds, string expected)
        => DurationFormat.FormatWords(seconds).Should().Be(expected);

    [Fact]
    public void FormatStopwatch_clamps_negative_to_zero()
        => DurationFormat.FormatStopwatch(TimeSpan.FromSeconds(-5)).Should().Be("0:00:00");
}
