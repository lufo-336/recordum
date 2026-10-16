using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

/// <summary>La passphrase dell'auto-backup nel portachiavi di sistema. Se questo non risponde, <c>ReadKey</c> torna
/// <c>null</c> e lo scheduler si spegne.</summary>
public sealed class SecretStoreAutoBackupKeyStore(ISecretStore store) : IAutoBackupKeyStore
{
    public bool HasKey() => ReadKey() is not null;

    public void WriteKey(string passphrase) => store.Write(SecretKind.AutoBackup, passphrase);

    public void DeleteKey() => store.Delete(SecretKind.AutoBackup);

    public string? ReadKey()
    {
        try { return store.TryRead(SecretKind.AutoBackup); }
        catch (SecretStoreUnavailableException) { return null; }
    }
}
