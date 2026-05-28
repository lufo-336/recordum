using System.IO;
using System.Security.Cryptography;
using System.Text;
using Recordum.WPF.Bootstrap;

namespace Recordum.WPF.Services;

public sealed class WpfAutoBackupKeyStore(RecordumOptions options) : IAutoBackupKeyStore
{
    public bool HasKey() => File.Exists(options.AutoBackupKeyPath);

    public void WriteKey(string passphrase)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        var raw = Encoding.UTF8.GetBytes(passphrase);
#pragma warning disable CA1416
        var wrapped = ProtectedData.Protect(raw, optionalEntropy: null, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
        File.WriteAllBytes(options.AutoBackupKeyPath, wrapped);
        CryptographicOperations.ZeroMemory(raw);
    }

    public void DeleteKey()
    {
        if (File.Exists(options.AutoBackupKeyPath)) File.Delete(options.AutoBackupKeyPath);
    }
}
