using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Services.Queries;

public class HabitTreeQueryServiceZeroDurationTests
{
    private static DateTimeOffset At(int day, int h) => new(2026, 10, day, h, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Zero_duration_abstinence_period_creates_no_lapse_or_hole_in_14_day_strip()
    {
        // Orologio al 10/10/2026: la striscia copre 27/09 (indice 0) .. 10/10 (indice 13).
        // Giorno 1 = 01/10 (indice 4), giorno 3 = 03/10 (indice 6).
        var clock = new FakeClock(At(10, 12));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var day1Ms = At(1, 9).ToUnixTimeMilliseconds();
        var day3Ms = At(3, 14).ToUnixTimeMilliseconds();

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId,
                NodeType = NodeType.Habit,
                HabitType = HabitType.Resistance,
                Name = "Niente fumo",
                Color = "#3366cc",
            });
            db.AbstinencePeriods.Add(new AbstinencePeriod
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                StartedAtMs = day1Ms,
                EndedAtMs = null,
            });
            // Periodo aperto duplicato chiuso a durata zero dalla riparazione dell'unione.
            db.AbstinencePeriods.Add(new AbstinencePeriod
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                StartedAtMs = day3Ms,
                EndedAtMs = day3Ms,
            });
            await db.SaveChangesAsync();
        }

        var sut = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);
        var row = (await sut.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);

        row.AbstinenceStatus.Should().Be(AbstinenceStatus.Abstaining);
        row.LapsesLast14Days[6].Should().BeFalse("un periodo a durata zero non è una ricaduta");
        row.CompletionsLast14Days[6].Should().Be(4, "il giorno 3 resta pulito senza buchi");
        for (var i = 4; i <= 13; i++)
        {
            row.LapsesLast14Days[i].Should().BeFalse();
            row.CompletionsLast14Days[i].Should().Be(4);
        }
    }
}
