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
        vm.Items.Select(f => f.Caption).Should().Equal("serie attuale", "serie migliore", "30 giorni", "in tutto");
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

    private Guid ScaleMetric()
    {
        var m = new MetricDefinition
        {
            Id = Guid.NewGuid(), HabitNodeId = _id, Name = "umore", Type = MetricType.Integer,
            ScaleTemplateId = _refData.Scales[0].Id, LabelsJson = "[\"male\",\"così così\",\"ok\",\"bene\",\"molto bene\"]",
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
        vm.Items.Select(f => f.Caption).Should().Equal("voci della settimana", "serie", "voci");
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
}
