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
    private static readonly string[] ExpectedLibraryNames = ["A", "B"];

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
        var abstinence = new AbstinenceService(fx.Factory, fx.Clock);
        return new ExportQueryService(fx.Factory, fx.Clock, completions, notes, metrics, goals, configs, ramp, new StubAggregator(), abstinence);
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

    [Fact]
    public async Task Layout_toggles_exclude_sections()
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Run" });
            db.Completions.Add(new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() });
            db.Notes.Add(new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "hi", UpdatedAtMs = 1 });
            await db.SaveChangesAsync();
        }
        var layout = new ExportLayout(Completions: false, Notes: false, Heatmap: false);
        var request = new ExportRequest(ExportSubject.SingleHabit, habitId, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), layout);
        var habit = Assert.Single((await Build(fx).BuildAsync(request)).Habits);
        Assert.Empty(habit.Completions);
        Assert.Empty(habit.Notes);
        Assert.Empty(habit.Heatmap);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Library_includes_every_non_deleted_habit_node()
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "A" });
            db.HabitNodes.Add(new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "B" });
            db.HabitNodes.Add(new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Group, Name = "G" });
            await db.SaveChangesAsync();
        }
        var request = new ExportRequest(ExportSubject.Library, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var model = await Build(fx).BuildAsync(request);
        Assert.Equal(ExportSubject.Library, model.Subject);
        Assert.Equal("Recordum Library", model.Title);
        Assert.Equal(ExpectedLibraryNames, model.Habits.Select(h => h.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Heatmap_emits_cells_for_days_with_completions()
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Run" });
            db.Completions.Add(new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 25, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = new DateTimeOffset(2026, 5, 25, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() });
            await db.SaveChangesAsync();
        }
        var request = new ExportRequest(ExportSubject.SingleHabit, habitId, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var habit = Assert.Single((await Build(fx).BuildAsync(request)).Habits);
        Assert.Contains(habit.Heatmap, c => c.Step >= 1);
        Assert.All(habit.Heatmap, c => Assert.InRange(c.Week, 0, 52));
        Assert.All(habit.Heatmap, c => Assert.InRange(c.Weekday, 0, 6));
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Export_completion_rows_carry_metric_name()
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        long Ms(int d) => new DateTimeOffset(2026, 5, d, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "Read" });
            db.MetricDefinitions.Add(new MetricDefinition { Id = metricId, HabitNodeId = habitId, Name = "Pagine", SortOrder = 0 });
            var completionId = Guid.NewGuid();
            db.Completions.Add(new Completion { Id = completionId, HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = Ms(20) });
            db.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = completionId, MetricDefinitionId = metricId, ValueInteger = 22 });
            await db.SaveChangesAsync();
        }

        var request = new ExportRequest(ExportSubject.SingleHabit, habitId, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var model = await Build(fx).BuildAsync(request);
        var habit = Assert.Single(model.Habits);
        var row = Assert.Single(habit.Completions);
        Assert.Equal("Pagine", row.Metric);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Export_resistance_habit_lists_periods_not_completions()
    {
        var t0 = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var fx = new DbFixture(new FakeClock(t0.AddDays(10)));
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "Sober" });
            db.AbstinencePeriods.Add(new AbstinencePeriod
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                StartedAtMs = t0.ToUnixTimeMilliseconds(),
                EndedAtMs = t0.AddDays(3).ToUnixTimeMilliseconds(),
            });
            db.AbstinencePeriods.Add(new AbstinencePeriod
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                StartedAtMs = t0.AddDays(4).ToUnixTimeMilliseconds(),
                EndedAtMs = null,
            });
            await db.SaveChangesAsync();
        }

        var from = DateOnly.FromDateTime(t0.AddDays(-1).UtcDateTime);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var request = new ExportRequest(ExportSubject.SingleHabit, habitId, from, today, new ExportLayout());
        var model = await Build(fx).BuildAsync(request);

        var habit = Assert.Single(model.Habits);
        Assert.Empty(habit.Completions);
        Assert.Equal(2, habit.Periods.Count);
        Assert.Contains(habit.Periods, p => p.IsOpen && p.EndedAt is null);
        Assert.Equal(2, habit.Summary.TotalCompletions);
        Assert.NotEmpty(habit.Heatmap);
        await fx.DisposeAsync();
    }
}
