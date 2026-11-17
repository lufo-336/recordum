using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Scales;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

/// <summary>The scale a scale cell records on (GIO-23): range, precision and anchors all live in the template.</summary>
public sealed record ScaleInfo(ScaleTemplate Scale);

/// <summary>One stepped-slider caption with its position along the track as a 0..1 fraction
/// (0 = first step / left thumb stop, 1 = last), the true <see cref="Value"/> it records,
/// and whether it is the value currently recorded on the selected day.</summary>
public sealed partial class ScalePoint(string label, double fraction, decimal value) : ObservableObject
{
    public string Label { get; } = label;
    public double Fraction { get; } = fraction;
    /// <summary>Il valore vero della scala che il gradino registra.</summary>
    public decimal Value { get; } = value;
    /// <summary>Il gradino registrato nel giorno scelto (lo scrive <c>MeasuredQuickAddViewModel.LoadDayAsync</c>).</summary>
    [ObservableProperty] private bool _isCurrent;
}

/// <summary>Input cell for one <see cref="MetricDefinition"/> in a recording row. Kind-aware:
/// measured (numeric + unit), scale (labelled selector or slider), or free text.</summary>
public sealed partial class SeriesCellViewModel : ScaleCursorBase
{
    public MetricDefinition Metric { get; }
    public MetricType Type => Metric.Type;
    public string Name => Metric.Name;

    public MetricInputKind Kind { get; }
    public string? UnitLabel { get; }

    // Scale-only: the cursor (Min/Max/Step/SliderValue/ValueText/CurrentLabel/Points) lives in ScaleCursorBase.
    public int StepCount => Scale is null ? 0 : Scale.MaxValue - Scale.MinValue + 1;

    /// <summary>True when every value can carry a reference tick (at most 12 values: a 1–10 stress level, a 5-point
    /// mood). Larger scales show only the slider, the exact field and the current reading.</summary>
    public bool ShowReferencePoints => Kind == MetricInputKind.Scale && StepCount is > 0 and <= ScaleRules.TickLimit;

    /// <summary>End-of-scale captions. Only the two extremes are labelled under the slider.</summary>
    public string ScaleMinLabel => ShowReferencePoints ? Points[0].Label : string.Empty;
    public string ScaleMaxLabel => ShowReferencePoints ? Points[^1].Label : string.Empty;

    [ObservableProperty] private long? _integerValue;
    [ObservableProperty] private decimal? _decimalValue;
    [ObservableProperty] private string? _textValue;

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

    public static MetricInputKind ResolveKind(MetricDefinition metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        if (metric.ScaleTemplateId is not null) return MetricInputKind.Scale;
        return metric.Type == MetricType.Text ? MetricInputKind.Text : MetricInputKind.Measured;
    }

    public SeriesCellViewModel(MetricDefinition metric, string? unitCode = null, ScaleInfo? scale = null, string? unitLabel = null)
    {
        ArgumentNullException.ThrowIfNull(metric);
        Metric = metric;
        Kind = ResolveKind(metric);
        UnitLabel = unitLabel ?? unitCode;
        if (IsDuration)
            DurationUnit = unitCode?.ToLowerInvariant() switch
            {
                "hours" => TimeUnit.Hours,
                "seconds" => TimeUnit.Seconds,
                _ => TimeUnit.Minutes,
            };
        if (Kind == MetricInputKind.Scale && scale is not null)
            UseScale(scale.Scale);   // a slider always rests on a value: the first one is selected by default
    }

    public CompletionValue? BuildValue(int seriesIndex)
    {
        var v = new CompletionValue { MetricDefinitionId = Metric.Id, SeriesIndex = seriesIndex };
        switch (Kind)
        {
            case MetricInputKind.Scale:
                if (Scale is null) return null;
                v.ValueDecimal = CurrentValue;   // the true value on the scale, never a position
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
