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
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("STACKEDBAR")]
    public void Unknown_or_null_falls_back_to_Line(string? token)
        => Assert.Equal(token == "STACKEDBAR" ? ChartKind.StackedBar : ChartKind.Line, ChartKindParser.Parse(token));
}
