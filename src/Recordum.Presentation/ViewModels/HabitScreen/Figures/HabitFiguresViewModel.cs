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
    private sealed record Ctx(HabitNode Node, HabitConfig? Config, DayBoundary Day, DateOnly Today, DateOnly Created)
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
        var c = new Ctx(node, config, day, today, created);

        DailyTarget = null;
        Items = @case switch
        {
            HabitCase.Action => await ActionAsync(c, ct).ConfigureAwait(true),
            HabitCase.CounterTap => await CounterTapAsync(c, ct).ConfigureAwait(true),
            HabitCase.State => await StateAsync(c, ct).ConfigureAwait(true),
            HabitCase.Journal => await JournalAsync(c, ct).ConfigureAwait(true),
            _ => [],
        };
        UsualHour = @case == HabitCase.Abstinence ? null : await UsualHourAsync(c, ct).ConfigureAwait(true);
    }

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
        DailyTarget = goal is { PeriodType: GoalPeriodType.Rolling } && (goal.RollingDays ?? 1) <= 1 ? goal.TargetValue : null;
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

    // ---- fonti ----

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
    private static string Pct(int p) => string.Create(CultureInfo.CurrentCulture, $"{p}%");
    private static string Dec(decimal d) => Math.Round(d, 1, MidpointRounding.AwayFromZero).ToString("#,0.#", CultureInfo.CurrentCulture);

    public void Dispose() { }
}
