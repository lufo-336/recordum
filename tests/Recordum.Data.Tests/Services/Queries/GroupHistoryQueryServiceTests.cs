using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services.Evaluators;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Services.Queries;

public class GroupHistoryQueryServiceTests
{
    private static DateOnly D(int day) => new(2026, 10, day);
    private static DateTimeOffset At(int day, int h, int m = 0) => new(2026, 10, day, h, m, 0, TimeSpan.Zero);

    private sealed class Env : IAsyncDisposable
    {
        public FakeClock Clock = new(At(28, 10));
        public DbFixture Fx;
        public Env() { Fx = new DbFixture(Clock); }
        public GroupHistoryQueryService Sut => new(Fx.Factory, Fx.Clock, Fx.ResetTimes, new ScheduleEvaluator());
        public ValueTask DisposeAsync() => Fx.DisposeAsync();

        /// <summary>Crea un nodo "il giorno <paramref name="createdDay"/>" (l'orologio si sposta e torna).</summary>
        public async Task<Guid> AddAsync(string name, NodeType nt, HabitType? type = HabitType.Action, Guid? parent = null,
            int createdDay = 20, int sort = 0, TimeOnly? reset = null, bool archived = false, bool deleted = false,
            ScheduleType? schedule = null, string? days = null)
        {
            Clock.Set(At(createdDay, 12));
            var id = Guid.NewGuid();
            await using (var db = await Fx.Factory.CreateDbContextAsync())
            {
                db.HabitNodes.Add(new HabitNode
                {
                    Id = id, ParentId = parent, NodeType = nt, HabitType = type, Name = name, SortOrder = sort,
                    ResetTime = reset, IsArchived = archived, Color = "#112233",
                    DeletedAt = deleted ? Clock.UtcNow.UtcDateTime : null,
                });
                if (nt == NodeType.Habit && schedule is not null)
                    db.HabitConfigs.Add(new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = id, ScheduleType = schedule.Value, ScheduleDaysJson = days });
                await db.SaveChangesAsync();
            }
            Clock.Set(At(28, 10));
            return id;
        }

        public async Task RecordAsync(Guid habit, DateTimeOffset at)
        {
            await using var db = await Fx.Factory.CreateDbContextAsync();
            db.Completions.Add(new Completion { Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds() });
            await db.SaveChangesAsync();
        }

        public async Task PeriodAsync(Guid habit, DateTimeOffset start, DateTimeOffset? end)
        {
            await using var db = await Fx.Factory.CreateDbContextAsync();
            db.AbstinencePeriods.Add(new AbstinencePeriod
            {
                Id = Guid.NewGuid(), HabitNodeId = habit, StartedAtMs = start.ToUnixTimeMilliseconds(),
                EndedAtMs = end?.ToUnixTimeMilliseconds(),
            });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Empty_group_is_empty()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        (await e.Sut.GetAsync(g, D(1), D(28))).Should().BeEmpty();
    }

    [Fact]
    public async Task Subgroup_children_are_included_with_depth()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        await e.AddAsync("A", NodeType.Habit, parent: g, sort: 0);
        var sub = await e.AddAsync("S", NodeType.Group, null, parent: g, sort: 1);
        await e.AddAsync("B", NodeType.Habit, parent: sub);
        var rows = await e.Sut.GetAsync(g, D(20), D(28));
        rows.Select(r => (r.Name, r.Depth)).Should().Equal(("A", 0), ("B", 1));
    }

    [Fact]
    public async Task Archived_and_deleted_children_are_out()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        await e.AddAsync("ok", NodeType.Habit, parent: g);
        await e.AddAsync("arch", NodeType.Habit, parent: g, archived: true);
        await e.AddAsync("del", NodeType.Habit, parent: g, deleted: true);
        (await e.Sut.GetAsync(g, D(20), D(28))).Select(r => r.Name).Should().Equal("ok");
    }

    [Fact]
    public async Task Done_days_use_each_childs_boundary()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        var late = await e.AddAsync("late", NodeType.Habit, parent: g, reset: new TimeOnly(5, 0));
        var plain = await e.AddAsync("plain", NodeType.Habit, parent: g);
        await e.RecordAsync(late, At(28, 4, 30));
        await e.RecordAsync(plain, At(28, 4, 30));
        var rows = await e.Sut.GetAsync(g, D(20), D(28));
        rows.Single(r => r.Name == "late").Done.Should().BeEquivalentTo([D(27)]);
        rows.Single(r => r.Name == "plain").Done.Should().BeEquivalentTo([D(28)]);
    }

    [Fact]
    public async Task Scheduled_respects_the_schedule()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        await e.AddAsync("mwf", NodeType.Habit, parent: g, createdDay: 1, schedule: ScheduleType.SpecificDays, days: """["Mon","Wed","Fri"]""");
        var rows = await e.Sut.GetAsync(g, D(19), D(25));   // lun 19 .. dom 25
        rows.Single().Scheduled.Should().BeEquivalentTo([D(19), D(21), D(23)]);
    }

    [Fact]
    public async Task Scheduled_starts_at_creation()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        await e.AddAsync("a", NodeType.Habit, parent: g, createdDay: 23);
        var r = (await e.Sut.GetAsync(g, D(15), D(28))).Single();
        r.StartsOn.Should().Be(D(23));
        r.Scheduled.Min().Should().Be(D(23));
        r.Scheduled.Should().HaveCount(6);
    }

    [Fact]
    public async Task Starts_at_the_oldest_of_creation_and_first_record()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        var a = await e.AddAsync("a", NodeType.Habit, parent: g, createdDay: 23);
        await e.RecordAsync(a, At(18, 12));
        var r = (await e.Sut.GetAsync(g, D(10), D(28))).Single();
        r.StartsOn.Should().Be(D(18));
        r.Scheduled.Min().Should().Be(D(18));
        r.Done.Should().BeEquivalentTo([D(18)]);
        (await e.Sut.GetStartDayAsync(a)).Should().Be(D(18));
    }

    [Fact]
    public async Task Abstinence_starts_at_its_first_period()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        var a = await e.AddAsync("a", NodeType.Habit, HabitType.Resistance, parent: g, createdDay: 5);
        await e.PeriodAsync(a, At(12, 9), null);
        var r = (await e.Sut.GetAsync(g, D(1), D(28))).Single();
        r.StartsOn.Should().Be(D(12));
        r.Scheduled.Min().Should().Be(D(12));
        (await e.Sut.GetStartDayAsync(a)).Should().Be(D(12));
    }

    [Fact]
    public async Task Never_started_abstinence_has_nothing_scheduled()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        var a = await e.AddAsync("a", NodeType.Habit, HabitType.Resistance, parent: g, createdDay: 5);
        var r = (await e.Sut.GetAsync(g, D(1), D(28))).Single();
        r.Scheduled.Should().BeEmpty();
        r.Done.Should().BeEmpty();
        (await e.Sut.GetStartDayAsync(a)).Should().Be(D(5));
    }

    [Fact]
    public async Task Future_days_are_neither()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        await e.AddAsync("a", NodeType.Habit, parent: g, createdDay: 20);
        var r = (await e.Sut.GetAsync(g, D(20), D(31))).Single();
        r.Scheduled.Max().Should().Be(D(28));
        r.Done.Should().NotContain(d => d > D(28));
    }

    [Fact]
    public async Task Abstinence_clean_days_exclude_the_lapse_day()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        var a = await e.AddAsync("a", NodeType.Habit, HabitType.Resistance, parent: g, createdDay: 5);
        await e.PeriodAsync(a, At(10, 9), At(15, 8));
        await e.PeriodAsync(a, At(15, 9), null);
        var r = (await e.Sut.GetAsync(g, D(1), D(28))).Single();
        var expected = Enumerable.Range(10, 5).Concat(Enumerable.Range(16, 13)).Select(D);
        r.Done.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Zero_duration_abstinence_period_creates_no_lapse_or_gap()
    {
        await using var e = new Env();
        var g = await e.AddAsync("G", NodeType.Group, null);
        var a = await e.AddAsync("a", NodeType.Habit, HabitType.Resistance, parent: g, createdDay: 5);
        await e.PeriodAsync(a, At(10, 9), null);
        await e.PeriodAsync(a, At(12, 14), At(12, 14));

        var r = (await e.Sut.GetAsync(g, D(1), D(28))).Single();
        var expected = Enumerable.Range(10, 19).Select(D);
        r.Done.Should().BeEquivalentTo(expected);
    }
}
