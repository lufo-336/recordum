using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Data.Interceptors;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Interceptors;

/// <summary>F2c-2: il cancello che il restore chiude per scambiare i file del database.</summary>
public class DatabaseGateTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task Ordinary_queries_give_their_permit_back_so_the_gate_closes_at_once()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
                await db.HabitNodes.IgnoreQueryFilters().CountAsync();
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
                db.HabitNodes.IgnoreQueryFilters().Count();   // anche la via sincrona

            using var quiet = await fixture.Gate.QuiesceAsync(Short);
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task An_open_connection_holds_the_gate_and_the_timeout_reopens_it()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            await using var holder = await fixture.DbFactory.CreateDbContextAsync();
            await holder.Database.OpenConnectionAsync();

            var act = () => fixture.Gate.QuiesceAsync(Short);
            await act.Should().ThrowAsync<TimeoutException>();

            // Riaperto: un altro utente lavora di nuovo.
            await using var other = await fixture.DbFactory.CreateDbContextAsync();
            (await other.AppSettings.CountAsync()).Should().BeGreaterThan(0);
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Closing_waits_for_the_open_connection_to_close()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var holder = await fixture.DbFactory.CreateDbContextAsync();
            await holder.Database.OpenConnectionAsync();

            var closing = fixture.Gate.QuiesceAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(100);
            closing.IsCompleted.Should().BeFalse();

            await holder.DisposeAsync();
            using var quiet = await closing;
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task An_async_open_on_a_closed_gate_waits_and_runs_when_it_reopens()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var quiet = await fixture.Gate.QuiesceAsync(Short);
            var query = Task.Run(async () =>
            {
                await using var db = await fixture.DbFactory.CreateDbContextAsync();
                return await db.AppSettings.CountAsync();
            });
            await Task.Delay(150);
            query.IsCompleted.Should().BeFalse();

            quiet.Dispose();
            (await query.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeGreaterThan(0);
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task A_sync_open_on_a_closed_gate_throws_instead_of_blocking()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            using var quiet = await fixture.Gate.QuiesceAsync(Short);
            await using var db = await fixture.DbFactory.CreateDbContextAsync();

            var act = () => db.HabitNodes.IgnoreQueryFilters().Count();
            act.Should().Throw<DatabaseQuiescedException>();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Kept_closed_it_stays_closed_and_a_waiting_open_can_be_cancelled()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            using (var quiet = await fixture.Gate.QuiesceAsync(Short)) quiet.KeepClosed();

            using var cts = new CancellationTokenSource(Short);
            await using var db = await fixture.DbFactory.CreateDbContextAsync();
            var act = () => db.HabitNodes.IgnoreQueryFilters().CountAsync(cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();

            fixture.Gate.Reopen();   // per il Dispose del fixture
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task A_failed_open_gives_its_permit_back()
    {
        var gate = new DatabaseGate();
        var missing = Path.Combine(Path.GetTempPath(), "recordum-gate-" + Guid.NewGuid().ToString("N"), "none.db");
        var options = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = missing, Mode = SqliteOpenMode.ReadOnly }.ToString())
            .AddInterceptors(gate)
            .Options;
        await using (var db = new RecordumDbContext(options))
        {
            var open = () => db.Database.OpenConnectionAsync();
            await open.Should().ThrowAsync<SqliteException>();
        }

        using var quiet = await gate.QuiesceAsync(Short);
    }

    [Fact]
    public async Task IsKeptClosed_is_true_after_KeepClosed_and_false_otherwise()
    {
        var gate = new DatabaseGate();
        gate.IsKeptClosed.Should().BeFalse();

        using (var quiet = await gate.QuiesceAsync(Short))
        {
            gate.IsKeptClosed.Should().BeFalse();
        }
        gate.IsKeptClosed.Should().BeFalse();

        using (var quiet = await gate.QuiesceAsync(Short))
        {
            quiet.KeepClosed();
            gate.IsKeptClosed.Should().BeTrue();
        }
        gate.IsKeptClosed.Should().BeTrue();

        gate.Reopen();
        gate.IsKeptClosed.Should().BeFalse();
    }
}
