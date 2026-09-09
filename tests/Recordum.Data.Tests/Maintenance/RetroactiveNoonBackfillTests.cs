using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Maintenance;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Maintenance;

/// <summary>
/// Il backfill una tantum di R9: le retroattive salvate a mezzanotte UTC vanno a mezzogiorno locale
/// del giorno che già dichiarano. È l'eccezione motivata alla regola "nessun backfill" — quel giorno
/// l'utente l'aveva scelto a mano, e col confine locale la mezzanotte UTC può cadere in quello prima.
/// </summary>
public class RetroactiveNoonBackfillTests
{
    private static readonly TimeZoneInfo Plus2 = TestTimeZones.Fixed(2);

    private static async Task<(DbFixture fx, Guid habitId)> SeedHabitAsync(
        DbFixture fx, TimeOnly? resetTime = null)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action,
            Name = "h", ResetTime = resetTime,
        };
        db.HabitNodes.Add(h);
        await db.SaveChangesAsync();
        return (fx, h.Id);
    }

    private static async Task<Guid> SeedCompletionAsync(
        DbFixture fx, Guid habitId, DateTimeOffset at, bool retroactive)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        var c = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habitId,
            CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
            IsRetroactive = retroactive,
        };
        db.Completions.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private static RetroactiveNoonBackfill Backfill(DbFixture fx) => new(
        fx.Factory, fx.ResetTimes,
        new AppSettingService(fx.Factory, fx.Clock, NullLogger<AppSettingService>.Instance),
        NullLogger<RetroactiveNoonBackfill>.Instance);

    private static async Task<Completion> ReloadAsync(DbFixture fx, Guid id)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        return await db.Completions.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    private static DbFixture NewFixture() =>
        new(clock: new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero), Plus2));

    /// <summary>Mezzanotte UTC del 15 agosto, in fuso +2 con reset 04:00, significa "14 agosto": il
    /// backfill la riporta a mezzogiorno locale del 15, cioè 10:00Z.</summary>
    [Fact]
    public async Task Moves_a_retroactive_completion_to_local_noon_of_the_day_it_declares()
    {
        await using var fx = NewFixture();
        var (_, habitId) = await SeedHabitAsync(fx, new TimeOnly(4, 0));
        var id = await SeedCompletionAsync(
            fx, habitId, new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero), retroactive: true);

        var moved = await Backfill(fx).RunAsync();

        moved.Should().Be(1);
        var after = await ReloadAsync(fx, id);
        after.CompletedAtMs.Should().Be(
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
        // Il giorno dichiarato non si è mosso: è tutto lo scopo dell'operazione.
        var boundary = await fx.ResetTimes.GetDayBoundaryAsync(habitId);
        boundary.DayOf(after.CompletedAtMs).Should().Be(new DateOnly(2026, 8, 15));
    }

    /// <summary>L'istante è l'istante: una registrazione live non si tocca mai (spec §5).</summary>
    [Fact]
    public async Task Leaves_non_retroactive_completions_alone()
    {
        await using var fx = NewFixture();
        var (_, habitId) = await SeedHabitAsync(fx);
        var at = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
        var liveId = await SeedCompletionAsync(fx, habitId, at, retroactive: false);

        await Backfill(fx).RunAsync();

        (await ReloadAsync(fx, liveId)).CompletedAtMs.Should().Be(at.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task Is_idempotent()
    {
        await using var fx = NewFixture();
        var (_, habitId) = await SeedHabitAsync(fx);
        var id = await SeedCompletionAsync(
            fx, habitId, new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero), retroactive: true);
        var backfill = Backfill(fx);
        await backfill.RunAsync();
        var after = (await ReloadAsync(fx, id)).CompletedAtMs;

        var second = await backfill.RunAsync();

        second.Should().Be(0);
        (await ReloadAsync(fx, id)).CompletedAtMs.Should().Be(after);
    }

    /// <summary>Il guard è l'AppSetting, non l'assenza di lavoro: azzerato il flag il passo rigira, e
    /// il risultato è lo stesso perché i valori sono già a mezzogiorno.</summary>
    [Fact]
    public async Task Runs_again_if_the_flag_is_reset_and_finds_nothing_left_to_move()
    {
        await using var fx = NewFixture();
        var (_, habitId) = await SeedHabitAsync(fx);
        var id = await SeedCompletionAsync(
            fx, habitId, new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero), retroactive: true);
        var settings = new AppSettingService(fx.Factory, fx.Clock, NullLogger<AppSettingService>.Instance);
        var backfill = new RetroactiveNoonBackfill(
            fx.Factory, fx.ResetTimes, settings, NullLogger<RetroactiveNoonBackfill>.Instance);
        (await backfill.RunAsync()).Should().Be(1);
        var after = (await ReloadAsync(fx, id)).CompletedAtMs;

        await settings.SetAsync(RetroactiveNoonBackfill.DoneKey, "false");
        var again = await backfill.RunAsync();

        again.Should().Be(0);
        (await ReloadAsync(fx, id)).CompletedAtMs.Should().Be(after);
    }

    /// <summary>Due abitudini con reset diversi: ognuna riceve il proprio mezzogiorno — che nello
    /// stesso fuso coincide, ma il codice non deve assumerlo, e il giorno dichiarato dev'essere
    /// preservato per entrambe.</summary>
    [Fact]
    public async Task Uses_each_habits_own_boundary()
    {
        await using var fx = NewFixture();
        var (_, early) = await SeedHabitAsync(fx, new TimeOnly(0, 0));
        var (_, late) = await SeedHabitAsync(fx, new TimeOnly(6, 0));
        var declared = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
        var earlyId = await SeedCompletionAsync(fx, early, declared, retroactive: true);
        var lateId = await SeedCompletionAsync(fx, late, declared, retroactive: true);

        (await Backfill(fx).RunAsync()).Should().Be(2);

        foreach (var (habitId, completionId) in new[] { (early, earlyId), (late, lateId) })
        {
            var boundary = await fx.ResetTimes.GetDayBoundaryAsync(habitId);
            var row = await ReloadAsync(fx, completionId);
            boundary.DayOf(row.CompletedAtMs).Should().Be(new DateOnly(2026, 8, 15));
        }
    }

    /// <summary>Lasciarne indietro una delle due colonne è il modo classico di rompere la
    /// sincronizzazione last-write-wins.</summary>
    [Fact]
    public async Task Updates_both_CompletedAt_and_CompletedAtMs()
    {
        await using var fx = NewFixture();
        var (_, habitId) = await SeedHabitAsync(fx);
        var id = await SeedCompletionAsync(
            fx, habitId, new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero), retroactive: true);

        await Backfill(fx).RunAsync();

        var row = await ReloadAsync(fx, id);
        row.CompletedAt.Should().Be(new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc));
        new DateTimeOffset(DateTime.SpecifyKind(row.CompletedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
            .Should().Be(row.CompletedAtMs);
    }
}
