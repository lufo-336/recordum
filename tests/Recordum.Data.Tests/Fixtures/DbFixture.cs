using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions;
using Recordum.Data;
using Recordum.Data.Interceptors;

namespace Recordum.Data.Tests.Fixtures;

public sealed class DbFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

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
        Factory = new FixtureFactory(_connection, history, timestamp);
        using var db = Factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private sealed class FixtureFactory(
        SqliteConnection conn,
        HabitConfigHistoryInterceptor? history,
        TimestampInterceptor timestamp)
        : IDbContextFactory<RecordumDbContext>
    {
        public RecordumDbContext CreateDbContext()
        {
            var b = new DbContextOptionsBuilder<RecordumDbContext>()
                .UseSqlite(conn)
                .UseSnakeCaseNamingConvention();
            if (history is not null) b.AddInterceptors(history, timestamp);
            else                     b.AddInterceptors(timestamp);
            return new RecordumDbContext(b.Options);
        }
    }
}
