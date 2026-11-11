using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class HabitTreeQueryServiceTests
{
    private static readonly int[] ExpectedSevenDayBuckets = [1, 0, 0, 0, 0, 0, 2];

    private static async Task<Guid> AddHabitAsync(DbFixture fx, string name, TimeOnly? resetTime = null)
    {
        var id = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode
        {
            Id = id,
            NodeType = NodeType.Habit,
            HabitType = HabitType.Action,
            Name = name,
            ResetTime = resetTime,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task AddCompletionAsync(DbFixture fx, Guid habitId, DateTimeOffset at)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.Completions.Add(new Completion
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitId,
            CompletedAt = at.UtcDateTime,
            CompletedAtMs = at.ToUnixTimeMilliseconds(),
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Rows_carry_the_habit_day_and_the_app_day()
    {
        // La riga dice "reset 5:00" quando il giorno dell'abitudine e' indietro: servono i due confini.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 2, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var id = await AddHabitAsync(fx, "late", new TimeOnly(5, 0));

        var row = (await new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes).GetHomeTreeAsync()).Single(r => r.HabitNodeId == id);

        row.HabitDay!.ResetTime.Should().Be(new TimeOnly(5, 0));
        row.AppDay!.ResetTime.Should().NotBe(new TimeOnly(5, 0));
    }

    [Fact]
    public async Task Two_habits_with_different_resets_get_different_strips_from_the_same_completion_time()
    {
        // Stessa registrazione (22:30Z del 18 = 19 alle 00:30 locali), due abitudini: una senza
        // reset (per lei e' oggi, il 19) e una con reset 04:00 (per lei e' ancora ieri, il 18).
        // Se la striscia le trattasse uguale, il confine per-abitudine sarebbe finto.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 6, 0, 0, TimeSpan.Zero), TestTimeZones.Fixed(2));
        await using var fx = new DbFixture(clock);
        var plain = await AddHabitAsync(fx, "plain");
        var lateReset = await AddHabitAsync(fx, "late", new TimeOnly(4, 0));
        var at = new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero);
        await AddCompletionAsync(fx, plain, at);
        await AddCompletionAsync(fx, lateReset, at);

        var rows = await new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes).GetHomeTreeAsync();

        var plainStrip = rows.Single(r => r.HabitNodeId == plain).CompletionsLast14Days;
        var lateStrip = rows.Single(r => r.HabitNodeId == lateReset).CompletionsLast14Days;
        plainStrip[13].Should().Be(1);   // oggi
        lateStrip[13].Should().Be(0);    // per lei oggi e' ancora vuoto
        lateStrip[12].Should().Be(1);    // ieri
    }

    [Fact]
    public async Task The_strip_does_not_shift_across_a_daylight_saving_change()
    {
        // Finestra 19 marzo - 1 aprile 2026, che contiene il 29 marzo: giorno di 23 ore. Una
        // registrazione per ogni giorno deve dare 14 quadretti pieni, non 13 pieni e uno doppio.
        //
        // Le registrazioni sono alle 00:30 LOCALI, non a mezzogiorno: con la divisione per
        // 86.400.000 le fette scivolano di un ora dopo il cambio, e mezzogiorno ha undici ore di
        // margine dentro la fetta, quindi non se ne accorge. A ridosso della mezzanotte si', ed e
        // li che il difetto morde davvero — chi registra a tarda notte.
        var tz = TestTimeZones.RomeLike;
        var nowLocal = new DateTime(2026, 4, 1, 10, 0, 0);
        var clock = new FakeClock(new DateTimeOffset(nowLocal, tz.GetUtcOffset(nowLocal)), tz);
        await using var fx = new DbFixture(clock);
        var habitId = await AddHabitAsync(fx, "daily");
        for (var d = new DateOnly(2026, 3, 19); d <= new DateOnly(2026, 4, 1); d = d.AddDays(1))
        {
            var justAfterMidnight = d.ToDateTime(new TimeOnly(0, 30));
            await AddCompletionAsync(fx, habitId, new DateTimeOffset(justAfterMidnight, tz.GetUtcOffset(justAfterMidnight)));
        }

        var rows = await new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes).GetHomeTreeAsync();

        rows.Single().CompletionsLast14Days.Should().AllSatisfy(v => v.Should().Be(1));
    }

    [Fact]
    public async Task Empty_db_returns_empty_list()
    {
        await using var fx = new DbFixture();
        var q = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);
        Assert.Empty(await q.GetHomeTreeAsync());
    }

    [Fact]
    public async Task Soft_deleted_nodes_are_excluded()
    {
        await using var fx = new DbFixture();
        var alive = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "alive" };
        var dead = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "dead", DeletedAt = DateTime.UtcNow };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.AddRange(alive, dead);
            await db.SaveChangesAsync();
        }
        var q = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);
        var rows = await q.GetHomeTreeAsync();
        Assert.Single(rows);
        Assert.Equal(alive.Id, rows[0].HabitNodeId);
    }

    [Fact]
    public async Task IsCompletedToday_true_when_completion_falls_in_today_window()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };
        var completion = new Completion { Id = Guid.NewGuid(), HabitNodeId = habit.Id, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = clock.UtcNowMs };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(habit);
            db.Completions.Add(completion);
            await db.SaveChangesAsync();
        }
        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var rows = await q.GetHomeTreeAsync();
        Assert.True(rows.Single().IsCompletedToday);
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_PinScope_from_db()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId,
                Name = "Pinned",
                NodeType = NodeType.Habit,
                HabitType = HabitType.Action,
                PinScope = PinScope.Global,
                Color = "#ff0000",
            });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);
        var rows = await q.GetHomeTreeAsync();
        rows.Single(r => r.HabitNodeId == habitId).PinScope.Should().Be(PinScope.Global);
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_ChildCount_for_groups()
    {
        await using var fx = new DbFixture();
        var groupId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = groupId, Name = "G", NodeType = NodeType.Group });
            db.HabitNodes.Add(new HabitNode { Id = Guid.NewGuid(), Name = "Child1", NodeType = NodeType.Habit, HabitType = HabitType.Action, ParentId = groupId });
            db.HabitNodes.Add(new HabitNode { Id = Guid.NewGuid(), Name = "Child2", NodeType = NodeType.Habit, HabitType = HabitType.Action, ParentId = groupId });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);
        var rows = await q.GetHomeTreeAsync();
        rows.Single(r => r.HabitNodeId == groupId).ChildCount.Should().Be(2);
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_IsArchived_flag()
    {
        await using var fx = new DbFixture();
        var id = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = id, Name = "A", NodeType = NodeType.Habit, HabitType = HabitType.Action, IsArchived = true });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);
        var rows = await q.GetHomeTreeAsync();
        rows.Single(r => r.HabitNodeId == id).IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_7day_completion_counts_oldest_to_today()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var today = clock.UtcNow.UtcDateTime.Date;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "H", NodeType = NodeType.Habit, HabitType = HabitType.Action });
            db.Completions.AddRange(
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = today.AddDays(-6), CompletedAtMs = new DateTimeOffset(today.AddDays(-6), TimeSpan.Zero).ToUnixTimeMilliseconds() },
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = today, CompletedAtMs = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds() },
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = today, CompletedAtMs = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds() + 1 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var rows = await q.GetHomeTreeAsync();
        rows.Single(r => r.HabitNodeId == habitId).CompletionsLast7Days
            .Should().BeEquivalentTo(ExpectedSevenDayBuckets);
    }

    [Fact]
    public async Task GetTrashTreeAsync_returns_only_soft_deleted_rows_flat()
    {
        await using var fx = new DbFixture();
        var live = Guid.NewGuid();
        var trashed = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = live, Name = "Live", NodeType = NodeType.Habit, HabitType = HabitType.Action });
            db.HabitNodes.Add(new HabitNode { Id = trashed, Name = "Trashed", NodeType = NodeType.Habit, HabitType = HabitType.Action, DeletedAt = fx.Clock.UtcNow.UtcDateTime });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);
        var rows = await q.GetTrashTreeAsync();
        rows.Should().ContainSingle().Which.HabitNodeId.Should().Be(trashed);
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_TagIds_for_each_node()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        var tagA = Guid.NewGuid();
        var tagB = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "x", NodeType = NodeType.Habit, HabitType = HabitType.Action });
            db.Tags.AddRange(
                new Tag { Id = tagA, Name = "a" },
                new Tag { Id = tagB, Name = "b" });
            db.HabitNodeTags.AddRange(
                new HabitNodeTag { HabitNodeId = habitId, TagId = tagA },
                new HabitNodeTag { HabitNodeId = habitId, TagId = tagB });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.TagIds.Should().BeEquivalentTo(new[] { tagA, tagB });
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_empty_TagIds_when_no_tags()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "x", NodeType = NodeType.Habit, HabitType = HabitType.Action });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.TagIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_TodayCounterTotal_summed_from_primary_metric()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "c", NodeType = NodeType.Habit, HabitType = HabitType.Counter });
            var unitId = await db.UnitsOfMeasure.Where(u => u.Code == "reps").Select(u => u.Id).SingleAsync();
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricId,
                HabitNodeId = habitId,
                Name = "Reps",
                Type = MetricType.Integer,
                UnitOfMeasureId = unitId,
                SortOrder = 0,
            });
            var c1 = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs };
            var c2 = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs + 1 };
            db.Completions.AddRange(c1, c2);
            db.CompletionValues.AddRange(
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = c1.Id, MetricDefinitionId = metricId, ValueInteger = 10 },
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = c2.Id, MetricDefinitionId = metricId, ValueInteger = 12 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.TodayCounterTotal.Should().Be(22);
        row.TodayCounterUnitCode.Should().Be("reps");
    }

    [Fact]
    public async Task GetHomeTreeAsync_converts_duration_counter_total_to_the_metrics_unit()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "t", NodeType = NodeType.Habit, HabitType = HabitType.Counter });
            var unitId = await db.UnitsOfMeasure.Where(u => u.Code == "minutes").Select(u => u.Id).SingleAsync();
            // A duration (Seconds) metric measured in "minutes": stored values are SECONDS.
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricId,
                HabitNodeId = habitId,
                Name = "Tempo",
                Type = MetricType.Seconds,
                UnitOfMeasureId = unitId,
                SortOrder = 0,
            });
            var c1 = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs };
            var c2 = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs + 1 };
            db.Completions.AddRange(c1, c2);
            db.CompletionValues.AddRange(
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = c1.Id, MetricDefinitionId = metricId, ValueSeconds = 60 },
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = c2.Id, MetricDefinitionId = metricId, ValueSeconds = 30 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        // 90 stored seconds shown in minutes = 1.5, NOT "90 minutes" (raw seconds + label).
        row.TodayCounterTotal.Should().Be(1.5m);
        row.TodayCounterUnitCode.Should().Be("minutes");
    }

    [Fact]
    public async Task GetHomeTreeAsync_counts_occurrences_for_a_counter_with_no_metrics()
    {
        // A bare counter (tap counter) has no metric to sum, so the sum-based total collapsed to 0
        // while the habit screen, the calendar badge and the day note all showed the occurrence
        // count — the Home row was the only place reading "0" for a day with 3 taps (W23-VF1).
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "tap", NodeType = NodeType.Habit, HabitType = HabitType.Counter });
            for (var i = 0; i < 3; i++)
            {
                db.Completions.Add(new Completion
                {
                    Id = Guid.NewGuid(), HabitNodeId = habitId,
                    CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs + i,
                });
            }
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.TodayCounterTotal.Should().Be(3m);
    }

    [Fact]
    public async Task GetHomeTreeAsync_keeps_a_zero_sum_for_a_counter_that_does_have_a_metric()
    {
        // The occurrence fallback must NOT leak into metric-bearing counters: a day with two
        // registrations that both recorded 0 is a real 0, not "2".
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "m", NodeType = NodeType.Habit, HabitType = HabitType.Counter });
            db.MetricDefinitions.Add(new MetricDefinition
            { Id = metricId, HabitNodeId = habitId, Name = "Pagine", Type = MetricType.Integer, SortOrder = 0 });
            for (var i = 0; i < 2; i++)
            {
                var c = new Completion
                {
                    Id = Guid.NewGuid(), HabitNodeId = habitId,
                    CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs + i,
                };
                db.Completions.Add(c);
                db.CompletionValues.Add(new CompletionValue
                { Id = Guid.NewGuid(), CompletionId = c.Id, MetricDefinitionId = metricId, ValueInteger = 0 });
            }
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.TodayCounterTotal.Should().Be(0m);
    }

    [Fact]
    public async Task GetHomeTreeAsync_flags_a_duration_counter_whose_total_stayed_in_raw_seconds()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "t", NodeType = NodeType.Habit, HabitType = HabitType.Counter });
            // A duration (Seconds) metric with NO unit — the default for a "+ Tempo" field from the
            // builder. There is nothing to convert into, so the total stays raw seconds; the row must
            // say so, otherwise the Home row prints "720" where the records list reads "12:00".
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricId,
                HabitNodeId = habitId,
                Name = "Tempo",
                Type = MetricType.Seconds,
                SortOrder = 0,
            });
            var c = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs };
            db.Completions.Add(c);
            db.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = c.Id, MetricDefinitionId = metricId, ValueSeconds = 720 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.TodayCounterTotal.Should().Be(720m);
        row.TodayCounterUnitCode.Should().BeNull();
        row.TodayCounterIsDuration.Should().BeTrue();
    }

    [Fact]
    public async Task GetHomeTreeAsync_does_not_flag_a_plain_counter_as_a_duration()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "t", NodeType = NodeType.Habit, HabitType = HabitType.Counter });
            db.MetricDefinitions.Add(new MetricDefinition
            { Id = metricId, HabitNodeId = habitId, Name = "Pagine", Type = MetricType.Integer, SortOrder = 0 });
            var c = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs };
            db.Completions.Add(c);
            db.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = c.Id, MetricDefinitionId = metricId, ValueInteger = 720 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.TodayCounterTotal.Should().Be(720m); // 720 pages must never read as "12:00"
        row.TodayCounterIsDuration.Should().BeFalse();
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_ValuesLast14Days_daily_sums_for_counter()
    {
        // 2.1-bis: counter strip = value-height mini-bars → the row needs the daily SUM of the
        // primary metric over the 14-day window, not just the completion count.
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var threeDaysAgoMs = todayMs - 3 * 86_400_000L;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "c", NodeType = NodeType.Habit, HabitType = HabitType.Counter });
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricId, HabitNodeId = habitId, Name = "Reps", Type = MetricType.Integer, SortOrder = 0,
            });
            var c1 = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs };
            var c2 = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs + 1 };
            var c3 = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime.AddDays(-3), CompletedAtMs = threeDaysAgoMs };
            db.Completions.AddRange(c1, c2, c3);
            db.CompletionValues.AddRange(
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = c1.Id, MetricDefinitionId = metricId, ValueInteger = 10 },
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = c2.Id, MetricDefinitionId = metricId, ValueInteger = 12 },
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = c3.Id, MetricDefinitionId = metricId, ValueInteger = 5 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.ValuesLast14Days.Length.Should().Be(14);
        row.ValuesLast14Days[13].Should().Be(22m);
        row.ValuesLast14Days[10].Should().Be(5m);
        row.ValuesLast14Days[9].Should().BeNull();
        row.ValuesLast14Days[0].Should().BeNull();
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_ValuesLast14Days_daily_last_value_for_state()
    {
        // State strip = ramp cells tinted by the scale value → per day the LAST value wins
        // (a state is a point-in-time reading, same last-wins rule as TodayStateValue).
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var yesterdayMs = todayMs - 86_400_000L;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "s", NodeType = NodeType.Habit, HabitType = HabitType.State });
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricId, HabitNodeId = habitId, Name = "Mood", Type = MetricType.Integer, SortOrder = 0,
            });
            var early = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime.AddDays(-1), CompletedAtMs = yesterdayMs };
            var late = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime.AddDays(-1), CompletedAtMs = yesterdayMs + 100 };
            db.Completions.AddRange(early, late);
            db.CompletionValues.AddRange(
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = early.Id, MetricDefinitionId = metricId, ValueInteger = 1 },
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = late.Id, MetricDefinitionId = metricId, ValueInteger = 3 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.ValuesLast14Days[12].Should().Be(3m);
        row.ValuesLast14Days[13].Should().BeNull();
    }

    [Fact]
    public async Task GetHomeTreeAsync_leaves_ValuesLast14Days_empty_for_action()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "a", NodeType = NodeType.Habit, HabitType = HabitType.Action });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.ValuesLast14Days.IsDefaultOrEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_TodayStateValue_and_label_from_latest_completion()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "s", NodeType = NodeType.Habit, HabitType = HabitType.State });
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricId,
                HabitNodeId = habitId,
                Name = "Mood",
                Type = MetricType.Integer,
                LabelsJson = "[\"Male\",\"Così\",\"Bene\"]",
                SortOrder = 0,
            });
            var earlier = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs };
            var latest = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs + 100 };
            db.Completions.AddRange(earlier, latest);
            db.CompletionValues.AddRange(
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = earlier.Id, MetricDefinitionId = metricId, ValueInteger = 1 },
                new CompletionValue { Id = Guid.NewGuid(), CompletionId = latest.Id, MetricDefinitionId = metricId, ValueInteger = 3 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.TodayStateValue.Should().Be(3);
        row.TodayStateLabel.Should().Be("Bene");
    }

    [Fact]
    public async Task GetHomeTreeAsync_resolves_TodayStateLabel_from_ScaleTemplate_when_metric_has_no_override()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var scaleId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "s", NodeType = NodeType.Habit, HabitType = HabitType.State });
            db.ScaleTemplates.Add(new ScaleTemplate
            {
                Id = scaleId,
                Code = "test-mood",
                Name = "Test Mood",
                MinValue = 1,
                MaxValue = 3,
                LabelsJson = "[\"Male\",\"Così\",\"Bene\"]",
            });
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricId,
                HabitNodeId = habitId,
                Name = "Mood",
                Type = MetricType.Integer,
                ScaleTemplateId = scaleId,
                LabelsJson = null, // no metric-level override → must fall back to the template
                SortOrder = 0,
            });
            var completion = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs };
            db.Completions.Add(completion);
            db.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = completion.Id, MetricDefinitionId = metricId, ValueInteger = 3 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.TodayStateValue.Should().Be(3);
        row.TodayStateLabel.Should().Be("Bene");
    }

    [Fact]
    public async Task GetHomeTreeAsync_metric_LabelsJson_wins_over_ScaleTemplate()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var scaleId = Guid.NewGuid();
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "s", NodeType = NodeType.Habit, HabitType = HabitType.State });
            db.ScaleTemplates.Add(new ScaleTemplate
            {
                Id = scaleId,
                Code = "test-generic",
                Name = "Test Generic",
                MinValue = 1,
                MaxValue = 3,
                LabelsJson = "[\"TmplA\",\"TmplB\",\"TmplC\"]",
            });
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricId,
                HabitNodeId = habitId,
                Name = "Mood",
                Type = MetricType.Integer,
                ScaleTemplateId = scaleId,
                LabelsJson = "[\"Male\",\"Così\",\"Bene\"]", // override must win
                SortOrder = 0,
            });
            var completion = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = todayMs };
            db.Completions.Add(completion);
            db.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = completion.Id, MetricDefinitionId = metricId, ValueInteger = 3 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.TodayStateValue.Should().Be(3);
        row.TodayStateLabel.Should().Be("Bene");
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_Abstaining_for_open_period()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var startedAtMs = clock.UtcNowMs - 100_000;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "r", NodeType = NodeType.Habit, HabitType = HabitType.Resistance });
            db.AbstinencePeriods.Add(new AbstinencePeriod { Id = Guid.NewGuid(), HabitNodeId = habitId, StartedAtMs = startedAtMs });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.AbstinenceStatus.Should().Be(AbstinenceStatus.Abstaining);
        row.AbstinenceOpenSinceMs.Should().Be(startedAtMs);
    }

    [Fact]
    public async Task GetHomeTreeAsync_resistance_strip_is_abstaining_band_from_periods()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        long Ms(int daysAgo) => new DateTimeOffset(
            clock.UtcNow.UtcDateTime.Date.AddDays(-daysAgo), TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "r", NodeType = NodeType.Habit, HabitType = HabitType.Resistance });
            // open period started 3 days ago → covers today-3..today = indices 10,11,12,13 (today = index 13)
            db.AbstinencePeriods.Add(new AbstinencePeriod { Id = Guid.NewGuid(), HabitNodeId = habitId, StartedAtMs = Ms(3) });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.CompletionsLast14Days.Length.Should().Be(14);
        // the abstaining days (indices 10..13) are filled; earlier days are empty
        row.CompletionsLast14Days[13].Should().BeGreaterThan(0);
        row.CompletionsLast14Days[11].Should().BeGreaterThan(0);
        row.CompletionsLast14Days[10].Should().BeGreaterThan(0);
        row.CompletionsLast14Days[9].Should().Be(0);
        row.CompletionsLast14Days[0].Should().Be(0);
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_Relapsed_for_only_closed_period()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var startedAtMs = clock.UtcNowMs - 200_000;
        var endedAtMs = clock.UtcNowMs - 100_000;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "r2", NodeType = NodeType.Habit, HabitType = HabitType.Resistance });
            db.AbstinencePeriods.Add(new AbstinencePeriod { Id = Guid.NewGuid(), HabitNodeId = habitId, StartedAtMs = startedAtMs, EndedAtMs = endedAtMs });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.AbstinenceStatus.Should().Be(AbstinenceStatus.Relapsed);
        row.AbstinenceOpenSinceMs.Should().BeNull();
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_14day_completion_counts_with_day10_at_index3()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var today = clock.UtcNow.UtcDateTime.Date;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "H14", NodeType = NodeType.Habit, HabitType = HabitType.Action });
            db.Completions.Add(new Completion
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                CompletedAt = today.AddDays(-10),
                CompletedAtMs = new DateTimeOffset(today.AddDays(-10), TimeSpan.Zero).ToUnixTimeMilliseconds(),
            });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.CompletionsLast14Days.Length.Should().Be(14);
        row.CompletionsLast14Days[3].Should().Be(1);
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_JournalEntriesThisWeek_for_journal_habit()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var today = clock.UtcNow.UtcDateTime.Date;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "j", NodeType = NodeType.Habit, HabitType = HabitType.Journal });
            db.Completions.AddRange(
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = today, CompletedAtMs = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds() },
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = today.AddDays(-3), CompletedAtMs = new DateTimeOffset(today.AddDays(-3), TimeSpan.Zero).ToUnixTimeMilliseconds() },
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = today.AddDays(-10), CompletedAtMs = new DateTimeOffset(today.AddDays(-10), TimeSpan.Zero).ToUnixTimeMilliseconds() });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);
        row.JournalEntriesThisWeek.Should().Be(2);
    }

    [Fact]
    public async Task GetHomeTreeAsync_projects_unit_symbol_name_and_is_official_for_counter()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var hours = Recordum.Data.SeedData.UnitOfMeasureSeed.All.Single(u => u.Code == "hours");
        var hoursId = hours.Id;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "Sonno", NodeType = NodeType.Habit, HabitType = HabitType.Counter });
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                Name = "Durata",
                Type = MetricType.Seconds,
                UnitOfMeasureId = hoursId,
                SortOrder = 0,
            });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock, fx.ResetTimes);
        var row = (await q.GetHomeTreeAsync()).Single(r => r.HabitNodeId == habitId);

        row.TodayCounterUnitCode.Should().Be("hours");
        row.TodayCounterUnitSymbol.Should().Be(hours.Symbol).And.NotBeNullOrEmpty();
        row.TodayCounterUnitName.Should().Be(hours.Name);
        row.TodayCounterUnitIsOfficial.Should().BeTrue();
    }
}
