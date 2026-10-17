using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

/// <summary>La passphrase dell'auto-backup nel portachiavi di sistema. <c>null</c> = la passphrase manca; se il
/// portachiavi non risponde, <see cref="SecretStoreUnavailableException"/> arriva al chiamante (R11).</summary>
public sealed class SecretStoreAutoBackupKeyStore(ISecretStore store) : IAutoBackupKeyStore
{
    public bool HasKey() => ReadKey() is not null;

    public void WriteKey(string passphrase) => store.Write(SecretKind.AutoBackup, passphrase);

    public void DeleteKey() => store.Delete(SecretKind.AutoBackup);

    public string? ReadKey() => store.TryRead(SecretKind.AutoBackup);
}
