using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Formatting;

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
    public int StepCount { get; }
    public int SliderMax => Math.Max(0, StepCount - 1);

    /// <summary>Above this step count the stepped slider drops the per-step captions (they would not fit)
    /// and shows only the current value; at or below it every step gets a labelled reference point.</summary>
    private const int MaxReferencePoints = 12;

    /// <summary>True when the stepped slider can caption every step (small scales: a 1–10 stress level, a
    /// 5-point mood). Larger scales fall back to a bare slider + current-value label.</summary>
    public bool ShowReferencePoints => Kind == MetricInputKind.Scale && StepCount <= MaxReferencePoints;

    /// <summary>Caption under each step of the stepped slider: the scale's text labels, or its bare numbers
    /// when it has none (e.g. a 1–10 scale). Empty for non-scale cells.</summary>
    public IReadOnlyList<string> ReferencePoints { get; } = [];

    [ObservableProperty] private long? _integerValue;
    [ObservableProperty] private decimal? _decimalValue;
    [ObservableProperty] private string? _textValue;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedScaleLabel))]
    private int? _selectedScaleIndex; // 0-based across steps

    /// <summary>True per la cella durata (Measured + Seconds): usa DurationText/mask invece di IntegerValue.</summary>
    public bool IsDuration => Kind == MetricInputKind.Measured && Metric.Type == MetricType.Seconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDurationInvalid))]
    private string? _durationText;

    public bool IsDurationInvalid =>
        !string.IsNullOrWhiteSpace(DurationText) && !DurationFormat.TryParse(DurationText, out _);

    public void SetDurationSeconds(int seconds) => DurationText = DurationFormat.Format(seconds);

    /// <summary>Label for the current slider position (falls back to the 1-based step number when a step
    /// has no label), so the slider reads "Good" instead of a bare number. Empty when nothing is picked.</summary>
    public string SelectedScaleLabel
    {
        get
        {
            if (SelectedScaleIndex is not { } i) return string.Empty;
            return i >= 0 && i < ScaleLabels.Count
                ? ScaleLabels[i]
                : (i + 1).ToString(CultureInfo.CurrentCulture);
        }
    }

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
            StepCount = scale.Labels.Count > 0 ? scale.Labels.Count : Math.Max(1, scale.Max - scale.Min + 1);
            // Scales are 1-based for the user: step i records value i+1, so the first step is 1 (never 0).
            ReferencePoints = scale.Labels.Count > 0
                ? scale.Labels
                : Enumerable.Range(1, StepCount).Select(n => n.ToString(CultureInfo.CurrentCulture)).ToList();
            // A slider always rests on a step, so the first one is selected by default: the thumb's
            // position is a real value (the first step is otherwise unreachable — you can't drag onto
            // where the thumb already sits), so a straight Save records it instead of "no value".
            SelectedScaleIndex = 0;
        }
    }

    public CompletionValue? BuildValue(int seriesIndex)
    {
        var v = new CompletionValue { MetricDefinitionId = Metric.Id, SeriesIndex = seriesIndex };
        switch (Kind)
        {
            case MetricInputKind.Scale:
                if (SelectedScaleIndex is null) return null;
                v.ValueInteger = SelectedScaleIndex.Value + 1; // 1-based: first step records 1, not 0
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
                        if (IntegerValue is null) return null;
                        v.ValueInteger = IntegerValue;
                        return v;
                    case MetricType.Seconds:
                        if (!DurationFormat.TryParse(DurationText, out var secs)) return null;
                        v.ValueSeconds = secs;
                        return v;
                    default:
                        return null;
                }
        }
    }
}
