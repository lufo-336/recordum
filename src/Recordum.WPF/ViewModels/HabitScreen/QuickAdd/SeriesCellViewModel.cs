using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Scale range + labels resolved for a scale cell. Labels may be empty (slider with bare numbers).</summary>
public sealed record ScaleInfo(int Min, int Max, IReadOnlyList<string> Labels);

/// <summary>Input cell for one <see cref="MetricDefinition"/> in a recording row. Kind-aware:
/// measured (numeric + unit), scale (labelled selector or slider), or free text.</summary>
public sealed partial class SeriesCellViewModel : ObservableObject
{
    public MetricDefinition Metric { get; }
    public MetricType Type => Metric.Type;
    public string Name => Metric.Name;

    public MetricInputKind Kind { get; }
    public string? UnitLabel { get; }

    // Scale-only
    public IReadOnlyList<string> ScaleLabels { get; } = [];
    public int ScaleMin { get; }
    public int StepCount { get; }
    public bool UseSlider { get; }
    public int SliderMax => Math.Max(0, StepCount - 1);

    [ObservableProperty] private long? _integerValue;
    [ObservableProperty] private decimal? _decimalValue;
    [ObservableProperty] private string? _textValue;
    [ObservableProperty] private int? _selectedScaleIndex; // 0-based across steps

    public static MetricInputKind ResolveKind(MetricDefinition metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        if (metric.ScaleTemplateId is not null) return MetricInputKind.Scale;
        return metric.Type == MetricType.Text ? MetricInputKind.Text : MetricInputKind.Measured;
    }

    public SeriesCellViewModel(MetricDefinition metric, string? unitCode = null, ScaleInfo? scale = null)
    {
        ArgumentNullException.ThrowIfNull(metric);
        Metric = metric;
        Kind = ResolveKind(metric);
        UnitLabel = unitCode;
        if (Kind == MetricInputKind.Scale && scale is not null)
        {
            ScaleLabels = scale.Labels;
            ScaleMin = scale.Min;
            StepCount = scale.Labels.Count > 0 ? scale.Labels.Count : Math.Max(1, scale.Max - scale.Min + 1);
            UseSlider = StepCount > 10;
        }
    }

    public CompletionValue? BuildValue(int seriesIndex)
    {
        var v = new CompletionValue { MetricDefinitionId = Metric.Id, SeriesIndex = seriesIndex };
        switch (Kind)
        {
            case MetricInputKind.Scale:
                if (SelectedScaleIndex is null) return null;
                v.ValueInteger = SelectedScaleIndex; // 0-based, preserves StateQuickAdd encoding
                return v;
            case MetricInputKind.Text:
                if (string.IsNullOrWhiteSpace(TextValue)) return null;
                v.ValueText = TextValue;
                return v;
            default: // Measured
                switch (Metric.Type)
                {
                    case MetricType.Decimal:
                        if (DecimalValue is null) return null;
                        v.ValueDecimal = DecimalValue;
                        return v;
                    case MetricType.Integer:
                    case MetricType.Seconds:
                        if (IntegerValue is null) return null;
                        v.ValueInteger = IntegerValue;
                        return v;
                    default:
                        return null;
                }
        }
    }
}
