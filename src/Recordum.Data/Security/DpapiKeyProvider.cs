using System.Runtime.Versioning;
using System.Security.Cryptography;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Errors;

namespace Recordum.Data.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiKeyProvider(string keyFilePath) : IDbKeyProvider
{
    public string ProviderId => KeyProviderId.Dpapi;

    public async ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default)
    {
        if (!File.Exists(keyFilePath))
        {
            var fresh = new byte[KeyMaterial.LengthBytes];
            RandomNumberGenerator.Fill(fresh);
            var protectedBytes = ProtectedData.Protect(fresh, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var dir = Path.GetDirectoryName(keyFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(keyFilePath, protectedBytes, ct);
            return new KeyMaterial(fresh);
        }
        try
        {
            var blob = await File.ReadAllBytesAsync(keyFilePath, ct);
            var raw = ProtectedData.Unprotect(blob, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return new KeyMaterial(raw);
        }
        catch (CryptographicException ex)
        {
            throw new KeyUnavailableException(ProviderId, $"cannot decrypt {keyFilePath}: {ex.Message}");
        }
    }
}
