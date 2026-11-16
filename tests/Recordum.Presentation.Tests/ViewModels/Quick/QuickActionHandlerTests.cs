using System.Globalization;
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
        var sut = new ActionQuickActionHandler(cs, clock, new FakeResetTimeResolver(), new FakeConfirmationDialog());
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
        var sut = new ActionQuickActionHandler(cs, clock, resolver, new FakeConfirmationDialog());

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
        var sut = new ActionQuickActionHandler(cs, clock, resolver, new FakeConfirmationDialog());

        await sut.ExecuteAsync(habitId, null);
        var withNull = cs.LastRecord!;
        await sut.ExecuteAsync(habitId, new DateOnly(2026, 5, 20));
        var withYesterday = cs.LastRecord!;

        withNull.CompletedAt.Should().Be(clock.UtcNow.UtcDateTime);
        withNull.IsRetroactive.Should().BeFalse();
        withYesterday.CompletedAt.Should().NotBe(withNull.CompletedAt);
        withYesterday.IsRetroactive.Should().BeTrue();
    }

    // ── Task 8b: dalla Home una spunta si toglie, con conferma ──────────────────────────────────────────────────────

    private static Completion Seed(FakeCompletionService cs, Guid habit, DateTimeOffset when)
    {
        var c = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = when.UtcDateTime, CompletedAtMs = when.ToUnixTimeMilliseconds(),
        };
        cs.Store[c.Id] = c;
        return c;
    }

    private static T InCulture<T>(string name, Func<T> body)
    {
        var saved = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        try { return body(); }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = saved; }
    }

    [Fact]
    public async Task Action_not_done_records_without_asking()
    {
        var habitId = Guid.NewGuid();
        var cs = new FakeCompletionService();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        // Ieri è fatta, oggi no: la registrazione di ieri non conta per oggi.
        var yesterday = Seed(cs, habitId, clock.UtcNow.AddDays(-1));
        var confirm = new FakeConfirmationDialog();
        var sut = new ActionQuickActionHandler(cs, clock, new FakeResetTimeResolver(), confirm);

        var handled = await sut.ExecuteAsync(habitId, null);

        handled.Should().BeTrue();
        confirm.Calls.Should().Be(0);
        cs.RecordCalls.Should().Be(1);
        cs.DeleteCalls.Should().BeEmpty();
        cs.Store.Should().ContainKey(yesterday.Id);
    }

    [Fact]
    public async Task Action_already_done_asks_and_removes_the_latest_of_that_day()
    {
        var habitId = Guid.NewGuid();
        var cs = new FakeCompletionService();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        var older = Seed(cs, habitId, clock.UtcNow.AddHours(-2));
        var latest = Seed(cs, habitId, clock.UtcNow.AddHours(-1));
        var other = Seed(cs, Guid.NewGuid(), clock.UtcNow.AddMinutes(-5)); // un'altra abitudine: non si tocca
        var confirm = new FakeConfirmationDialog { Result = true };
        var sut = new ActionQuickActionHandler(cs, clock, new FakeResetTimeResolver(), confirm);

        var handled = await InCulture("it-IT", () => sut.ExecuteAsync(habitId, null).AsTask());

        handled.Should().BeTrue("la schermata non si apre");
        confirm.Calls.Should().Be(1);
        confirm.LastTitle.Should().Be("Togliere la spunta?");
        confirm.LastMessage.Should().Be("La registrazione di oggi viene cancellata.");
        cs.DeleteCalls.Should().Equal(latest.Id);
        cs.Store.Keys.Should().BeEquivalentTo([older.Id, other.Id]);
        cs.RecordCalls.Should().Be(0);
    }

    [Fact]
    public async Task Declining_writes_nothing_and_does_not_open_the_screen()
    {
        var habitId = Guid.NewGuid();
        var cs = new FakeCompletionService();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        var done = Seed(cs, habitId, clock.UtcNow.AddHours(-1));
        var confirm = new FakeConfirmationDialog { Result = false };
        var sut = new ActionQuickActionHandler(cs, clock, new FakeResetTimeResolver(), confirm);

        var handled = await sut.ExecuteAsync(habitId, null);

        handled.Should().BeTrue("anche il no resta nella Home: la schermata non si apre");
        confirm.Calls.Should().Be(1);
        cs.DeleteCalls.Should().BeEmpty();
        cs.RecordCalls.Should().Be(0);
        cs.Store.Should().ContainKey(done.Id);
    }

    [Theory]
    [InlineData("it-IT", "La registrazione di mercoledì 20 maggio viene cancellata.")]
    [InlineData("en-US", "The record of Wednesday May 20 is deleted.")]
    public async Task Removal_on_a_past_day_leaves_today_alone(string culture, string message)
    {
        var habitId = Guid.NewGuid();
        var cs = new FakeCompletionService();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        var today = Seed(cs, habitId, clock.UtcNow.AddHours(-1));
        var yesterday = Seed(cs, habitId, new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero));
        var confirm = new FakeConfirmationDialog { Result = true };
        var sut = new ActionQuickActionHandler(cs, clock, new FakeResetTimeResolver(), confirm);

        var handled = await InCulture(culture, () => sut.ExecuteAsync(habitId, new DateOnly(2026, 5, 20)).AsTask());

        handled.Should().BeTrue();
        confirm.LastMessage.Should().Be(message);
        cs.DeleteCalls.Should().Equal(yesterday.Id);
        cs.Store.Should().ContainKey(today.Id);
        cs.RecordCalls.Should().Be(0);
    }

    [Fact]
    public async Task Removal_uses_the_habit_boundary()
    {
        // Abitudine con inizio di giornata alle 05:00 (Roma). Una registrazione alle 02:00 del 21 appartiene al 20.
        var habitId = Guid.NewGuid();
        var resolver = new FakeResetTimeResolver(new DayBoundary(Rome, TimeOnly.MinValue));
        resolver.PerHabit[habitId] = new DayBoundary(Rome, new TimeOnly(5, 0));
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.FromHours(2)), Rome);
        var cs = new FakeCompletionService();
        var night = Seed(cs, habitId, new DateTimeOffset(2026, 5, 21, 2, 0, 0, TimeSpan.FromHours(2)));
        var confirm = new FakeConfirmationDialog { Result = true };
        var sut = new ActionQuickActionHandler(cs, clock, resolver, confirm);

        // Oggi (il 21 dell'abitudine) non è fatta: registra senza chiedere, quella della notte resta.
        await sut.ExecuteAsync(habitId, null);
        confirm.Calls.Should().Be(0);
        cs.RecordCalls.Should().Be(1);
        cs.Store.Should().ContainKey(night.Id);

        // Il 20 è fatto da quella delle 02:00: la conferma, e va via lei.
        await sut.ExecuteAsync(habitId, new DateOnly(2026, 5, 20));
        confirm.Calls.Should().Be(1);
        cs.DeleteCalls.Should().Equal(night.Id);
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
        var action = new ActionQuickActionHandler(new FakeCompletionService(), new FakeClock(), new FakeResetTimeResolver(), new FakeConfirmationDialog());
        var open = new OpenScreenQuickActionHandler(HabitType.Journal);
        var sut = new QuickActionHandlerSelector(new IQuickActionHandler[] { action, open });

        sut.For(HabitType.Action)!.Should().BeSameAs(action);
        sut.For(HabitType.Journal)!.Should().BeSameAs(open);
        sut.For(HabitType.Counter).Should().BeNull();
    }
}
