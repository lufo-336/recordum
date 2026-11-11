using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Quick;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Quick;

public class ActionQuickActionHandlerTests
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    [Fact]
    public async Task ExecuteAsync_records_completion_at_clock_now()
    {
        var cs = new FakeCompletionService();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        var sut = new ActionQuickActionHandler(cs, clock, new FakeResetTimeResolver());
        var habitId = Guid.NewGuid();

        var recorded = await sut.ExecuteAsync(habitId, null);

        recorded.Should().BeTrue();
        cs.LastRecord!.HabitNodeId.Should().Be(habitId);
        cs.LastRecord.CompletedAt.Should().Be(clock.UtcNow.UtcDateTime);
        cs.LastRecord.IsRetroactive.Should().BeFalse();
        cs.LastValues.Should().BeNull();
    }

    [Fact]
    public async Task Action_on_a_past_day_records_at_noon_of_the_habit_boundary()
    {
        // App a 00:00 (reset), abitudine a 05:00; adesso = 2026-05-21 03:00 Roma → giorno app 21, giorno abitudine 20.
        var habitId = Guid.NewGuid();
        var resolver = new FakeResetTimeResolver(new DayBoundary(Rome, TimeOnly.MinValue));
        resolver.PerHabit[habitId] = new DayBoundary(Rome, new TimeOnly(5, 0));
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 1, 0, 0, TimeSpan.Zero), Rome); // 03:00 CEST
        var cs = new FakeCompletionService();
        var sut = new ActionQuickActionHandler(cs, clock, resolver);

        // "ieri" per l'abitudine = 2026-05-19 (il suo oggi è il 20).
        var recorded = await sut.ExecuteAsync(habitId, new DateOnly(2026, 5, 19));

        recorded.Should().BeTrue();
        var noon = new DateTimeOffset(2026, 5, 19, 12, 0, 0, TimeSpan.FromHours(2));
        cs.LastRecord!.CompletedAt.Should().Be(noon.UtcDateTime);
        cs.LastRecord.CompletedAtMs.Should().Be(noon.ToUnixTimeMilliseconds());
        cs.LastRecord.IsRetroactive.Should().BeTrue();
    }

    [Fact]
    public async Task Null_day_behaves_as_before()
    {
        var habitId = Guid.NewGuid();
        var resolver = new FakeResetTimeResolver(new DayBoundary(Rome, new TimeOnly(5, 0)));
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero), Rome);
        var cs = new FakeCompletionService();
        var sut = new ActionQuickActionHandler(cs, clock, resolver);

        await sut.ExecuteAsync(habitId, null);
        var withNull = cs.LastRecord!;
        await sut.ExecuteAsync(habitId, new DateOnly(2026, 5, 20));
        var withYesterday = cs.LastRecord!;

        withNull.CompletedAt.Should().Be(clock.UtcNow.UtcDateTime);
        withNull.IsRetroactive.Should().BeFalse();
        withYesterday.CompletedAt.Should().NotBe(withNull.CompletedAt);
        withYesterday.IsRetroactive.Should().BeTrue();
    }
}

public class CounterQuickActionHandlerTests
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    [Fact]
    public async Task Bare_counter_records_occurrence_without_values()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService(); // no metrics → bare tap counter
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        var sut = new CounterQuickActionHandler(cs, metrics, clock, new FakeResetTimeResolver());
        var habitId = Guid.NewGuid();

        var recorded = await sut.ExecuteAsync(habitId, null);

        recorded.Should().BeTrue();
        cs.LastRecord!.HabitNodeId.Should().Be(habitId);
        cs.LastRecord.CompletedAt.Should().Be(clock.UtcNow.UtcDateTime);
        cs.LastValues.Should().BeNull(); // occorrenza nuda: nessun CompletionValue
    }

    [Fact]
    public async Task Counter_without_fields_on_a_past_day_records_one()
    {
        var cs = new FakeCompletionService();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero), Rome);
        var resolver = new FakeResetTimeResolver(new DayBoundary(Rome, new TimeOnly(5, 0)));
        var sut = new CounterQuickActionHandler(cs, new FakeMetricDefinitionService(), clock, resolver);
        var habitId = Guid.NewGuid();

        var recorded = await sut.ExecuteAsync(habitId, new DateOnly(2026, 5, 20));

        recorded.Should().BeTrue();
        var noon = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.FromHours(2));
        cs.LastRecord!.HabitNodeId.Should().Be(habitId);
        cs.LastRecord.CompletedAt.Should().Be(noon.UtcDateTime);
        cs.LastRecord.IsRetroactive.Should().BeTrue();
        cs.LastValues.Should().BeNull();
    }

    [Fact]
    public async Task Counter_with_metrics_defers_to_screen()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "count", Type = MetricType.Integer, SortOrder = 0 };
        metrics.Store[m.Id] = m;
        var sut = new CounterQuickActionHandler(cs, metrics, new FakeClock(), new FakeResetTimeResolver());

        var recorded = await sut.ExecuteAsync(habitId, null);

        recorded.Should().BeFalse(); // con campi il +1 cieco non ha senso: apri la schermata
        cs.LastRecord.Should().BeNull();
    }

    [Fact]
    public async Task Counter_with_fields_on_a_past_day_opens_the_screen()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "count", Type = MetricType.Integer, SortOrder = 0 };
        metrics.Store[m.Id] = m;
        var sut = new CounterQuickActionHandler(cs, metrics, new FakeClock(), new FakeResetTimeResolver());

        var recorded = await sut.ExecuteAsync(habitId, new DateOnly(2025, 12, 31));

        recorded.Should().BeFalse();
        cs.LastRecord.Should().BeNull();
    }
}

public class OpenScreenQuickActionHandlerTests
{
    [Theory]
    [InlineData(HabitType.Resistance)]
    [InlineData(HabitType.Journal)]
    [InlineData(HabitType.State)]
    public async Task ExecuteAsync_returns_false_and_does_not_record(HabitType type)
    {
        var sut = new OpenScreenQuickActionHandler(type);

        var recorded = await sut.ExecuteAsync(Guid.NewGuid(), null);

        recorded.Should().BeFalse();
        sut.HabitType.Should().Be(type);
    }
}

public class QuickActionHandlerSelectorTests
{
    [Fact]
    public void For_returns_correct_handler_per_type()
    {
        var action = new ActionQuickActionHandler(new FakeCompletionService(), new FakeClock(), new FakeResetTimeResolver());
        var open = new OpenScreenQuickActionHandler(HabitType.Journal);
        var sut = new QuickActionHandlerSelector(new IQuickActionHandler[] { action, open });

        sut.For(HabitType.Action)!.Should().BeSameAs(action);
        sut.For(HabitType.Journal)!.Should().BeSameAs(open);
        sut.For(HabitType.Counter).Should().BeNull();
    }
}
