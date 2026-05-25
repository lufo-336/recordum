namespace Recordum.Plugins.Contracts.Charts;

public static class ChartKindParser
{
    public static string ToToken(ChartKind kind) => kind switch
    {
        ChartKind.Line => "line",
        ChartKind.Bar => "bar",
        ChartKind.Area => "area",
        ChartKind.StackedBar => "stacked-bar",
        ChartKind.Scatter => "scatter",
        ChartKind.Pie => "pie",
        _ => "line",
    };

    public static ChartKind Parse(string? token) => token?.Trim().ToLowerInvariant() switch
    {
        "line" => ChartKind.Line,
        "bar" => ChartKind.Bar,
        "area" => ChartKind.Area,
        "stacked-bar" or "stackedbar" => ChartKind.StackedBar,
        "scatter" => ChartKind.Scatter,
        "pie" => ChartKind.Pie,
        _ => ChartKind.Line,
    };
}
