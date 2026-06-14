using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Security;
using Recordum.Data;
using Recordum.Data.Interceptors;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>
/// Every opened SQLCipher connection must carry a non-zero <c>busy_timeout</c>. With the SQLite default of 0,
/// a writer that finds the file momentarily locked by another connection fails instantly with
/// SQLITE_BUSY ("database is locked") instead of waiting — which crashed the app when a background
/// operation overlapped a user write. See phase-4 issue B-dbLock.
/// </summary>
public class SqlCipherBusyTimeoutTests
{
    [Fact]
    public async Task Factory_Open_sets_busy_timeout()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recordum-test-{Guid.NewGuid():N}.db");
        try
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            using var conn = SqlCipherConnectionFactory.Open(path, new KeyMaterial(key));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA busy_timeout;";
            var value = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.True(value > 0, $"busy_timeout should be non-zero, was {value}");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Interceptor_sets_busy_timeout_on_open()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recordum-test-{Guid.NewGuid():N}.db");
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        try
        {
            var opts = new DbContextOptionsBuilder<RecordumDbContext>()
                .UseSqlite($"Data Source={path}")
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(new SqlCipherKeyConnectionInterceptor(new FakeKeyProvider(key)))
                .Options;
            await using var ctx = new RecordumDbContext(opts);
            await ctx.Database.OpenConnectionAsync();   // routes through the connection interceptor
            var conn = ctx.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA busy_timeout;";
            var value = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.True(value > 0, $"busy_timeout should be non-zero, was {value}");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class FakeKeyProvider(byte[] key) : IDbKeyProvider
    {
        public string ProviderId => "fake";
        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default)
            => ValueTask.FromResult(new KeyMaterial((byte[])key.Clone()));
    }
}
