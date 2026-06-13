using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Quick;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Quick;

public class ActionQuickActionHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_records_completion_at_clock_now()
    {
        var cs = new FakeCompletionService();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        var sut = new ActionQuickActionHandler(cs, clock);
        var habitId = Guid.NewGuid();

        var recorded = await sut.ExecuteAsync(habitId);

        recorded.Should().BeTrue();
        cs.LastRecord!.HabitNodeId.Should().Be(habitId);
        cs.LastRecord.CompletedAt.Should().Be(clock.UtcNow.UtcDateTime);
        cs.LastValues.Should().BeNull();
    }
}

public class CounterQuickActionHandlerTests
{
    [Fact]
    public async Task Bare_counter_records_occurrence_without_values()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService(); // no metrics → bare tap counter
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        var sut = new CounterQuickActionHandler(cs, metrics, clock);
        var habitId = Guid.NewGuid();

        var recorded = await sut.ExecuteAsync(habitId);

        recorded.Should().BeTrue();
        cs.LastRecord!.HabitNodeId.Should().Be(habitId);
        cs.LastRecord.CompletedAt.Should().Be(clock.UtcNow.UtcDateTime);
        cs.LastValues.Should().BeNull(); // occorrenza nuda: nessun CompletionValue
    }

    [Fact]
    public async Task Counter_with_metrics_defers_to_screen()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "count", Type = MetricType.Integer, SortOrder = 0 };
        metrics.Store[m.Id] = m;
        var sut = new CounterQuickActionHandler(cs, metrics, new FakeClock());

        var recorded = await sut.ExecuteAsync(habitId);

        recorded.Should().BeFalse(); // con campi il +1 cieco non ha senso: apri la schermata
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

        var recorded = await sut.ExecuteAsync(Guid.NewGuid());

        recorded.Should().BeFalse();
        sut.HabitType.Should().Be(type);
    }
}

public class QuickActionHandlerSelectorTests
{
    [Fact]
    public void For_returns_correct_handler_per_type()
    {
        var action = new ActionQuickActionHandler(new FakeCompletionService(), new FakeClock());
        var open = new OpenScreenQuickActionHandler(HabitType.Journal);
        var sut = new QuickActionHandlerSelector(new IQuickActionHandler[] { action, open });

        sut.For(HabitType.Action)!.Should().BeSameAs(action);
        sut.For(HabitType.Journal)!.Should().BeSameAs(open);
        sut.For(HabitType.Counter).Should().BeNull();
    }
}
