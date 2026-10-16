namespace Recordum.Core.Abstractions.Security;

/// <summary>Portachiavi di sistema (Secret Service su Linux). Le chiamate sono sincrone e possono attendere un prompt di
/// sblocco: mai sul thread dell'interfaccia. Ogni guasto è <see cref="SecretStoreUnavailableException"/>.</summary>
public interface ISecretStore
{
    bool IsAvailable { get; }

    /// <summary>Il valore, o <c>null</c> se il segreto non c'è.</summary>
    string? TryRead(SecretKind kind);

    void Write(SecretKind kind, string value);

    /// <summary>No-op se il segreto non c'è.</summary>
    void Delete(SecretKind kind);
}
