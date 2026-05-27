namespace Recordum.Plugins.Contracts.Charts;

/// <summary>Platform-neutral, UI-free description of a chart to render.</summary>
public sealed record ChartRenderModel(
    ChartKind Kind,
    string? Title,
    IReadOnlyList<ChartSeriesModel> Series,
    ChartAxisModel? XAxis,
    ChartAxisModel? YAxis,
    ChartThemeModel Theme);

public sealed record ChartSeriesModel(string Label, string? ColorHex, IReadOnlyList<ChartPoint> Points);

/// <summary>Cartesian: X numeric (or index) + Y. Categorical/pie: Label set, Y = value.</summary>
public readonly record struct ChartPoint(double X, double? Y, string? Label);

public sealed record ChartAxisModel(string? Label, bool IsTime, string? Format);

public sealed record ChartThemeModel(bool IsDark, string TextColorHex, string GridColorHex);
