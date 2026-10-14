using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public class HabitRecordsViewModelTests
{
    private static (HabitRecordsViewModel sut, FakeCompletionService cs, Guid habit) Build(DateTimeOffset now)
    {
        var cs = new FakeCompletionService();
        var clock = new FakeClock(now);
        var habit = Guid.NewGuid();
        var list = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeDiaryService());
        var periods = new AbstinencePeriodListViewModel(new FakeAbstinenceService(), new FakeConfirmationDialog(), clock);
        var sut = new HabitRecordsViewModel(list, periods, clock);
        sut.SetContext(habit, isJournal: false);
        return (sut, cs, habit);
    }

    private static void Seed(FakeCompletionService cs, Guid habit, params DateTime[] daysUtc)
    {
        foreach (var ts in daysUtc)
            cs.Store[Guid.NewGuid()] = new Completion
            {
                Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = ts,
                CompletedAtMs = new DateTimeOffset(ts, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            };
    }

    [Fact]
    public async Task ShowDayAsync_in_day_mode_lists_only_that_day()
    {
        var (sut, cs, habit) = Build(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero));
        Seed(cs, habit, new DateTime(2026, 6, 17, 8, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 6, 16, 8, 0, 0, DateTimeKind.Utc));

        await sut.ShowDayAsync(new DateOnly(2026, 6, 17));

        sut.Mode.Should().Be(EntriesRecordsMode.Giorno);
        sut.Records.Rows.Should().ContainSingle();
        sut.Records.Rows[0].CompletedAt.Day.Should().Be(17);
    }

    [Fact]
    public void Switching_to_periodo_loads_the_default_month_range()
    {
        var (sut, cs, habit) = Build(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero));
        Seed(cs, habit, new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 6, 28, 8, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 5, 30, 8, 0, 0, DateTimeKind.Utc)); // fuori mese

        sut.Mode = EntriesRecordsMode.Periodo; // default SelectedPeriod = Mese; hook ricarica inline (fake sincrono)

        sut.SelectedPeriod.Should().Be(EntriesPeriod.Mese);
        sut.Records.Rows.Should().HaveCount(2); // solo i due di giugno
    }

    [Fact]
    public void Custom_period_uses_custom_bounds()
    {
        var (sut, cs, habit) = Build(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero));
        Seed(cs, habit, new DateTime(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 6, 25, 8, 0, 0, DateTimeKind.Utc));

        sut.Mode = EntriesRecordsMode.Periodo;
        sut.CustomFrom = new DateOnly(2026, 6, 1);
        sut.CustomTo = new DateOnly(2026, 6, 10);
        sut.SelectedPeriod = EntriesPeriod.Personalizzato; // ricarica con gli estremi custom

        sut.Records.Rows.Should().ContainSingle();
        sut.Records.Rows[0].CompletedAt.Day.Should().Be(5);
    }

    [Fact]
    public async Task ReloadAsync_reruns_current_mode_and_day_returns_after_periodo()
    {
        var (sut, cs, habit) = Build(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero));
        Seed(cs, habit, new DateTime(2026, 6, 17, 8, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc));
        await sut.ShowDayAsync(new DateOnly(2026, 6, 17)); // memorizza il giorno

        sut.Mode = EntriesRecordsMode.Periodo;  // mostra il mese (2 righe)
        sut.Records.Rows.Should().HaveCount(2);

        sut.Mode = EntriesRecordsMode.Giorno;   // torna al giorno memorizzato (1 riga)
        sut.Records.Rows.Should().ContainSingle();
        sut.Records.Rows[0].CompletedAt.Day.Should().Be(17);
    }

    [Fact]
    public async Task Resistance_context_routes_records_to_period_list()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero));
        var habit = Guid.NewGuid();
        var abstinence = new FakeAbstinenceService();
        abstinence.Periods[habit] = new List<AbstinencePeriod>
        {
            new() { Id = Guid.NewGuid(), HabitNodeId = habit,
                    StartedAtMs = new DateTimeOffset(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds(),
                    EndedAtMs = null },
        };
        var periods = new AbstinencePeriodListViewModel(abstinence, new FakeConfirmationDialog(), clock);
        var cs = new FakeCompletionService();
        var completions = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeDiaryService());
        var sut = new HabitRecordsViewModel(completions, periods, clock);

        sut.SetContext(habit, isJournal: false, isResistance: true);
        await sut.ApplyPeriodAsync();

        sut.CurrentList.Should().BeSameAs(periods);
        periods.Rows.Should().ContainSingle();
    }

    [Fact]
    public async Task Disposing_while_a_reload_is_in_flight_does_not_pull_the_token_source_from_under_it()
    {
        var (sut, cs, _) = Build(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero));
        var gate = new TaskCompletionSource();
        cs.ListGate = gate.Task;

        sut.Mode = EntriesRecordsMode.Periodo; // starts the fire-and-forget reload, parked on the gate
        sut.Dispose();
        gate.SetResult();
        await Task.Delay(50); // the reload touches its token now

        cs.ListTokenFault.Should().BeNull("the in-flight reload is cancelled, its CancellationTokenSource is not disposed under it");
    }
}
