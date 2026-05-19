using System.Security.Cryptography;

namespace Recordum.Core.Abstractions.Security;

public sealed class KeyMaterial : IDisposable
{
    public const int LengthBytes = 32;

    public byte[] RawKey { get; }

    public KeyMaterial(byte[] rawKey)
    {
        ArgumentNullException.ThrowIfNull(rawKey);
        if (rawKey.Length != LengthBytes)
            throw new ArgumentException(
                $"Key must be {LengthBytes} bytes (got {rawKey.Length}).",
                nameof(rawKey));
        RawKey = rawKey;
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(RawKey);
}
