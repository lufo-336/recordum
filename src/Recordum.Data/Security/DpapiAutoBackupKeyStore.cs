using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

/// <summary>La passphrase dell'auto-backup, cifrata con DPAPI per l'utente corrente. Era <c>WpfAutoBackupKeyStore</c>:
/// sta in Data perché la shell Avalonia su Windows la usi uguale, e perché <c>BackupScheduler</c> smetta di aprire il
/// file da solo.</summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiAutoBackupKeyStore(string keyPath) : IAutoBackupKeyStore
{
    public bool HasKey() => File.Exists(keyPath);

    public void WriteKey(string passphrase)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        var raw = Encoding.UTF8.GetBytes(passphrase);
        try
        {
            File.WriteAllBytes(keyPath, ProtectedData.Protect(raw, optionalEntropy: null, DataProtectionScope.CurrentUser));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(raw);
        }
    }

    public void DeleteKey()
    {
        if (File.Exists(keyPath)) File.Delete(keyPath);
    }

    public string? ReadKey()
    {
        if (!File.Exists(keyPath)) return null;
        try
        {
            var raw = ProtectedData.Unprotect(File.ReadAllBytes(keyPath), optionalEntropy: null, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(raw); }
            finally { CryptographicOperations.ZeroMemory(raw); }
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return null;   // chi chiama si spegne e lo scrive nel log (BackupScheduler)
        }
    }
}
