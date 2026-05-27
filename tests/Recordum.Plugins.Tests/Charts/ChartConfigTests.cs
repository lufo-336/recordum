using Recordum.Plugins.Contracts.Charts;
using Xunit;

namespace Recordum.Plugins.Tests.Charts;

public sealed class ChartConfigTests
{
    [Fact]
    public void Parse_null_or_empty_returns_defaults()
    {
        var c = ChartConfig.Parse(null);
        Assert.Equal("90d", c.Range);
        Assert.Equal("daily", c.Bucket);
        Assert.True(c.ShowLegend);
        Assert.Equal(c, ChartConfig.Parse("{}"));
        Assert.Equal(c, ChartConfig.Parse("  "));
    }

    [Fact]
    public void ToJson_then_Parse_round_trips()
    {
        var original = new ChartConfig("custom", "2026-01-01", "2026-03-01", "weekly", false, "Day", "Value");
        Assert.Equal(original, ChartConfig.Parse(original.ToJson()));
    }

    [Fact]
    public void Parse_malformed_json_returns_defaults()
        => Assert.Equal(new ChartConfig(), ChartConfig.Parse("{not json"));
}
