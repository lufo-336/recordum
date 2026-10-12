using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Services;

/// <summary>F2a-10: fermare un timer non deve perdere la sessione se la registrazione fallisce.</summary>
public class ActiveTimerServiceStopTests
{
    [Fact]
    public async Task A_stop_whose_completion_is_rejected_keeps_the_timer()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using (var seed = await fx.Factory.CreateDbContextAsync())
        {
            // Uno stato rifiuta una registrazione senza valori: è il caso del cronometro di cella
            // di un'abitudine stato fermato dalla barra dei timer.
            seed.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "s" });
            await seed.SaveChangesAsync();
        }
        var completions = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, completions, new RecordingEventBus());
        await sut.StartAsync(habitId, TimerMode.Stopwatch);

        var stop = async () => await sut.StopAsync(habitId);

        await stop.Should().ThrowAsync<InvalidCompletionForHabitTypeException>();
        (await sut.GetActiveAsync(habitId)).Should().NotBeNull("la sessione resta nel timer, non va persa");
        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.Completions.CountAsync(c => c.HabitNodeId == habitId)).Should().Be(0);
    }
}
