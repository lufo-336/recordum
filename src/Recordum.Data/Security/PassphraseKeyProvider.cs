using System.Text;
using Konscious.Security.Cryptography;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Errors;

namespace Recordum.Data.Security;

public sealed class PassphraseKeyProvider : IDbKeyProvider
{
    /// <summary>Lunghezza minima in caratteri; la fa rispettare anche <see cref="DbUnlockService"/> prima di costruire il provider.</summary>
    public const int MinPassphraseLength = 12;
    private readonly byte[] _passphraseBytes;
    private readonly string _saltPath;

    public PassphraseKeyProvider(string passphrase, string saltPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        ArgumentException.ThrowIfNullOrWhiteSpace(saltPath);
        if (passphrase.Length < MinPassphraseLength)
            throw new ArgumentException($"Passphrase must be at least {MinPassphraseLength} characters.", nameof(passphrase));
        _passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
        _saltPath = saltPath;
    }

    public string ProviderId => KeyProviderId.Passphrase;

    public async ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_saltPath))
            throw new KeyUnavailableException(ProviderId, $"salt file missing: {_saltPath}");
        var salt = await File.ReadAllBytesAsync(_saltPath, ct);
        if (salt.Length != 16)
            throw new KeyUnavailableException(ProviderId, $"unexpected salt length {salt.Length}");
        using var argon = new Argon2id(_passphraseBytes)
        {
            Salt = salt,
            DegreeOfParallelism = 2,
            Iterations = 4,
            MemorySize = 65_536,
        };
        var raw = await argon.GetBytesAsync(KeyMaterial.LengthBytes);
        return new KeyMaterial(raw);
    }
}
