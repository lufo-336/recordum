using System.Runtime.Versioning;
using FluentAssertions;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>DPAPI esiste solo su Windows: il job Linux della CI esclude questa classe, quello Windows la esegue.</summary>
[SupportedOSPlatform("windows")]
[Trait("Category", "WindowsOnly")]
public sealed class DpapiAutoBackupKeyStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "recordum-autokey-" + Guid.NewGuid().ToString("N"));
    private readonly DpapiAutoBackupKeyStore _store;

    public DpapiAutoBackupKeyStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new DpapiAutoBackupKeyStore(Path.Combine(_dir, "autobackup.key"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* best effort */ }
    }

    [Fact]
    public void A_written_passphrase_reads_back_and_is_not_stored_in_clear()
    {
        _store.WriteKey("correct horse battery staple");

        _store.HasKey().Should().BeTrue();
        _store.ReadKey().Should().Be("correct horse battery staple");
        File.ReadAllText(Path.Combine(_dir, "autobackup.key")).Should().NotContain("horse");
    }

    [Fact]
    public void No_file_or_an_unreadable_one_reads_as_no_key()
    {
        _store.ReadKey().Should().BeNull();

        File.WriteAllBytes(Path.Combine(_dir, "autobackup.key"), [1, 2, 3]);   // non è un blob DPAPI

        _store.ReadKey().Should().BeNull();
    }
}
