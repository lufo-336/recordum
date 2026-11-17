using System.Globalization;
using Recordum.Presentation.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen.Today;

/// <summary>State "Oggi" content (spec §1): a quick stepped slider for today plus the last recorded value.
/// Resolves the scale the same way the quick-add cell does — the metric's <c>LabelsJson</c> override wins
/// over the <see cref="ScaleTemplate"/> (CSS-style specificity). Recording is last-wins for the day.
/// Degrades to a bare numeric slider (0–10) when the scale is missing (e.g. data wiped by 4d01f9c).</summary>
public sealed partial class StateTodayContent : ObservableObject, ITodayContent
{
    private const int FallbackMax = 10;

    private readonly ICompletionService _completions;
    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;
    private readonly IClock _clock;
    private Guid _habitId;
    private Guid? _metricId;
    private string[] _labels = [];

    [ObservableProperty] private int _min;
    [ObservableProperty] private int _max = FallbackMax;
    [ObservableProperty] private int _sliderValue;
    [ObservableProperty] private string _currentLabel = string.Empty;
    [ObservableProperty] private string? _lastValueLabel;

    /// <summary>False when the scale could not be resolved to a real metric — the record button is disabled
    /// (the slider still moves so the strip never looks broken).</summary>
    public bool CanRecord => _metricId is not null;

    public StateTodayContent(
        ICompletionService completions, IMetricDefinitionService metrics,
        IReferenceDataQueryService refData, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(refData);
        ArgumentNullException.ThrowIfNull(clock);
        _completions = completions;
        _metrics = metrics;
        _refData = refData;
        _clock = clock;
    }

    /// <summary>Il confine ricevuto dallo screen. Lo usano sia la lettura del valore di oggi sia la
    /// FINESTRA del last-wins in <see cref="RecordAsync"/>: con un confine sbagliato quest'ultima
    /// cancella le registrazioni del giorno vicino, non di quello che l'utente sta compilando.</summary>
    private DayBoundary _day = DayBoundary.Utc;

    public async Task LoadAsync(HabitNode node, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        _habitId = node.Id;
        _day = day;

        await ResolveScaleAsync(ct).ConfigureAwait(true);

        // Seed the slider from today's recorded value (if any); Max is set before Value so it isn't coerced.
        var (index, hasValue) = await ReadTodayIndexAsync(ct).ConfigureAwait(true);
        SliderValue = hasValue ? Math.Clamp(index, Min, Max) : Min;
        LastValueLabel = hasValue ? LabelFor(index) : null;
        CurrentLabel = LabelFor(SliderValue);
    }

    private async Task ResolveScaleAsync(CancellationToken ct)
    {
        var metrics = (await _metrics.ListByHabitAsync(_habitId, ct).ConfigureAwait(true)).ToList();
        var primary = metrics.Where(m => m.ScaleTemplateId is not null).OrderBy(m => m.SortOrder).FirstOrDefault()
                      ?? metrics.OrderBy(m => m.SortOrder).FirstOrDefault();
        _metricId = primary?.Id;
        OnPropertyChanged(nameof(CanRecord));

        var stepCount = FallbackMax + 1;
        if (primary?.ScaleTemplateId is { } sid)
        {
            var scales = await _refData.ListScalesAsync(ct).ConfigureAwait(true);
            var tmpl = scales.FirstOrDefault(s => s.Id == sid);
            if (tmpl is not null)
            {
                // One reading per value, from ScaleText (GIO-23); recording by true value is Task 5.
                _labels = Enumerable.Range(tmpl.MinValue, tmpl.MaxValue - tmpl.MinValue + 1).Select(v => ScaleText.Short(tmpl, v)).ToArray();
                stepCount = Math.Max(1, _labels.Length);
            }
        }
        else
        {
            _labels = [];
        }

        Min = 0;
        Max = Math.Max(0, stepCount - 1);
    }

    /// <summary>Today's recorded scale index (stored value is 1-based, so index = value − 1), and whether
    /// a value exists. Reads the day magnitude (same source the calendar uses).</summary>
    private async Task<(int Index, bool HasValue)> ReadTodayIndexAsync(CancellationToken ct)
    {
        var today = _day.Today(_clock);
        var mags = await _completions.GetDailyMagnitudeAsync(_habitId, today.Year, today.Month, ct).ConfigureAwait(true);
        var todayMag = mags.FirstOrDefault(m => m.Date == today);
        if (todayMag is null) return (0, false);
        return ((int)todayMag.Magnitude - 1, true);
    }

    partial void OnSliderValueChanged(int value) => CurrentLabel = LabelFor(value);

    private string LabelFor(int index)
        => index >= 0 && index < _labels.Length ? _labels[index] : (index + 1).ToString(CultureInfo.CurrentCulture);


    [RelayCommand]
    private async Task RecordAsync()
    {
        if (_metricId is not { } metricId) return;

        // Last-wins for the day: drop any existing completion(s) recorded today, then record the new value.
        var today = _day.Today(_clock);
        // Tetto inclusivo al millisecondo come vuole il filtro del servizio: qui conta il doppio,
        // perché questa finestra decide quali registrazioni CANCELLARE (last-wins).
        var (dayStart, dayEndExclusive) = _day.RangeOf(today);
        var dayEnd = dayEndExclusive.AddMilliseconds(-1);
        var existing = await _completions.ListByHabitAsync(_habitId, dayStart, dayEnd).ConfigureAwait(true);
        foreach (var c in existing) await _completions.DeleteAsync(c.Id).ConfigureAwait(true);

        var draft = new Completion
        {
            HabitNodeId = _habitId,
            CompletedAt = _clock.UtcNow.UtcDateTime,
            CompletedAtMs = _clock.UtcNowMs,
        };
        var value = new CompletionValue
        {
            MetricDefinitionId = metricId,
            SeriesIndex = 0,
            ValueInteger = SliderValue + 1, // 1-based, mirroring the quick-add cell
        };
        await _completions.RecordAsync(draft, new[] { value }).ConfigureAwait(true);

        LastValueLabel = LabelFor(SliderValue);
    }
}
