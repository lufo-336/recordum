using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Backup;
using Recordum.Data.Security;
using Recordum.Data.Tests.Fixtures;
using Xunit;
using S = Recordum.Data.Tests.Backup.RecbakReferenceSeed;

namespace Recordum.Data.Tests.Backup;

/// <summary>A .recbak made on one OS restores on the other. Both reference files are committed; both CI jobs restore both.</summary>
public class CrossPlatformRestoreTests
{
    [Theory]
    [InlineData("linux.recbak")]
    [InlineData("windows.recbak")]
    public async Task Reference_backup_restores_with_known_values(string file)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "recbak", file);
        File.Exists(source).Should().BeTrue(
            $"{file} is missing: generate it with RECORDUM_GENERATE_RECBAK=<folder> dotnet test tests/Recordum.Data.Tests --filter Category=FixtureGenerator " +
            "(the Windows one is the 'recbak-fixture-windows' artifact of the CI Windows job) and commit it in Fixtures/recbak/");

        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            await fixture.NewRestoreService().RestoreAsync(source, S.Passphrase, new RestoreOptions(SkipSafetySnapshot: true));

            await using var db = await fixture.DbFactory.CreateDbContextAsync();
            await S.AssertKnownValuesAsync(db);
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Restored_database_is_encrypted_with_the_local_key_only()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "recbak", "linux.recbak");
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            await fixture.NewRestoreService().RestoreAsync(source, S.Passphrase, new RestoreOptions(SkipSafetySnapshot: true));
            SqliteConnection.ClearAllPools();

            using (var local = await fixture.KeyProvider.GetKeyAsync())
            using (var conn = SqlCipherConnectionFactory.Open(fixture.Options.DatabasePath, local))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM habit_nodes";
                Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(4);
            }
            SqliteConnection.ClearAllPools();

            using var other = new Recordum.Core.Abstractions.Security.KeyMaterial(new byte[Recordum.Core.Abstractions.Security.KeyMaterial.LengthBytes]);
            using var bad = SqlCipherConnectionFactory.Open(fixture.Options.DatabasePath, other);
            using var badCmd = bad.CreateCommand();
            badCmd.CommandText = "SELECT COUNT(*) FROM habit_nodes";
            var act = () => badCmd.ExecuteScalar();
            act.Should().Throw<SqliteException>().Which.SqliteErrorCode.Should().Be(26);
        }
        finally { SqliteConnection.ClearAllPools(); await fixture.DisposeAsync(); }
    }
}
