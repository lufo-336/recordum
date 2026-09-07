using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Services;

public class ResetTimeResolverTests
{
    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static (DbFixture fx, ResetTimeResolver resolver) Build()
    {
        var fx = new DbFixture(new FakeClock(
            new DateTimeOffset(2026, 8, 19, 0, 30, 0, TimeSpan.Zero), Plus2));
        var settings = new AppSettingService(fx.Factory, fx.Clock, NullLogger<AppSettingService>.Instance);
        var resolver = new ResetTimeResolver(
            fx.Factory, settings, fx.Clock, NullLogger<ResetTimeResolver>.Instance);
        return (fx, resolver);
    }

    private static async Task<Guid> AddNodeAsync(
        DbFixture fx, Guid? parentId, TimeOnly? resetTime, bool deleted = false)
    {
        var id = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode
        {
            Id = id,
            ParentId = parentId,
            NodeType = parentId is null ? NodeType.Group : NodeType.Habit,
            HabitType = parentId is null ? null : HabitType.Action,
            Name = "n",
            ResetTime = resetTime,
            DeletedAt = deleted ? new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) : null,
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Boundary_carries_the_clock_timezone_and_the_effective_reset()
    {
        var (fx, resolver) = Build();
        await using var _ = fx;
        var group = await AddNodeAsync(fx, null, new TimeOnly(4, 0));
        var habit = await AddNodeAsync(fx, group, null);   // eredita dal gruppo

        var b = await resolver.GetDayBoundaryAsync(habit);

        b.TimeZone.Should().Be(Plus2);
        b.ResetTime.Should().Be(new TimeOnly(4, 0));
        // 00:30Z = 02:30 locali: con reset 04:00 la registrazione appartiene a ieri.
        b.Today(fx.Clock).Should().Be(new DateOnly(2026, 8, 18));
    }

    [Fact]
    public async Task Without_any_reset_the_boundary_is_local_midnight()
    {
        var (fx, resolver) = Build();
        await using var _ = fx;
        var habit = await AddNodeAsync(fx, null, null);

        var b = await resolver.GetDayBoundaryAsync(habit);

        b.ResetTime.Should().Be(TimeOnly.MinValue);
        // 00:30Z = 02:30 locali del 19: senza reset è già il 19, mentre in UTC era ancora il 19
        // solo per caso — il test che discrimina è quello sopra.
        b.Today(fx.Clock).Should().Be(new DateOnly(2026, 8, 19));
    }

    [Fact]
    public async Task Batch_agrees_with_the_single_resolution_for_every_habit()
    {
        var (fx, resolver) = Build();
        await using var _ = fx;
        var group = await AddNodeAsync(fx, null, new TimeOnly(5, 0));
        var inherits = await AddNodeAsync(fx, group, null);
        var overrides = await AddNodeAsync(fx, group, new TimeOnly(3, 0));
        var orphan = await AddNodeAsync(fx, null, null);

        var batch = await resolver.GetDayBoundariesAsync([inherits, overrides, orphan]);

        batch[inherits].Should().Be(await resolver.GetDayBoundaryAsync(inherits));
        batch[overrides].ResetTime.Should().Be(new TimeOnly(3, 0));
        batch[orphan].ResetTime.Should().Be(TimeOnly.MinValue);
    }

    [Fact]
    public async Task Batch_resolves_deleted_nodes_too()
    {
        // L'albero Cestino passa da qui: un nodo soft-deleted deve avere il suo confine,
        // o la sua riga perde strisce e cifra chiave.
        var (fx, resolver) = Build();
        await using var _ = fx;
        var group = await AddNodeAsync(fx, null, new TimeOnly(6, 0));
        var trashed = await AddNodeAsync(fx, group, null, deleted: true);

        var batch = await resolver.GetDayBoundariesAsync([trashed]);

        batch[trashed].ResetTime.Should().Be(new TimeOnly(6, 0));
    }

    [Fact]
    public async Task Unknown_id_falls_back_to_the_app_default()
    {
        var (fx, resolver) = Build();
        await using var _ = fx;
        var settings = new AppSettingService(fx.Factory, fx.Clock, NullLogger<AppSettingService>.Instance);
        await settings.SetAsync("daily_reset_time", "07:00");

        var single = await resolver.GetDayBoundaryAsync(Guid.NewGuid());
        var batch = await resolver.GetDayBoundariesAsync([Guid.NewGuid()]);
        var app = await resolver.GetAppDayBoundaryAsync();

        single.ResetTime.Should().Be(new TimeOnly(7, 0));
        batch.Values.Single().ResetTime.Should().Be(new TimeOnly(7, 0));
        app.ResetTime.Should().Be(new TimeOnly(7, 0));
    }

    [Fact]
    public async Task A_cycle_in_the_tree_does_not_hang_the_batch()
    {
        // Impossibile via UI (Move valida), ma un DB importato può portarlo: la versione singola
        // ha già un guard, la batch deve averlo uguale o l'albero Home si pianta.
        var (fx, resolver) = Build();
        await using var _ = fx;
        var a = await AddNodeAsync(fx, null, null);
        var b = await AddNodeAsync(fx, a, null);
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var na = await db.HabitNodes.FindAsync(a);
            na!.ParentId = b;
            await db.SaveChangesAsync();
        }

        var batch = await resolver.GetDayBoundariesAsync([a, b]);

        batch[a].ResetTime.Should().Be(TimeOnly.MinValue);
        batch[b].ResetTime.Should().Be(TimeOnly.MinValue);
    }

    [Fact]
    public async Task Batch_reads_the_tree_once_regardless_of_how_many_habits()
    {
        // Il vincolo di perf della spec §3.2, misurato dove si può misurare: il numero di query.
        var (fx, resolver) = Build();
        await using var _ = fx;
        var group = await AddNodeAsync(fx, null, new TimeOnly(4, 0));
        var ids = new List<Guid>();
        for (var i = 0; i < 50; i++) ids.Add(await AddNodeAsync(fx, group, null));

        using var counter = fx.CountCommands();
        await resolver.GetDayBoundariesAsync(ids);

        counter.Count.Should().BeLessThanOrEqualTo(2,
            "il batch legge l'albero una volta e l'AppSetting una volta, non una cascata per abitudine");
    }
}
