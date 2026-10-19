using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

/// <summary>Le operazioni sullo stesso segreto, una alla volta e nell'ordine in cui arrivano (F2c-4). Lo sblocco
/// scrive la chiave con un tetto di tempo: se il portachiavi aspetta un prompt, la scrittura resta in volo. Senza
/// quest'ordine un "Dimentica" fatto nel frattempo cancellerebbe per primo, e la scrittura tardiva rimetterebbe la
/// chiave; cosi' la cancellazione aspetta la scrittura e vince lei.</summary>
public sealed class SerializedSecretStore(ISecretStore inner) : ISecretStore
{
    private readonly object _dbKey = new();
    private readonly object _autoBackup = new();

    public bool IsAvailable => inner.IsAvailable;

    public string? TryRead(SecretKind kind)
    {
        lock (Gate(kind)) return inner.TryRead(kind);
    }

    public void Write(SecretKind kind, string value)
    {
        lock (Gate(kind)) inner.Write(kind, value);
    }

    public void Delete(SecretKind kind)
    {
        lock (Gate(kind)) inner.Delete(kind);
    }

    private object Gate(SecretKind kind) => kind == SecretKind.DbKey ? _dbKey : _autoBackup;
}
