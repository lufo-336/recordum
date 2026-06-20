using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitRecordsViewModelTests
{
    private static (HabitRecordsViewModel sut, FakeCompletionService cs, Guid habit) Build(DateTimeOffset now)
    {
        var cs = new FakeCompletionService();
        var habit = Guid.NewGuid();
        var list = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeDiaryService());
        var sut = new HabitRecordsViewModel(list, new FakeClock(now));
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
}
