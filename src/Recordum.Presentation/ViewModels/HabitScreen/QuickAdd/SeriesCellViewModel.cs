using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

/// <summary>Scale range + labels resolved for a scale cell. Labels may be empty (slider with bare numbers).</summary>
public sealed record ScaleInfo(int Min, int Max, IReadOnlyList<string> Labels);

/// <summary>One stepped-slider caption with its position along the track as a 0..1 fraction
/// (0 = first step / left thumb stop, 1 = last). Lets the captions sit exactly under the thumb
/// stops, which fall on (N-1) intervals — not the N cell-centres a uniform grid would give.</summary>
public sealed record ScalePoint(string Label, double Fraction);

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

    /// <summary>The reference points with their track fraction, so each tick can be centred on its
    /// thumb stop instead of a uniform-grid cell centre (which only lined up at the middle step).</summary>
    public IReadOnlyList<ScalePoint> ScalePoints { get; } = [];

    /// <summary>End-of-scale captions. Only the two extremes are labelled under the slider (worded
    /// labels centred on every stop collide); the accent caption above names the current step live.</summary>
    public string ScaleMinLabel => ReferencePoints.Count > 0 ? ReferencePoints[0] : string.Empty;
    public string ScaleMaxLabel => ReferencePoints.Count > 0 ? ReferencePoints[^1] : string.Empty;

    [ObservableProperty] private long? _integerValue;
    [ObservableProperty] private decimal? _decimalValue;
    [ObservableProperty] private string? _textValue;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedScaleLabel))]
    private int? _selectedScaleIndex; // 0-based across steps

    /// <summary>True per la cella durata (Measured + Seconds): usa DurationValue/DurationUnit invece di IntegerValue.</summary>
    public bool IsDuration => Kind == MetricInputKind.Measured && Metric.Type == MetricType.Seconds;

    /// <summary>The trailing unit label shows only for non-duration cells: a duration cell already
    /// carries its unit in the time-unit selector (h/min/sec), so repeating the metric's unit code
    /// beside it would show the unit twice ("9 sec … minutes"). U8 double-unit fix.</summary>
    public bool ShowUnitLabel => UnitLabel is not null && !IsDuration;

    [ObservableProperty] private decimal? _durationValue;
    [ObservableProperty] private TimeUnit _durationUnit = TimeUnit.Minutes;

    /// <summary>True while THIS cell's stopwatch is running (U8 live chrono): scopes the running
    /// clock readout and the stop icon to the timed cell only. Set by the parent quick-add VM.</summary>
    [ObservableProperty] private bool _isChronoRunning;

    /// <summary>Static so the XAML binds it via x:Static (one list app-wide).</summary>
    public static IReadOnlyList<TimeUnitOption> TimeUnitOptions { get; } =
    [
        new(TimeUnit.Hours, Strings.TimeUnit_Hours),
        new(TimeUnit.Minutes, Strings.TimeUnit_Minutes),
        new(TimeUnit.Seconds, Strings.TimeUnit_Seconds),
    ];

    public void SetDurationSeconds(int seconds)
    {
        DurationUnit = TimeUnit.Seconds;
        DurationValue = seconds;
    }

    /// <summary>Label for the current slider position (falls back to the 1-based step number when a step
    /// has no label), so the slider reads "Good" instead of a bare number. Empty when nothing is picked.</summary>
    public string SelectedScaleLabel
    {
        get
        {
            if (SelectedScaleIndex is not { } i) return string.Empty;
            return ScaleLabelText.At(ScaleLabels, i);
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
        if (IsDuration)
            DurationUnit = unitCode?.ToLowerInvariant() switch
            {
                "hours" => TimeUnit.Hours,
                "seconds" => TimeUnit.Seconds,
                _ => TimeUnit.Minutes,
            };
        if (Kind == MetricInputKind.Scale && scale is not null)
        {
            ScaleLabels = scale.Labels;
            StepCount = scale.Labels.Count > 0 ? scale.Labels.Count : Math.Max(1, scale.Max - scale.Min + 1);
            // Scales are 1-based for the user: step i records value i+1, so the first step is 1 (never 0).
            ReferencePoints = scale.Labels.Count > 0
                ? Enumerable.Range(0, scale.Labels.Count).Select(i => ScaleLabelText.At(scale.Labels, i)).ToList()
                : Enumerable.Range(1, StepCount).Select(n => n.ToString(CultureInfo.CurrentCulture)).ToList();
            var lastStep = Math.Max(1, StepCount - 1);
            ScalePoints = ReferencePoints
                .Select((label, i) => new ScalePoint(label, StepCount > 1 ? (double)i / lastStep : 0d))
                .ToList();
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
                        if (DurationValue is not { } dur) return null;
                        var factor = DurationUnit switch
                        {
                            TimeUnit.Hours => 3600m,
                            TimeUnit.Minutes => 60m,
                            _ => 1m,
                        };
                        // BuildValue runs live on every keystroke (row IsValid re-evaluates on
                        // PropertyChanged): an out-of-range value must read as "no value", never
                        // throw. The multiply itself can overflow System.Decimal for extreme input,
                        // so guard the whole conversion — not just the int range. Negatives are
                        // rejected too: a duration can't be negative.
                        try
                        {
                            var total = Math.Round(dur * factor);
                            if (total < 0 || total > int.MaxValue) return null;
                            v.ValueSeconds = (int)total;
                        }
                        catch (OverflowException) { return null; }
                        return v;
                    default:
                        return null;
                }
        }
    }
}
