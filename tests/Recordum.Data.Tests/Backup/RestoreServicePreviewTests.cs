#pragma warning disable CA1416   // Windows-only (DpapiKeyProvider in EncryptedDbFixture)

using System.Runtime.Versioning;
using FluentAssertions;
using Recordum.Core.Backup;
using Recordum.Data.Backup;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Backup;

[SupportedOSPlatform("windows")]
[Trait("Category", "WindowsOnly")]
public class RestoreServicePreviewTests
{
    private const string Pass = "passphrase-1234567890";

    [Fact]
    public async Task Preview_returns_manifest_for_valid_backup()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var bk = await fixture.CreateBackupAsync(Pass);

            var restore = fixture.NewRestoreService();

            var preview = await restore.PreviewAsync(bk.Path, Pass);

            preview.Manifest.SchemaVersion.Should().Be("2.18");
            preview.Warnings.Should().BeEmpty();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Preview_with_wrong_passphrase_throws_invalid_passphrase()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var bk = await fixture.CreateBackupAsync(Pass);

            var restore = fixture.NewRestoreService();

            var ex = await Assert.ThrowsAsync<RestoreException>(
                () => restore.PreviewAsync(bk.Path, "wrong-passphrase-here"));
            ex.Code.Should().Be("invalid_passphrase");
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Preview_with_higher_major_version_throws_incompatible_schema()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var bkPath = await fixture.CreateBackupWithSchemaAsync(Pass, "3.0");

            var restore = fixture.NewRestoreService();

            var ex = await Assert.ThrowsAsync<RestoreException>(
                () => restore.PreviewAsync(bkPath, Pass));
            ex.Code.Should().Be("incompatible_schema");
        }
        finally { await fixture.DisposeAsync(); }
    }
}
