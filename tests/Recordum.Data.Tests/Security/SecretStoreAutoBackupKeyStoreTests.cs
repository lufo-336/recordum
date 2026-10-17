using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

public sealed class SecretStoreAutoBackupKeyStoreTests
{
    private sealed class MemoryStore : ISecretStore
    {
        public Dictionary<SecretKind, string> Items { get; } = [];
        public bool IsAvailable => true;
        public string? TryRead(SecretKind kind) => Items.GetValueOrDefault(kind);
        public void Write(SecretKind kind, string value) => Items[kind] = value;
        public void Delete(SecretKind kind) => Items.Remove(kind);
    }

    private sealed class ThrowingStore : ISecretStore
    {
        public bool IsAvailable => false;
        public string? TryRead(SecretKind kind) => throw new SecretStoreUnavailableException("no");
        public void Write(SecretKind kind, string value) => throw new SecretStoreUnavailableException("no");
        public void Delete(SecretKind kind) => throw new SecretStoreUnavailableException("no");
    }

    [Fact]
    public void Write_then_read_round_trips_on_the_AutoBackup_slot_only()
    {
        var mem = new MemoryStore();
        var s = new SecretStoreAutoBackupKeyStore(mem);
        s.HasKey().Should().BeFalse();
        s.WriteKey("frase");
        s.ReadKey().Should().Be("frase");
        s.HasKey().Should().BeTrue();
        mem.Items.Keys.Should().ContainSingle().Which.Should().Be(SecretKind.AutoBackup);
        s.DeleteKey();
        s.ReadKey().Should().BeNull();
    }

    [Fact]
    public void An_unavailable_store_is_reported_not_turned_into_a_missing_key()
    {
        var s = new SecretStoreAutoBackupKeyStore(new ThrowingStore());
        s.Invoking(x => x.ReadKey()).Should().Throw<SecretStoreUnavailableException>();
        s.Invoking(x => x.HasKey()).Should().Throw<SecretStoreUnavailableException>();
    }
}
