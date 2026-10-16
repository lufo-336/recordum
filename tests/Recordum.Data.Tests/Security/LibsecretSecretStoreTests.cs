using System.Runtime.Versioning;
using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>Parlano con il Secret Service vero (CI: gnome-keyring in dbus-run-session; Windows li esclude).
/// Ogni test usa una data_dir unica e cancella ciò che crea.</summary>
[SupportedOSPlatform("linux")]
[Trait("Category", "Keyring")]
public sealed class LibsecretSecretStoreTests : IDisposable
{
    private readonly string _a = "/tmp/recordum-keyring-test-" + Guid.NewGuid().ToString("N");
    private readonly string _b = "/tmp/recordum-keyring-test-" + Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        foreach (var d in new[] { _a, _b })
        {
            var s = new LibsecretSecretStore(d);
            foreach (var k in Enum.GetValues<SecretKind>()) s.Delete(k);
        }
    }

    [Fact]
    public void Write_then_read_returns_value_and_delete_clears_it()
    {
        var s = new LibsecretSecretStore(_a);
        s.IsAvailable.Should().BeTrue();
        s.Write(SecretKind.DbKey, "valore-è-1");
        s.TryRead(SecretKind.DbKey).Should().Be("valore-è-1");
        s.Write(SecretKind.DbKey, "valore-2");
        s.TryRead(SecretKind.DbKey).Should().Be("valore-2");
        s.Delete(SecretKind.DbKey);
        s.TryRead(SecretKind.DbKey).Should().BeNull();
        s.Delete(SecretKind.DbKey);
    }

    [Fact]
    public void Kinds_do_not_see_each_other()
    {
        var s = new LibsecretSecretStore(_a);
        s.Write(SecretKind.DbKey, "db");
        s.TryRead(SecretKind.AutoBackup).Should().BeNull();
        s.Write(SecretKind.AutoBackup, "bk");
        s.Delete(SecretKind.DbKey);
        s.TryRead(SecretKind.AutoBackup).Should().Be("bk");
    }

    [Fact]
    public void Different_data_dirs_neither_read_nor_delete_each_other()
    {
        var a = new LibsecretSecretStore(_a);
        var b = new LibsecretSecretStore(_b);
        a.Write(SecretKind.DbKey, "chiave-a");
        b.TryRead(SecretKind.DbKey).Should().BeNull();
        b.Write(SecretKind.DbKey, "chiave-b");
        b.Delete(SecretKind.DbKey);
        a.TryRead(SecretKind.DbKey).Should().Be("chiave-a");
    }

    [Fact]
    public void Trailing_separator_is_the_same_installation()
    {
        new LibsecretSecretStore(_a).Write(SecretKind.DbKey, "x");
        new LibsecretSecretStore(_a + "/").TryRead(SecretKind.DbKey).Should().Be("x");
    }

    [Fact]
    public void Missing_library_is_unavailable_and_throws_the_domain_exception()
    {
        var s = new LibsecretSecretStore(_a, "libsecret-assente.so");
        s.IsAvailable.Should().BeFalse();
        var read = () => s.TryRead(SecretKind.DbKey);
        var write = () => s.Write(SecretKind.DbKey, "x");
        read.Should().Throw<SecretStoreUnavailableException>();
        write.Should().Throw<SecretStoreUnavailableException>();
    }
}
