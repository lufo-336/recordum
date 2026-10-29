using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitScreen.Figures;

/// <summary>I quattro numeri della testa dell'abitudine (spec direzione visiva §6.2), per caso. Tutto viene dai
/// servizi che esistono (conteggi e somme per giorno, già col confine dell'abitudine; le serie dall'aggregatore):
/// nessuna query nuova. Le percentuali sono sui giorni previsti dal programma; ogni finestra parte dal più tardi fra
/// il suo inizio e il giorno di creazione.</summary>
public sealed partial class HabitFiguresViewModel : ObservableObject, IDisposable
{
    private readonly ICompletionService _completions;
    private readonly IStreakAggregator _streaks;
    private readonly IAbstinenceService _abstinence;
    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;
    private readonly IGoalService _goals;
    private readonly IHourlyDistributionQueryService _hourly;
    private readonly IClock _clock;
    private readonly IScheduleEvaluator _scheduler;
    private readonly ITickSourceFactory? _ticks;

    [ObservableProperty] private ImmutableArray<HabitFigure> _items = [];

    /// <summary>L'ora del picco degli ultimi 90 giorni ("di solito verso le …"); null senza dati e per l'astinenza.</summary>
    [ObservableProperty] private string? _usualHour;

    /// <summary>L'obiettivo giornaliero del contatore a tocco (per "5/8"): un obiettivo su un giorno mobile.</summary>
    [ObservableProperty] private decimal? _dailyTarget;

    public HabitFiguresViewModel(
        ICompletionService completions, IStreakAggregator streaks, IAbstinenceService abstinence,
        IMetricDefinitionService metrics, IReferenceDataQueryService refData, IGoalService goals,
        IHourlyDistributionQueryService hourly, IClock clock, IScheduleEvaluator? scheduler = null,
        ITickSourceFactory? ticks = null)
    {
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(streaks);
        ArgumentNullException.ThrowIfNull(abstinence);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(refData);
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(hourly);
        ArgumentNullException.ThrowIfNull(clock);
        _completions = completions;
        _streaks = streaks;
        _abstinence = abstinence;
        _metrics = metrics;
        _refData = refData;
        _goals = goals;
        _hourly = hourly;
        _clock = clock;
        _scheduler = scheduler ?? new Recordum.Data.Services.Evaluators.ScheduleEvaluator();
        _ticks = ticks;
    }

    /// <summary>Il contesto di un caricamento: oggi, il giorno di creazione e la finestra dei 30 giorni.</summary>
    private sealed record Ctx(HabitNode Node, HabitConfig? Config, DayBoundary Day, DateOnly Today, DateOnly Created, int Generation)
    {
        public DateOnly WindowStart(int days) => Max(Today.AddDays(-(days - 1)), Created);
        public int WindowDays(int days) => Today.DayNumber - WindowStart(days).DayNumber + 1;
        public static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    }

    public async Task LoadAsync(HabitNode node, HabitConfig? config, HabitCase @case, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        var today = day.Today(_clock);
        var created = day.DayOf(node.CreatedAt);
        if (created > today) created = today;
        // Un ricaricamento partito da un evento sull'abitudine di prima può finire dopo quello della nuova: vince l'ultimo.
        var generation = ++_generation;
        var c = new Ctx(node, config, day, today, created, generation);

        DailyTarget = null;
        _liveClock?.Stop();
        var items = @case switch
        {
            HabitCase.Action => await ActionAsync(c, ct).ConfigureAwait(true),
            HabitCase.CounterTap => await CounterTapAsync(c, ct).ConfigureAwait(true),
            HabitCase.State => await StateAsync(c, ct).ConfigureAwait(true),
            HabitCase.Journal => await JournalAsync(c, ct).ConfigureAwait(true),
            HabitCase.Measured => await MeasuredAsync(c, ct).ConfigureAwait(true),
            HabitCase.Series => await SeriesAsync(c, ct).ConfigureAwait(true),
            HabitCase.Session => await SessionAsync(c, ct).ConfigureAwait(true),
            HabitCase.Abstinence => await AbstinenceAsync(c, ct).ConfigureAwait(true),
            _ => [],
        };
        var usual = @case == HabitCase.Abstinence ? null : await UsualHourAsync(c, ct).ConfigureAwait(true);
        if (generation != _generation) return;
        Items = items;
        UsualHour = usual;
    }

    private int _generation;
    private bool IsCurrent(Ctx c) => c.Generation == _generation;

    // ---- casi ----

    private async Task<ImmutableArray<HabitFigure>> ActionAsync(Ctx c, CancellationToken ct)
    {
        var counts = await CountsAsync(c, ct).ConfigureAwait(true);
        var (current, best) = await StreaksAsync(c, ct).ConfigureAwait(true);
        var start = c.WindowStart(30);
        var scheduled = 0;
        var done = 0;
        for (var d = start; d <= c.Today; d = d.AddDays(1))
        {
            if (!IsScheduled(c, d)) continue;
            scheduled++;
            if (counts.ContainsKey(d)) done++;
        }
        var pct = scheduled == 0 ? 0 : (int)Math.Round(100.0 * done / scheduled, MidpointRounding.AwayFromZero);
        return
        [
            new(Int(current), Strings.Figures_CurrentStreak),
            new(Int(best), Strings.Figures_BestStreak),
            new(Pct(pct), Strings.Figures_Pct30),
            new(Int(counts.Count), Strings.Figures_Total),
        ];
    }

    private async Task<ImmutableArray<HabitFigure>> CounterTapAsync(Ctx c, CancellationToken ct)
    {
        var counts = await CountsAsync(c, ct).ConfigureAwait(true);
        var (current, _) = await StreaksAsync(c, ct).ConfigureAwait(true);
        var goal = await _goals.GetActiveAsync(c.Node.Id, ct).ConfigureAwait(true);
        if (IsCurrent(c)) DailyTarget = goal is { PeriodType: GoalPeriodType.Rolling } && (goal.RollingDays ?? 1) <= 1 ? goal.TargetValue : null;
        var window = WindowSum(c, counts, 30);
        return
        [
            new(Int(counts.GetValueOrDefault(c.Today)), Strings.Figures_Today),
            new(Dec((decimal)window / c.WindowDays(30)), Strings.Figures_Avg30),
            new(Int(current), Strings.Figures_GoalStreak),
            new(Int(counts.Values.Sum()), Strings.Figures_Total),
        ];
    }

    private async Task<ImmutableArray<HabitFigure>> StateAsync(Ctx c, CancellationToken ct)
    {
        var (metric, labels) = await ScaleAsync(c.Node.Id, ct).ConfigureAwait(true);
        var start = c.WindowStart(30);
        var values = metric is null
            ? new Dictionary<DateOnly, decimal>()
            : (await _completions.GetDailySumAsync(c.Node.Id, metric.Id, start, c.Today, ct).ConfigureAwait(true))
                .Where(v => v.Date >= start && v.Date <= c.Today).ToDictionary(v => v.Date, v => v.Sum);
        var todayLabel = values.TryGetValue(c.Today, out var tv) ? LabelFor(labels, (int)tv) : "—";
        var mean = values.Count == 0 ? (decimal?)null : values.Values.Average();
        var weekStart = c.Today.AddDays(-6);
        var last = values.Where(v => v.Key >= weekStart).Select(v => v.Value).ToList();
        var before = values.Where(v => v.Key < weekStart).Select(v => v.Value).ToList();
        var trend = last.Count == 0 || before.Count == 0 ? "—"
            : (last.Average() - before.Average()) switch
            {
                > 0.25m => "↗",
                < -0.25m => "↘",
                _ => "→",
            };
        return
        [
            new(todayLabel, Strings.Figures_Today),
            new(mean is { } m ? Dec(m) : "—", Strings.Figures_Avg30),
            new(trend, Strings.Figures_Trend),
            new($"{Int(values.Count)}/{Int(c.WindowDays(30))}", Strings.Figures_DaysMarked),
        ];
    }

    private async Task<ImmutableArray<HabitFigure>> JournalAsync(Ctx c, CancellationToken ct)
    {
        var counts = await CountsAsync(c, ct).ConfigureAwait(true);
        var (current, _) = await StreaksAsync(c, ct).ConfigureAwait(true);
        var monday = c.Today.AddDays(-(((int)c.Today.DayOfWeek + 6) % 7));
        var week = counts.Where(kv => kv.Key >= monday && kv.Key <= c.Today).Sum(kv => kv.Value);
        // "tag del mese" è facoltativo nella spec e non ha una fonte economica: escluso.
        return
        [
            new(Int(week), Strings.Figures_EntriesWeek),
            new(Int(current), Strings.Figures_Streak),
            new(Int(counts.Values.Sum()), Strings.Figures_Entries),
        ];
    }

    private async Task<ImmutableArray<HabitFigure>> MeasuredAsync(Ctx c, CancellationToken ct)
    {
        var metric = (await _metrics.ListByHabitAsync(c.Node.Id, ct).ConfigureAwait(true)).OrderBy(m => m.SortOrder).FirstOrDefault();
        var sums = await SumsAsync(c, metric, ct).ConfigureAwait(true);
        var isTime = metric?.Type == MetricType.Seconds;
        var start = c.WindowStart(30);
        var avg = sums.Where(kv => kv.Key >= start).Sum(kv => kv.Value) / c.WindowDays(30);
        var tail = new HabitFigure[]
        {
            new(Amount(avg, isTime), Strings.Figures_AvgPerDay),
            new(Amount(sums.Values.Sum(), isTime), Strings.Figures_Total),
        };

        var goal = new Detail.GoalProgressCardViewModel(_goals, _completions, _metrics, _refData, _clock);
        await goal.LoadAsync(c.Node, c.Day, ct).ConfigureAwait(true);
        if (!goal.IsEmpty)
        {
            var label = goal.PeriodLabel.Length > 0
                ? char.ToLower(goal.PeriodLabel[0], CultureInfo.CurrentCulture) + goal.PeriodLabel[1..]
                : goal.PeriodLabel;
            return [new(Dec(goal.Actual), label),
                    new(Pct((int)Math.Round(goal.PercentComplete * 100, MidpointRounding.AwayFromZero)), Strings.Figures_GoalPct),
                    .. tail];
        }
        var (current, _) = await StreaksAsync(c, ct).ConfigureAwait(true);
        return [new(Amount(sums.GetValueOrDefault(c.Today), isTime), Strings.Figures_Today),
                new(Int(current), Strings.Figures_CurrentStreak),
                .. tail];
    }

    private async Task<ImmutableArray<HabitFigure>> SeriesAsync(Ctx c, CancellationToken ct)
    {
        var counts = await CountsAsync(c, ct).ConfigureAwait(true);
        // "Volume" = il primo campo quantità (intero o decimale, non una scala), mai un prodotto fra campi.
        var quantity = (await _metrics.ListByHabitAsync(c.Node.Id, ct).ConfigureAwait(true))
            .Where(m => m.Type is MetricType.Integer or MetricType.Decimal && m.ScaleTemplateId is null)
            .OrderBy(m => m.SortOrder).FirstOrDefault();
        HabitFigure second;
        if (quantity is null)
        {
            second = new(Int(counts.Values.Sum()), Strings.Figures_SetsTotal);
        }
        else
        {
            var today = await _completions.GetDailySumAsync(c.Node.Id, quantity.Id, c.Today, c.Today, ct).ConfigureAwait(true);
            second = new(Dec(today.Where(v => v.Date == c.Today).Sum(v => v.Sum)),
                Fmt(Strings.Figures_VolumeToday, quantity.Name));
        }
        var start28 = c.WindowStart(28);
        var days28 = counts.Keys.Count(d => d >= start28);
        return
        [
            new(Int(counts.GetValueOrDefault(c.Today)), Strings.Figures_SetsToday),
            second,
            new(Dec(days28 * 7m / c.WindowDays(28)), Strings.Figures_DaysPerWeek),
            new(Int(counts.Count), Strings.Figures_Sessions),
        ];
    }

    private async Task<ImmutableArray<HabitFigure>> SessionAsync(Ctx c, CancellationToken ct)
    {
        // Il timer scrive la durata sulla registrazione (ActiveTimerService.StopAsync); "+ a mano" scrive il campo tempo.
        // Una registrazione conta la sua durata, altrimenti il primo campo tempo.
        var time = (await _metrics.ListByHabitAsync(c.Node.Id, ct).ConfigureAwait(true))
            .Where(m => m.Type == MetricType.Seconds).OrderBy(m => m.SortOrder).FirstOrDefault();
        var (from, toExclusive) = c.Day.RangeOf(c.Created, c.Today);
        var list = await _completions.ListByHabitAsync(c.Node.Id, from, toExclusive.AddMilliseconds(-1), ct: ct).ConfigureAwait(true);
        var seconds = list.GroupBy(x => c.Day.DayOf(x.CompletedAtMs)).ToDictionary(
            g => g.Key,
            g => (decimal)g.Sum(x => x.DurationSeconds
                ?? x.Values.Where(v => time is not null && v.MetricDefinitionId == time.Id).Sum(v => v.ValueSeconds ?? 0)));
        var (current, _) = await StreaksAsync(c, ct).ConfigureAwait(true);
        var start = c.WindowStart(30);
        var avg = seconds.Where(kv => kv.Key >= start).Sum(kv => kv.Value) / c.WindowDays(30);
        return
        [
            new(Amount(seconds.GetValueOrDefault(c.Today), isTime: true), Strings.Figures_Today),
            new(Int(current), Strings.Figures_Streak),
            new(Amount(avg, isTime: true), Strings.Figures_Avg30),
            new(Hours(seconds.Values.Sum()), Strings.Figures_Total),
        ];
    }

    private LiveElapsedClock? _liveClock;

    private async Task<ImmutableArray<HabitFigure>> AbstinenceAsync(Ctx c, CancellationToken ct)
    {
        var periods = await _abstinence.ListPeriodsAsync(c.Node.Id, c.Created, c.Today, ct).ConfigureAwait(true);
        var nowMs = _clock.UtcNowMs;
        double Days(AbstinencePeriod p) => ((p.EndedAtMs ?? nowMs) - p.StartedAtMs) / 86_400_000d;
        var longest = periods.Count == 0 ? 0 : (int)Math.Floor(periods.Max(Days));
        var clean = (int)Math.Floor(periods.Sum(Days));
        var since = c.Today.AddDays(-89);
        var lapses = periods.Count(p => p.EndedAtMs is { } e && c.Day.DayOf(e) >= since);

        var state = await _abstinence.GetStateAsync(c.Node.Id, ct).ConfigureAwait(true);
        string now;
        if (state.Status == AbstinenceStatus.Abstaining && state.CurrentStartedAtMs is { } startedMs && IsCurrent(c))
        {
            if (_liveClock is null)
            {
                _liveClock = new LiveElapsedClock(_clock, _ticks);
                _liveClock.PropertyChanged += OnLiveClockChanged;
            }
            _liveClock.Start(startedMs);
            now = _liveClock.Display;
        }
        else
        {
            now = Strings.Abstinence_NotStarted;
        }
        return
        [
            new(now, Strings.Figures_Now),
            new(DaysText(longest), Strings.Figures_Longest),
            new(Int(lapses), Strings.Figures_Lapses90),
            new(DaysText(clean), Strings.Figures_CleanDays),
        ];
    }

    /// <summary>L'orologio vivo dell'astinenza riscrive il primo numero a ogni secondo.</summary>
    private void OnLiveClockChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LiveElapsedClock.Display) || _liveClock is not { Display.Length: > 0 } clock) return;
        if (Items.Length > 0 && Items[0].Caption == Strings.Figures_Now)
            Items = Items.SetItem(0, Items[0] with { Value = clock.Display });
    }

    // ---- fonti ----

    private async Task<Dictionary<DateOnly, decimal>> SumsAsync(Ctx c, MetricDefinition? metric, CancellationToken ct)
        => metric is null ? []
            : (await _completions.GetDailySumAsync(c.Node.Id, metric.Id, c.Created, c.Today, ct).ConfigureAwait(true))
                .Where(v => v.Date >= c.Created && v.Date <= c.Today)
                .GroupBy(v => v.Date).ToDictionary(g => g.Key, g => g.Sum(v => v.Sum));

    /// <summary>Registrazioni per giorno dalla creazione a oggi (i giorni senza dati non ci sono).</summary>
    private async Task<Dictionary<DateOnly, int>> CountsAsync(Ctx c, CancellationToken ct)
        => (await _completions.GetHeatmapAsync(c.Node.Id, c.Created, c.Today, ct).ConfigureAwait(true))
            .Where(h => h.Count > 0 && h.Date >= c.Created && h.Date <= c.Today)
            .GroupBy(h => h.Date).ToDictionary(g => g.Key, g => g.Sum(h => h.Count));

    private static int WindowSum(Ctx c, Dictionary<DateOnly, int> counts, int days)
    {
        var start = c.WindowStart(days);
        return counts.Where(kv => kv.Key >= start).Sum(kv => kv.Value);
    }

    private async Task<(int Current, int Best)> StreaksAsync(Ctx c, CancellationToken ct)
    {
        var runs = await _streaks.GetHistoryAsync(c.Node.Id, c.Created, c.Today, ct).ConfigureAwait(true);
        return (runs.FirstOrDefault(r => r.IsCurrent)?.Length ?? 0, runs.Count == 0 ? 0 : runs.Max(r => r.Length));
    }

    private bool IsScheduled(Ctx c, DateOnly d)
    {
        var cfg = c.Config;
        return _scheduler.IsScheduled(d, new ScheduledRules(
            cfg?.ScheduleType ?? ScheduleType.Daily, cfg?.ScheduleDaysJson, cfg?.ScheduleWeeklyTarget,
            cfg?.ScheduleEveryXDays, cfg?.ScheduleMonthlyDaysJson, c.Created));
    }

    private async Task<(MetricDefinition? Metric, string[] Labels)> ScaleAsync(Guid habitId, CancellationToken ct)
    {
        var metrics = await _metrics.ListByHabitAsync(habitId, ct).ConfigureAwait(true);
        var primary = metrics.Where(m => m.ScaleTemplateId is not null).OrderBy(m => m.SortOrder).FirstOrDefault()
                      ?? metrics.OrderBy(m => m.SortOrder).FirstOrDefault();
        if (primary?.ScaleTemplateId is not { } sid) return (primary, []);
        var tmpl = (await _refData.ListScalesAsync(ct).ConfigureAwait(true)).FirstOrDefault(s => s.Id == sid);
        return (primary, ParseLabels(primary.LabelsJson) ?? ParseLabels(tmpl?.LabelsJson) ?? []);
    }

    /// <summary>Il valore salvato della scala è 1-based (come in <c>StateTodayContent</c>).</summary>
    private static string LabelFor(string[] labels, int value)
        => value >= 1 && value <= labels.Length ? labels[value - 1] : Int(value);

    private static string[]? ParseLabels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<string[]>(json); }
        catch (JsonException) { return null; }
    }

    private async Task<string?> UsualHourAsync(Ctx c, CancellationToken ct)
    {
        var hours = await _hourly.GetHourlyAsync(c.Node.Id, c.Today.AddDays(-89), c.Today, ct).ConfigureAwait(true);
        var peak = -1;
        var max = 0;
        for (var h = 0; h < hours.Count; h++)
            if (hours[h] > max) { max = hours[h]; peak = h; }
        return peak < 0 ? null : new TimeOnly(peak, 0).ToString("t", CultureInfo.CurrentCulture);
    }

    // ---- formati ----

    private static string Int(int n) => n.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>Una quantità, o un tempo in secondi scritto in minuti interi.</summary>
    private static string Amount(decimal value, bool isTime) => isTime
        ? Fmt(Strings.Figures_Value_Minutes, Int((int)Math.Round(value / 60m, MidpointRounding.AwayFromZero)))
        : Dec(value);

    /// <summary>Secondi in ore: una cifra decimale sotto le 10 ore, intere da lì.</summary>
    private static string Hours(decimal seconds)
    {
        var h = seconds / 3600m;
        var text = h < 10 ? Dec(h) : Int((int)Math.Round(h, MidpointRounding.AwayFromZero));
        return Fmt(Strings.Figures_Value_Hours, text);
    }

#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI strings
    private static string Fmt(string format, string arg) => string.Format(CultureInfo.CurrentCulture, format, arg);
#pragma warning restore CA1863

    private static string DaysText(int days) => Fmt(Strings.Figures_Value_Days, Int(days));
    private static string Pct(int p) => string.Create(CultureInfo.CurrentCulture, $"{p}%");
    private static string Dec(decimal d) => Math.Round(d, 1, MidpointRounding.AwayFromZero).ToString("#,0.#", CultureInfo.CurrentCulture);

    public void Dispose()
    {
        if (_liveClock is null) return;
        _liveClock.PropertyChanged -= OnLiveClockChanged;
        _liveClock.Dispose();
    }
}
