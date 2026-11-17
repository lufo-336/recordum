using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Figures;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Figures;

/// <summary>I quattro numeri della testa (spec §6.2). Orologio al 28/10/2026 alle 10:00 UTC, reset alle 05:00,
/// cultura it-IT.</summary>
public sealed class HabitFiguresTests : IDisposable
{
    private static readonly DayBoundary Boundary = new(TimeZoneInfo.Utc, new TimeOnly(5, 0));
    private static readonly DateOnly Today = new(2026, 10, 28);
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture, _uiCulture = CultureInfo.CurrentUICulture;

    private readonly FakeCompletionService _completions = new();
    private readonly FakeStreakAggregator _streaks = new();
    private readonly FakeAbstinenceService _abstinence = new();
    private readonly FakeMetricDefinitionService _metrics = new();
    private readonly FakeReferenceData _refData = new();
    private readonly FakeGoalService _goals = new();
    private readonly FakeHourlyDistributionQueryService _hourly = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _id = Guid.NewGuid();

    public HabitFiguresTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("it-IT");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    private HabitFiguresViewModel Vm() => new(_completions, _streaks, _abstinence, _metrics, _refData, _goals, _hourly, _clock);

    private HabitNode Node(HabitType type, DateTime? created = null) => new()
    {
        Id = _id, NodeType = NodeType.Habit, HabitType = type, Name = "h",
        CreatedAt = created ?? new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
    };

    private static HabitConfig Daily() => new() { ScheduleType = ScheduleType.Daily };

    private void Heat(params (DateOnly Day, int Count)[] cells)
        => _completions.HeatmapByHabit[_id] = cells.Select(c => new HeatmapCell(c.Day, c.Count)).ToList();

    private static IEnumerable<(DateOnly, int)> Days(DateOnly from, DateOnly to, int count = 1)
    {
        for (var d = from; d <= to; d = d.AddDays(1)) yield return (d, count);
    }

    private static string[] Values(HabitFiguresViewModel vm) => vm.Items.Select(f => f.Value).ToArray();

    [Fact]
    public async Task A_superseded_load_does_not_overwrite_the_newer_figures()
    {
        // Review dell'onda 2a: un ricaricamento partito per l'abitudine di prima e finito dopo mostrava i suoi numeri.
        Heat(Days(Today.AddDays(-2), Today).ToArray());
        var vm = Vm();
        var gate = new TaskCompletionSource();
        _completions.ListGate = gate.Task;
        var older = vm.LoadAsync(Node(HabitType.Counter), Daily(), HabitCase.Session, Boundary);
        _completions.ListGate = null;
        await vm.LoadAsync(Node(HabitType.Action), Daily(), HabitCase.Action, Boundary);
        var newer = Values(vm);
        gate.SetResult();
        await older;

        Values(vm).Should().Equal(newer);
        vm.Items.Select(f => f.Caption).Should().Contain("30 giorni", "restano i numeri dell'azione");
    }

    [Fact]
    public async Task Action_figures()
    {
        // 19 dei 30 giorni fatti → 63%; 148 giorni in tutto; serie attuale 4, migliore 12.
        var done = Days(Today.AddDays(-18), Today).ToList();
        done.AddRange(Days(new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 1).AddDays(128)));
        Heat(done.ToArray());
        _streaks.Runs.Add(new StreakRun(Today.AddDays(-3), Today, 4, IsCurrent: true));
        _streaks.Runs.Add(new StreakRun(new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 12), 12));

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Action), Daily(), HabitCase.Action, Boundary);

        Values(vm).Should().Equal("4", "12", "63%", "148");
        vm.Items.Select(f => f.Caption).Should().Equal("di fila ora", "record di fila", "30 giorni", "in tutto");
    }

    [Fact]
    public async Task Total_counts_records_before_creation()
    {
        // UX2A-1: creata il 3/1/2026, ma registrata dal 28/12/2025: l'inizio e' il piu' vecchio dei due.
        Heat(Days(new DateOnly(2025, 12, 28), new DateOnly(2025, 12, 30)).ToArray());
        var node = Node(HabitType.Action, new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc));

        var vm = Vm();
        await vm.LoadAsync(node, Daily(), HabitCase.Action, Boundary, new DateOnly(2025, 12, 28));

        vm.Items[3].Value.Should().Be("3");
    }

    [Fact]
    public async Task Pct30_window_starts_at_the_first_record()
    {
        // Creata 2 giorni fa, ma la prima registrazione e' di 10 giorni fa: la finestra e' di 11 giorni, 6 fatti = 55%.
        Heat(Days(Today.AddDays(-10), Today.AddDays(-6)).Append((Today, 1)).ToArray());
        var node = Node(HabitType.Action, new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc));

        var vm = Vm();
        await vm.LoadAsync(node, Daily(), HabitCase.Action, Boundary, Today.AddDays(-10));

        vm.Items[2].Value.Should().Be("55%");
    }

    [Fact]
    public async Task Action_pct_counts_only_scheduled_days()
    {
        Heat(Days(Today.AddDays(-29), Today).Where(d => d.Item1.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday).ToArray());
        var mwf = new HabitConfig { ScheduleType = ScheduleType.SpecificDays, ScheduleDaysJson = "[\"Mon\",\"Wed\",\"Fri\"]" };

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Action), mwf, HabitCase.Action, Boundary);
        vm.Items[2].Value.Should().Be("100%");

        await vm.LoadAsync(Node(HabitType.Action), Daily(), HabitCase.Action, Boundary);
        vm.Items[2].Value.Should().NotBe("100%");
    }

    [Fact]
    public async Task CounterTap_figures()
    {
        // oggi 5; 30 giorni da 6 + oggi 5 = (29*6+5)/30 = 5,97 → "6"; somma in tutto 1.932.
        var cells = Days(Today.AddDays(-29), Today.AddDays(-1), 6).ToList();
        cells.Add((Today, 5));
        cells.Add((new DateOnly(2025, 6, 1), 1932 - 29 * 6 - 5));
        Heat(cells.ToArray());
        _streaks.Runs.Add(new StreakRun(Today.AddDays(-8), Today, 9, IsCurrent: true));
        _goals.Store[Guid.NewGuid()] = new Goal { HabitNodeId = _id, TargetValue = 8, PeriodType = GoalPeriodType.Rolling, RollingDays = 1 };

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Counter), Daily(), HabitCase.CounterTap, Boundary);

        Values(vm).Should().Equal("5", "6", "9", "1.932");
        vm.DailyTarget.Should().Be(8);
    }

    [Fact]
    public async Task Without_a_daily_goal_there_is_no_target()
    {
        _goals.Store[Guid.NewGuid()] = new Goal { HabitNodeId = _id, TargetValue = 50, PeriodType = GoalPeriodType.Rolling, RollingDays = 7 };

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Counter), Daily(), HabitCase.CounterTap, Boundary);

        vm.DailyTarget.Should().BeNull();
    }

    private Guid ScaleMetric(params string[] names)
    {
        if (names.Length == 0) names = ["male", "così così", "ok", "bene", "molto bene"];
        var scale = _refData.Scales[0];
        scale.MinValue = 1; scale.MaxValue = names.Length; scale.Anchors = TestScales.Anchors(1, names);
        var m = new MetricDefinition
        {
            Id = Guid.NewGuid(), HabitNodeId = _id, Name = "umore", Type = MetricType.Integer,
            ScaleTemplateId = _refData.Scales[0].Id,
        };
        _metrics.Store[m.Id] = m;
        return m.Id;
    }

    [Fact]
    public async Task State_figures()
    {
        var metric = ScaleMetric();
        // 27 giorni segnati su 30, tutti a 4 ("bene") tranne oggi a 4 → media 4; oggi "bene".
        var rows = Days(Today.AddDays(-26), Today).Select(d => new DailyValue(d.Item1, 4)).ToList();
        _completions.DailySumByKey[(_id, metric)] = rows;

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.State), Daily(), HabitCase.State, Boundary);

        vm.Items[0].Value.Should().Be("bene");
        vm.Items[1].Value.Should().Be("4");
        vm.Items[3].Value.Should().Be("27/30");
        vm.Items.Select(f => f.Caption).Should().Equal("oggi", "media 30 g", "andamento", "giorni segnati");
    }

    [Fact]
    public async Task State_today_figure_on_an_unnamed_step_reads_the_nearest_name_and_the_number()
    {
        var metric = ScaleMetric("male", "", "ok");
        _completions.DailySumByKey[(_id, metric)] = [new DailyValue(Today, 2)];

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.State), Daily(), HabitCase.State, Boundary);

        vm.Items[0].Value.Should().Be("male · 2");
    }

    [Theory]
    [InlineData(2, 4, "↗")]
    [InlineData(4, 2, "↘")]
    [InlineData(3, 3, "→")]
    public async Task State_trend_arrows(int before, int lastWeek, string arrow)
    {
        var metric = ScaleMetric();
        var rows = Days(Today.AddDays(-29), Today.AddDays(-7)).Select(d => new DailyValue(d.Item1, before))
            .Concat(Days(Today.AddDays(-6), Today).Select(d => new DailyValue(d.Item1, lastWeek))).ToList();
        _completions.DailySumByKey[(_id, metric)] = rows;

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.State), Daily(), HabitCase.State, Boundary);

        vm.Items[2].Value.Should().Be(arrow);
    }

    [Fact]
    public async Task State_trend_without_one_side_is_a_dash()
    {
        var metric = ScaleMetric();
        _completions.DailySumByKey[(_id, metric)] = [new DailyValue(Today, 3)];

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.State), Daily(), HabitCase.State, Boundary);

        vm.Items[2].Value.Should().Be("—");
        vm.Items[0].Value.Should().Be("ok");
    }

    [Fact]
    public async Task Journal_has_three_figures()
    {
        // mercoledì 28: la settimana parte lunedì 26 → 3 voci in settimana (26, 27, 28), 10 in tutto.
        Heat(Days(Today.AddDays(-2), Today).Concat(Days(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7))).ToArray());
        _streaks.Runs.Add(new StreakRun(Today.AddDays(-2), Today, 3, IsCurrent: true));

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Journal), Daily(), HabitCase.Journal, Boundary);

        Values(vm).Should().Equal("3", "3", "10");
        vm.Items.Select(f => f.Caption).Should().Equal("voci della settimana", "di fila", "voci");
    }

    [Fact]
    public async Task Before_the_reset_today_is_yesterday()
    {
        _clock.Set(new DateTimeOffset(2026, 10, 28, 4, 30, 0, TimeSpan.Zero));
        Heat((Today.AddDays(-1), 7), (Today, 2));

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Counter), Daily(), HabitCase.CounterTap, Boundary);

        vm.Items[0].Value.Should().Be("7");
    }

    [Fact]
    public async Task New_habit_has_sane_figures()
    {
        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Action, new DateTime(2026, 10, 28, 8, 0, 0, DateTimeKind.Utc)), Daily(), HabitCase.Action, Boundary);
        Values(vm).Should().Equal("0", "0", "0%", "0");

        Heat(Days(Today.AddDays(-4), Today).ToArray());
        await vm.LoadAsync(Node(HabitType.Action, new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc)), Daily(), HabitCase.Action, Boundary);
        vm.Items[2].Value.Should().Be("100%");

        await vm.LoadAsync(Node(HabitType.Counter, new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc)), Daily(), HabitCase.CounterTap, Boundary);
        vm.Items[1].Value.Should().Be("1");
        vm.Items.Should().OnlyContain(f => !f.Value.Contains("NaN") && !f.Value.Contains('∞') && !f.Value.StartsWith('-'));
    }

    [Fact]
    public async Task Usual_hour_is_the_peak_hour()
    {
        _hourly.Buckets[7] = 3;
        _hourly.Buckets[19] = 9;

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Action), Daily(), HabitCase.Action, Boundary);

        vm.UsualHour.Should().Be("19:00");
    }

    [Fact]
    public async Task No_hourly_data_means_no_usual_hour()
    {
        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Action), Daily(), HabitCase.Action, Boundary);

        vm.UsualHour.Should().BeNull();
    }

    // ---- Task 3: misurato, serie, sessione, astinenza ----

    private MetricDefinition Metric(string name, MetricType type, int sort = 0)
    {
        var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = _id, Name = name, Type = type, SortOrder = sort };
        _metrics.Store[m.Id] = m;
        return m;
    }

    private void Record(DateTimeOffset at, int? durationSeconds = null, params (MetricDefinition Metric, decimal Value)[] values)
    {
        var c = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = _id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
            DurationSeconds = durationSeconds,
        };
        foreach (var (m, v) in values)
            c.Values.Add(new CompletionValue
            {
                MetricDefinitionId = m.Id,
                ValueInteger = m.Type == MetricType.Integer ? (long)v : null,
                ValueDecimal = m.Type == MetricType.Decimal ? v : null,
                ValueSeconds = m.Type == MetricType.Seconds ? (int)v : null,
            });
        _completions.Store[c.Id] = c;
    }

    [Fact]
    public async Task Measured_with_goal()
    {
        var pages = Metric("pagine", MetricType.Integer);
        Record(new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero), null, (pages, 60));
        Record(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero), null, (pages, 36));
        _completions.DailySumByKey[(_id, pages.Id)] = [new DailyValue(Today.AddDays(-1), 60), new DailyValue(Today, 36)];
        _goals.Store[Guid.NewGuid()] = new Goal
        {
            HabitNodeId = _id, TargetValue = 150, MetricDefinitionId = pages.Id, PeriodType = GoalPeriodType.Rolling, RollingDays = 7,
        };

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Counter), Daily(), HabitCase.Measured, Boundary);

        vm.Items[0].Value.Should().Be("96");
        vm.Items[0].Caption.Should().Be("ultimi 7 giorni");
        vm.Items[1].Value.Should().Be("64%");
        vm.Items[1].Caption.Should().Be("obiettivo");
        vm.Items[2].Caption.Should().Be("media al giorno");
        vm.Items[3].Value.Should().Be("96");
    }

    [Fact]
    public async Task Measured_without_goal()
    {
        var pages = Metric("pagine", MetricType.Integer);
        _completions.DailySumByKey[(_id, pages.Id)] = [new DailyValue(Today, 20)];
        _streaks.Runs.Add(new StreakRun(Today, Today, 1, IsCurrent: true));

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Counter), Daily(), HabitCase.Measured, Boundary);

        vm.Items.Select(f => f.Caption).Should().Equal("oggi", "di fila ora", "media al giorno", "in tutto");
        vm.Items.Select(f => f.Value).Should().Equal("20", "1", "0,7", "20");
    }

    [Fact]
    public async Task Measured_time_field_is_written_in_minutes()
    {
        var time = Metric("tempo", MetricType.Seconds);
        _completions.DailySumByKey[(_id, time.Id)] = [new DailyValue(Today, 1500)];

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Counter), Daily(), HabitCase.Measured, Boundary);

        vm.Items[0].Value.Should().Be("25 min");
    }

    [Fact]
    public async Task Series_volume_is_the_first_quantity_field()
    {
        var reps = Metric("ripetizioni", MetricType.Integer, 0);
        var kg = Metric("kg", MetricType.Decimal, 1);
        for (var i = 0; i < 4; i++)
            Record(new DateTimeOffset(2026, 10, 28, 9, i, 0, TimeSpan.Zero), null, (reps, 10), (kg, 40));
        Heat((Today, 4), (Today.AddDays(-2), 3), (new DateOnly(2026, 9, 1), 2));
        _completions.DailySumByKey[(_id, reps.Id)] = [new DailyValue(Today, 40)];

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Counter), Daily(), HabitCase.Series, Boundary);

        vm.Items[0].Value.Should().Be("4");
        vm.Items[1].Value.Should().Be("40");
        vm.Items[1].Caption.Should().Be("ripetizioni oggi");
        vm.Items[2].Value.Should().Be("0,5");   // 2 giorni negli ultimi 28 = 0,5 a settimana
        vm.Items[3].Value.Should().Be("3");     // sedute = giorni con registrazioni
    }

    [Fact]
    public async Task Series_without_quantity_shows_total_sets()
    {
        Metric("note", MetricType.Text);
        Heat((Today, 2), (Today.AddDays(-1), 3));

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Counter), Daily(), HabitCase.Series, Boundary);

        vm.Items[1].Value.Should().Be("5");
        vm.Items[1].Caption.Should().Be("serie in tutto");
    }

    [Fact]
    public async Task Session_figures_in_minutes_and_hours()
    {
        var time = Metric("tempo", MetricType.Seconds);
        // oggi: 10 min dal timer + 2 min a mano; ieri 600 min a mano.
        Record(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero), 600);
        Record(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero), null, (time, 120));
        Record(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero), null, (time, 36000));
        _streaks.Runs.Add(new StreakRun(Today.AddDays(-1), Today, 2, IsCurrent: true));

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Counter), Daily(), HabitCase.Session, Boundary);

        vm.Items.Select(f => f.Value).Should().Equal("12 min", "2", "20 min", "10 h");
        vm.Items.Select(f => f.Caption).Should().Equal("oggi", "di fila", "media 30 g", "in tutto");
    }

    private static long Ms(DateTimeOffset t) => t.ToUnixTimeMilliseconds();

    [Fact]
    public async Task Abstinence_figures()
    {
        var open = new DateTimeOffset(2026, 10, 16, 10, 0, 0, TimeSpan.Zero);
        _abstinence.Periods[_id] =
        [
            new AbstinencePeriod { HabitNodeId = _id, StartedAtMs = Ms(new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero)), EndedAtMs = Ms(new(2026, 5, 11, 10, 0, 0, TimeSpan.Zero)) },
            new AbstinencePeriod { HabitNodeId = _id, StartedAtMs = Ms(new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero)), EndedAtMs = Ms(new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero)) },
            new AbstinencePeriod { HabitNodeId = _id, StartedAtMs = Ms(open) },
        ];
        _abstinence.States[_id] = new AbstinenceState(AbstinenceStatus.Abstaining, Ms(open), null);

        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Resistance), Daily(), HabitCase.Abstinence, Boundary);

        vm.Items[0].Value.Should().StartWith("12");
        vm.Items[1].Value.Should().Be("41 g");
        vm.Items[2].Value.Should().Be("1");
        vm.Items[3].Value.Should().Be("63 g");
        vm.Items.Select(f => f.Caption).Should().Equal("adesso", "il più lungo", "ricadute in 90 g", "giorni liberi in tutto");
        vm.UsualHour.Should().BeNull();
    }

    [Fact]
    public async Task Abstinence_not_started()
    {
        var vm = Vm();
        await vm.LoadAsync(Node(HabitType.Resistance), Daily(), HabitCase.Abstinence, Boundary);

        vm.Items[0].Value.Should().Be(Recordum.Presentation.Resources.Strings.Abstinence_NotStarted);
        vm.Items.Skip(1).Select(f => f.Value).Should().Equal("0 g", "0", "0 g");
    }
}
