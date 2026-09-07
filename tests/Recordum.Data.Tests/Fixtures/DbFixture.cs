using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions;
using Recordum.Data;
using Recordum.Data.Interceptors;

namespace Recordum.Data.Tests.Fixtures;

public sealed class DbFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CommandCounterInterceptor _counter = new();

    public IClock Clock { get; }

    public FakePluginContext PluginContext { get; }

    public IDbContextFactory<RecordumDbContext> Factory { get; }

    public DbFixture(
        FakeClock? clock = null,
        FakePluginContext? pluginContext = null,
        bool enableHistory = true)
    {
        Clock = clock ?? new FakeClock();
        PluginContext = pluginContext ?? new FakePluginContext();
        _connection = new SqliteConnection("DataSource=:memory:;Cache=Shared");
        _connection.Open();
        var history = enableHistory
            ? new HabitConfigHistoryInterceptor(
                Clock, NullLogger<HabitConfigHistoryInterceptor>.Instance)
            : null;
        var timestamp = new TimestampInterceptor(Clock, PluginContext);
        Factory = new FixtureFactory(_connection, history, timestamp, _counter);
        using var db = Factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    /// <summary>Conta i comandi SQL eseguiti finché non viene disposto. Per i test che devono
    /// dimostrare "una passata sola", non "abbastanza veloce".</summary>
    public CommandCountScope CountCommands() => new(_counter);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private sealed class FixtureFactory(
        SqliteConnection conn,
        HabitConfigHistoryInterceptor? history,
        TimestampInterceptor timestamp,
        CommandCounterInterceptor counter)
        : IDbContextFactory<RecordumDbContext>
    {
        public RecordumDbContext CreateDbContext()
        {
            var b = new DbContextOptionsBuilder<RecordumDbContext>()
                .UseSqlite(conn)
                .UseSnakeCaseNamingConvention();
            if (history is not null) b.AddInterceptors(history, timestamp, counter);
            else                     b.AddInterceptors(timestamp, counter);
            return new RecordumDbContext(b.Options);
        }
    }

    public sealed class CommandCountScope(CommandCounterInterceptor counter) : IDisposable
    {
        private readonly int _start = counter.Count;
        public int Count => counter.Count - _start;
        public void Dispose() { }
    }

    public sealed class CommandCounterInterceptor : DbCommandInterceptor
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
