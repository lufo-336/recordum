using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Data;
using Recordum.Data.Interceptors;
using Recordum.Data.Services;
using Recordum.Data.Sync;

namespace Recordum.Data.Tests.Fixtures;

public sealed class DbFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CommandCounterInterceptor _counter = new();
    private IResetTimeResolver? _resetTimes;

    public IClock Clock { get; }

    /// <summary>Il testo degli SELECT eseguiti (per asserire cosa NON si legge, es. le colonne BLOB).</summary>
    public List<string> Sql => _counter.Sql;

    public FakePluginContext PluginContext { get; }

    public FakeDeviceIdentity DeviceIdentity { get; }

    public IDbContextFactory<RecordumDbContext> Factory { get; }

    /// <summary>Il resolver reale sul DB della fixture: i servizi che risolvono il confine di giornata
    /// lo prendono da qui. Con FakeClock a fuso UTC e nessun reset configurato, il confine è l'identità
    /// (mezzanotte UTC) — ed è perché i test scritti prima di R9 restano veri.</summary>
    public IResetTimeResolver ResetTimes => _resetTimes ??= new ResetTimeResolver(
        Factory,
        new AppSettingService(Factory, Clock, NullLogger<AppSettingService>.Instance),
        Clock,
        NullLogger<ResetTimeResolver>.Instance);

    public DbFixture(
        FakeClock? clock = null,
        FakePluginContext? pluginContext = null,
        bool enableHistory = true,
        FakeDeviceIdentity? deviceIdentity = null)
    {
        Clock = clock ?? new FakeClock();
        PluginContext = pluginContext ?? new FakePluginContext();
        DeviceIdentity = deviceIdentity ?? new FakeDeviceIdentity();
        _connection = new SqliteConnection("DataSource=:memory:;Cache=Shared");
        _connection.Open();
        // La connessione è aperta qui, non da EF: SyncConnectionInterceptor non la vede aprirsi.
        SyncConnectionInterceptor.Register(_connection, Clock);
        var history = enableHistory
            ? new HabitConfigHistoryInterceptor(
                Clock, NullLogger<HabitConfigHistoryInterceptor>.Instance)
            : null;
        var timestamp = new TimestampInterceptor(Clock, PluginContext, DeviceIdentity);
        Factory = new FixtureFactory(_connection, history, timestamp, _counter);
        using var db = Factory.CreateDbContext();
        db.Database.EnsureCreated();
        SyncTriggerInstaller.EnsureAsync(db, CancellationToken.None).GetAwaiter().GetResult();
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
        public List<string> Sql { get; } = [];
        public int Count => Volatile.Read(ref _count);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            lock (Sql) Sql.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            lock (Sql) Sql.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
