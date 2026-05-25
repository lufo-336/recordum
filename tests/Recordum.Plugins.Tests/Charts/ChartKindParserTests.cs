using Recordum.Plugins.Contracts.Charts;
using Xunit;

namespace Recordum.Plugins.Tests.Charts;

public sealed class ChartKindParserTests
{
    [Theory]
    [InlineData(ChartKind.Line, "line")]
    [InlineData(ChartKind.Bar, "bar")]
    [InlineData(ChartKind.Area, "area")]
    [InlineData(ChartKind.StackedBar, "stacked-bar")]
    [InlineData(ChartKind.Scatter, "scatter")]
    [InlineData(ChartKind.Pie, "pie")]
    public void Round_trips_token(ChartKind kind, string token)
    {
        Assert.Equal(token, ChartKindParser.ToToken(kind));
        Assert.Equal(kind, ChartKindParser.Parse(token));
    }

    [Theory]
    [InlineData("stackedbar")]
    [InlineData("STACKED-BAR")]
    [InlineData("StackedBar")]
    public void Parse_accepts_case_insensitive_stacked_bar_aliases(string token)
        => Assert.Equal(ChartKind.StackedBar, ChartKindParser.Parse(token));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    public void Unknown_or_null_falls_back_to_Line(string? token)
        => Assert.Equal(ChartKind.Line, ChartKindParser.Parse(token));

    [Fact]
    public void ToToken_out_of_range_falls_back_to_line()
        => Assert.Equal("line", ChartKindParser.ToToken((ChartKind)99));
}
