using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Export;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public sealed class ExportQueryServiceTests
{
    private sealed class StubAggregator : IStreakAggregator
    {
        public ValueTask<IReadOnlyList<StreakRun>> GetHistoryAsync(Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<StreakRun>>([]);
    }

    private static ExportQueryService Build(DbFixture fx)
    {
        var bus = new RecordingEventBus();
        var completions = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, bus);
        var notes = new NoteService(fx.Factory, fx.Clock, NullLogger<NoteService>.Instance, bus);
        var metrics = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance);
        var goals = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance);
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var ramp = new Recordum.Core.Services.IntensityRampResolver();
        return new ExportQueryService(fx.Factory, fx.Clock, completions, notes, metrics, goals, configs, ramp, new StubAggregator());
    }

    [Fact]
    public async Task SingleHabit_includes_metadata_and_completion_rows()
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Run", Color = "#112233" });
            long Ms(int d) => new DateTimeOffset(2026, 5, d, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            db.Completions.Add(new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = Ms(20) });
            db.Completions.Add(new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 21, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = Ms(21) });
            await db.SaveChangesAsync();
        }

        var request = new ExportRequest(ExportSubject.SingleHabit, habitId,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var model = await Build(fx).BuildAsync(request);

        Assert.Equal(ExportSubject.SingleHabit, model.Subject);
        var habit = Assert.Single(model.Habits);
        Assert.Equal("Run", habit.Name);
        Assert.Equal("#112233", habit.ColorHex);
        Assert.Equal(2, habit.Summary.TotalCompletions);
        Assert.Equal(2, habit.Completions.Count);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Missing_single_habit_returns_empty_habits()
    {
        var fx = new DbFixture();
        var request = new ExportRequest(ExportSubject.SingleHabit, Guid.NewGuid(),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var model = await Build(fx).BuildAsync(request);
        Assert.Empty(model.Habits);
        await fx.DisposeAsync();
    }
}
