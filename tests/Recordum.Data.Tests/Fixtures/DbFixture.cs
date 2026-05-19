using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    public DbFixture(FakeClock? clock = null, FakePluginContext? pluginContext = null)
    {
        Clock = clock ?? new FakeClock();
        PluginContext = pluginContext ?? new FakePluginContext();
        _connection = new SqliteConnection("DataSource=:memory:;Cache=Shared");
        _connection.Open();
        var ts = new TimestampInterceptor(Clock, PluginContext);
        Factory = new FixtureFactory(_connection, ts);
        using var db = Factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private sealed class FixtureFactory(SqliteConnection conn, TimestampInterceptor ts)
        : IDbContextFactory<RecordumDbContext>
    {
        public RecordumDbContext CreateDbContext()
        {
            var opts = new DbContextOptionsBuilder<RecordumDbContext>()
                .UseSqlite(conn)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(ts)
                .Options;
            return new RecordumDbContext(opts);
        }
    }
}
