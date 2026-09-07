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
    private static readonly Guid HoursUnitId = Guid.Parse("00000001-0000-0000-0000-000000000009");
    private static readonly Guid LitreUnitId = Guid.Parse("00000001-0000-0000-0000-000000000006");

    private sealed class StubAggregator : IStreakAggregator
    {
        public ValueTask<IReadOnlyList<StreakRun>> GetHistoryAsync(Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<StreakRun>>([]);
    }

    private static ExportQueryService Build(DbFixture fx)
    {
        var bus = new RecordingEventBus();
        var completions = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, bus, fx.ResetTimes);
        var notes = new NoteService(fx.Factory, fx.Clock, NullLogger<NoteService>.Instance, bus);
        var metrics = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance);
        var goals = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance);
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var ramp = new Recordum.Core.Services.IntensityRampResolver();
        var abstinence = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus());
        var refData = new ReferenceDataQueryService(fx.Factory);
        // Fresh temp dataRoot per Build() call: none of these tests except the attachment-specific
        // one below ever exercise disk I/O through AttachmentService, so the directory need not exist.
        var attachments = new AttachmentService(fx.Factory, fx.Clock, fx.PluginContext,
            Path.Combine(Path.GetTempPath(), "recordum-export-tests-" + Guid.NewGuid().ToString("N")));
        return new ExportQueryService(fx.Factory, fx.Clock, completions, notes, metrics, goals, configs, ramp, new StubAggregator(), abstinence, attachments, refData);
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
    public async Task Notes_carry_their_attachments()
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var dataRoot = Path.Combine(Path.GetTempPath(), "recordum-export-attachments-" + Guid.NewGuid().ToString("N"));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "Diary" });
            db.Notes.Add(new Note { Id = noteId, HabitNodeId = habitId, BodyMd = "hi", UpdatedAtMs = 1 });
            await db.SaveChangesAsync();
        }

        var attachmentService = new AttachmentService(fx.Factory, fx.Clock, fx.PluginContext, dataRoot);
        var sourceFile = Path.Combine(Path.GetTempPath(), "recordum-export-attachments-source-" + Guid.NewGuid().ToString("N") + ".jpg");
        await File.WriteAllTextAsync(sourceFile, "fake image data");
        var attachment = await attachmentService.AddAsync(noteId, sourceFile, default);

        var bus = new RecordingEventBus();
        var completions = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, bus, fx.ResetTimes);
        var notes = new NoteService(fx.Factory, fx.Clock, NullLogger<NoteService>.Instance, bus);
        var metrics = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance);
        var goals = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance);
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var ramp = new Recordum.Core.Services.IntensityRampResolver();
        var abstinence = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus());
        var refData = new ReferenceDataQueryService(fx.Factory);
        var query = new ExportQueryService(fx.Factory, fx.Clock, completions, notes, metrics, goals, configs, ramp,
            new StubAggregator(), abstinence, attachmentService, refData);

        var request = new ExportRequest(ExportSubject.SingleHabit, habitId,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var model = await query.BuildAsync(request);

        var habit = Assert.Single(model.Habits);
        var note = Assert.Single(habit.Notes);
        var attachmentRef = Assert.Single(note.Attachments);
        Assert.Equal(attachment.Filename, attachmentRef.Filename);
        Assert.Equal("image/jpeg", attachmentRef.MimeType);
        Assert.Equal($"attachments/{noteId}/{attachment.Id:N}.jpg", attachmentRef.RelativePath);
        Assert.NotNull(attachmentRef.AbsolutePath);
        Assert.True(File.Exists(attachmentRef.AbsolutePath));

        await fx.DisposeAsync();
        File.Delete(sourceFile);
        if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, recursive: true);
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
    public async Task Export_completion_rows_convert_duration_seconds_to_the_metric_unit()
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var hoursUnitId = Guid.Parse("00000001-0000-0000-0000-000000000009"); // seeded "hours" unit
        long Ms(int d) => new DateTimeOffset(2026, 5, d, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "Sleep" });
            db.MetricDefinitions.Add(new MetricDefinition { Id = metricId, HabitNodeId = habitId, Name = "Hours slept", SortOrder = 0, UnitOfMeasureId = hoursUnitId });
            var completionId = Guid.NewGuid();
            db.Completions.Add(new Completion { Id = completionId, HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = Ms(20) });
            db.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = completionId, MetricDefinitionId = metricId, ValueSeconds = 27000 });
            await db.SaveChangesAsync();
        }

        var request = new ExportRequest(ExportSubject.SingleHabit, habitId, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var model = await Build(fx).BuildAsync(request);
        var habit = Assert.Single(model.Habits);
        var row = Assert.Single(habit.Completions);
        Assert.Equal("Hours slept", row.Metric);
        Assert.Equal(7.5m, row.Value);
        await fx.DisposeAsync();
    }

    /// <summary>Seeds one Counter habit with one metric and one completion value, and returns the
    /// exported completion row — the shared shape for the unit-labelling tests below.</summary>
    private static async Task<ExportCompletionRow> ExportOneValueRowAsync(
        MetricType type, Guid? unitId, int? valueSeconds = null, decimal? valueDecimal = null)
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "H" });
            db.MetricDefinitions.Add(new MetricDefinition { Id = metricId, HabitNodeId = habitId, Name = "M", Type = type, SortOrder = 0, UnitOfMeasureId = unitId });
            var completionId = Guid.NewGuid();
            db.Completions.Add(new Completion
            {
                Id = completionId,
                HabitNodeId = habitId,
                CompletedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc),
                CompletedAtMs = new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            });
            db.CompletionValues.Add(new CompletionValue
            {
                Id = Guid.NewGuid(),
                CompletionId = completionId,
                MetricDefinitionId = metricId,
                ValueSeconds = valueSeconds,
                ValueDecimal = valueDecimal,
            });
            await db.SaveChangesAsync();
        }

        var request = new ExportRequest(ExportSubject.SingleHabit, habitId, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var model = await Build(fx).BuildAsync(request);
        var row = Assert.Single(Assert.Single(model.Habits).Completions);
        await fx.DisposeAsync();
        return row;
    }

    [Fact]
    public async Task Export_rows_name_the_unit_a_converted_duration_is_expressed_in()
    {
        // 27000s on an "hours" metric exports as 7.5 — useless unless the file says "hours".
        var row = await ExportOneValueRowAsync(MetricType.Seconds, HoursUnitId, valueSeconds: 27000);
        Assert.Equal(7.5m, row.Value);
        Assert.Equal("hours", row.Unit);
    }

    [Fact]
    public async Task Export_rows_label_an_unconverted_duration_as_seconds()
    {
        // No unit on the metric: the value stays a raw second count, so that IS its unit. Without
        // saying so the file reads "27000" for a 7.5-hour night (W23-VF2, export branch).
        var row = await ExportOneValueRowAsync(MetricType.Seconds, unitId: null, valueSeconds: 27000);
        Assert.Equal(27000m, row.Value);
        Assert.Equal("seconds", row.Unit);
    }

    [Fact]
    public async Task Export_rows_carry_the_unit_of_a_non_duration_metric()
    {
        var row = await ExportOneValueRowAsync(MetricType.Decimal, LitreUnitId, valueDecimal: 1.5m);
        Assert.Equal(1.5m, row.Value);
        Assert.Equal("l", row.Unit);
    }

    [Fact]
    public async Task Export_rows_leave_the_unit_empty_when_the_metric_has_none()
    {
        var row = await ExportOneValueRowAsync(MetricType.Decimal, unitId: null, valueDecimal: 3m);
        Assert.Null(row.Unit);
    }

    [Fact]
    public async Task Export_goal_actual_converts_duration_seconds_to_the_metric_unit()
    {
        // The target was typed in hours; the actual must be hours too, or the summary reads
        // "27000 / 10" for a night that was 7.5 hours long.
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var hoursUnitId = Guid.Parse("00000001-0000-0000-0000-000000000009"); // seeded "hours" unit
        long Ms(int d) => new DateTimeOffset(2026, 5, d, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "Sleep" });
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricId,
                HabitNodeId = habitId,
                Name = "Hours slept",
                Type = MetricType.Seconds,
                SortOrder = 0,
                UnitOfMeasureId = hoursUnitId,
            });
            var completionId = Guid.NewGuid();
            db.Completions.Add(new Completion { Id = completionId, HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = Ms(20) });
            db.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = completionId, MetricDefinitionId = metricId, ValueSeconds = 27000 });
            db.Goals.Add(new Goal
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                Name = "Sleep enough",
                MetricDefinitionId = metricId,
                TargetValue = 10m,
                PeriodType = GoalPeriodType.Open,
                IsClosed = false,
            });
            await db.SaveChangesAsync();
        }

        var request = new ExportRequest(ExportSubject.SingleHabit, habitId, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var model = await Build(fx).BuildAsync(request);
        var habit = Assert.Single(model.Habits);

        Assert.Equal(7.5m, habit.Summary.GoalActual);
        Assert.Equal(10m, habit.Summary.GoalTarget);
        // "7.5 / 10" is meaningless without saying of what — same rule as the completion rows.
        Assert.Equal("hours", habit.Summary.GoalUnit);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Export_goal_actual_on_a_non_duration_metric_is_left_alone()
    {
        // A Decimal metric already stores the unit it displays: converting it would divide by 3600.
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var hoursUnitId = Guid.Parse("00000001-0000-0000-0000-000000000009");
        long Ms(int d) => new DateTimeOffset(2026, 5, d, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "Study" });
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricId,
                HabitNodeId = habitId,
                Name = "Ore",
                Type = MetricType.Decimal,
                SortOrder = 0,
                UnitOfMeasureId = hoursUnitId,
            });
            var completionId = Guid.NewGuid();
            db.Completions.Add(new Completion { Id = completionId, HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = Ms(20) });
            db.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = completionId, MetricDefinitionId = metricId, ValueDecimal = 7.5m });
            db.Goals.Add(new Goal
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                Name = "Study enough",
                MetricDefinitionId = metricId,
                TargetValue = 10m,
                PeriodType = GoalPeriodType.Open,
                IsClosed = false,
            });
            await db.SaveChangesAsync();
        }

        var request = new ExportRequest(ExportSubject.SingleHabit, habitId, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var model = await Build(fx).BuildAsync(request);

        var summary = Assert.Single(model.Habits).Summary;
        Assert.Equal(7.5m, summary.GoalActual);
        Assert.Equal("hours", summary.GoalUnit);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Export_goal_on_a_count_has_no_unit()
    {
        // A goal with no metric counts completions: there is no unit to name.
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Run" });
            db.Completions.Add(new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() });
            db.Goals.Add(new Goal
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                Name = "Run often",
                MetricDefinitionId = null,
                TargetValue = 20m,
                PeriodType = GoalPeriodType.Open,
                IsClosed = false,
            });
            await db.SaveChangesAsync();
        }

        var request = new ExportRequest(ExportSubject.SingleHabit, habitId, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25), new ExportLayout());
        var summary = Assert.Single((await Build(fx).BuildAsync(request)).Habits).Summary;

        Assert.Equal(1m, summary.GoalActual);
        Assert.Null(summary.GoalUnit);
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
        // Closed period May 1-4: clean days are May 1-3 (3 days) — May 4 is the relapse day, excluded.
        // Open period May 5 onward: clean through "today" (May 11) inclusive — 7 days. Total: 10.
        Assert.Equal(10, habit.Heatmap.Count);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Export_resistance_heatmap_excludes_relapse_day_same_day_period_yields_no_clean_cell()
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "Sober" });
            db.AbstinencePeriods.Add(new AbstinencePeriod
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                StartedAtMs = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                EndedAtMs = new DateTimeOffset(2026, 5, 1, 20, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            });
            await db.SaveChangesAsync();
        }

        var request = new ExportRequest(ExportSubject.SingleHabit, habitId,
            new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 5), new ExportLayout());
        var model = await Build(fx).BuildAsync(request);

        // Started and relapsed on the same calendar day: zero clean days, so zero heatmap cells —
        // an off-by-one that counts the relapse day as clean would emit exactly 1 cell here.
        var habit = Assert.Single(model.Habits);
        Assert.Empty(habit.Heatmap);
        await fx.DisposeAsync();
    }
}
