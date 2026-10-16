using System.Security.Cryptography;
using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

/// <summary>La chiave ricordata nel portachiavi, come <see cref="IDbKeyProvider"/>. <c>ProviderId</c> e' quello della
/// passphrase: e' la stessa chiave che la passphrase deriva. Serve copie e azzera la propria al <see cref="Dispose"/>.</summary>
public sealed class StoredKeyProvider : IDbKeyProvider, IDisposable
{
    private readonly byte[] _key;

    public StoredKeyProvider(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeyMaterial.LengthBytes)
            throw new ArgumentException($"Key must be {KeyMaterial.LengthBytes} bytes (got {key.Length}).", nameof(key));
        _key = key.ToArray();
    }

    public string ProviderId => KeyProviderId.Passphrase;

    public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(new KeyMaterial((byte[])_key.Clone()));

    public void Dispose() => CryptographicOperations.ZeroMemory(_key);
}
