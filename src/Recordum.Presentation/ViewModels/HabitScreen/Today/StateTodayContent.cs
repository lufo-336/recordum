using System.Globalization;
using Recordum.Presentation.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Scales;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen.Today;

/// <summary>State "Oggi" content (spec §1): a quick stepped slider for today plus the last recorded value.
/// Resolves the scale the same way the quick-add cell does — the metric's <c>LabelsJson</c> override wins
/// over the <see cref="ScaleTemplate"/> (CSS-style specificity). Recording is last-wins for the day.
/// Records the TRUE value on the scale (GIO-23). Degrades to a bare numeric slider (0–10) that cannot record when
/// the scale is missing (e.g. data wiped by 4d01f9c).</summary>
public sealed partial class StateTodayContent : ScaleCursorBase, ITodayContent
{
    private readonly ICompletionService _completions;
    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;
    private readonly IClock _clock;
    private Guid _habitId;
    private Guid? _metricId;

    [ObservableProperty] private string? _lastValueLabel;

    /// <summary>False when the scale could not be resolved to a real metric — the record button is disabled
    /// (the slider still moves so the strip never looks broken).</summary>
    public bool CanRecord => _metricId is not null && Scale is not null;

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

        // Seed the cursor from today's recorded value (if any); the scale is set before the value so it isn't coerced.
        var today = await ReadTodayAsync(ct).ConfigureAwait(true);
        SetValue(today is { } v ? (double)v : Min);
        LastValueLabel = today is { } t && Scale is not null ? ScaleText.Long(Scale, t) : null;
    }

    private async Task ResolveScaleAsync(CancellationToken ct)
    {
        var metrics = (await _metrics.ListByHabitAsync(_habitId, ct).ConfigureAwait(true)).ToList();
        var primary = metrics.Where(m => m.ScaleTemplateId is not null).OrderBy(m => m.SortOrder).FirstOrDefault()
                      ?? metrics.OrderBy(m => m.SortOrder).FirstOrDefault();
        _metricId = primary?.Id;

        ScaleTemplate? tmpl = null;
        if (primary?.ScaleTemplateId is { } sid)
            tmpl = (await _refData.ListScalesAsync(ct).ConfigureAwait(true)).FirstOrDefault(s => s.Id == sid);
        UseScale(tmpl);
        OnPropertyChanged(nameof(CanRecord));
    }

    /// <summary>Today's recorded value on the scale (the true value, not a position), or null. Reads the day magnitude
    /// (same source the calendar uses).</summary>
    private async Task<decimal?> ReadTodayAsync(CancellationToken ct)
    {
        if (Scale is null) return null;
        var today = _day.Today(_clock);
        var mags = await _completions.GetDailyMagnitudeAsync(_habitId, today.Year, today.Month, ct).ConfigureAwait(true);
        var todayMag = mags.FirstOrDefault(m => m.Date == today);
        return todayMag is null ? null : ScaleRules.Normalize((decimal)todayMag.Magnitude, Scale.MinValue, Scale.MaxValue, Scale.Precision);
    }

    [RelayCommand]
    private async Task RecordAsync()
    {
        // AsyncRelayCommand.ExecuteAsync non consulta CanExecute: la guardia sta anche qui.
        if (_metricId is not { } metricId || Scale is null) return;

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
            ValueDecimal = CurrentValue,   // the true value, never a position
        };
        await _completions.RecordAsync(draft, new[] { value }).ConfigureAwait(true);

        LastValueLabel = ScaleText.Long(Scale, CurrentValue);
    }
}
