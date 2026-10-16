using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

/// <summary>Nessun portachiavi (Windows: lì c'è DPAPI). <c>Write</c> non finge di aver salvato.</summary>
public sealed class NullSecretStore : ISecretStore
{
    public bool IsAvailable => false;

    public string? TryRead(SecretKind kind) => null;

    public void Write(SecretKind kind, string value) =>
        throw new NotSupportedException("No system keyring on this platform.");

    public void Delete(SecretKind kind) { }
}
