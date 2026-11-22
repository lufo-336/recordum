using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Recordum.Data.Backup;

namespace Recordum.Data.Sync;

/// <summary>
/// Cifra e decifra i file di scambio <c>.rcsync</c> (busta <c>RCSY</c>, Argon2id + AES-256-GCM).
/// Singleton: usa un salt casuale per processo in scrittura e conserva in memoria la chiave derivata
/// per (salt, parametri KDF, SHA-256 della frase), così Argon2id gira una sola volta per salt.
/// </summary>
public sealed class SyncFileCipher
{
    private const int MaxCachedKeys = 16;
    private readonly byte[] _processSalt = RandomNumberGenerator.GetBytes(AesGcmEnvelope.SaltLen);
    private readonly ConcurrentDictionary<CacheKey, byte[]> _keyCache = new();
    private readonly Func<string, byte[], ushort, byte, byte, Task<byte[]>> _deriveKey;
    private readonly AesGcmEnvelope _envelope;

    public SyncFileCipher()
        : this(AesGcmEnvelope.DeriveKeyArgon2idAsync)
    {
    }

    internal SyncFileCipher(Func<string, byte[], ushort, byte, byte, Task<byte[]>> deriveKey)
    {
        ArgumentNullException.ThrowIfNull(deriveKey);
        _deriveKey = deriveKey;
        _envelope = new AesGcmEnvelope("RCSY"u8, CreateException, GetOrDeriveKeyAsync);
    }

    public Task EncryptAsync(Stream plain, Stream output, string passphrase, CancellationToken ct = default) =>
        _envelope.EncryptAsync(plain, output, passphrase, _processSalt, ct);

    public Task DecryptAsync(Stream input, Stream plain, string passphrase, CancellationToken ct = default) =>
        _envelope.DecryptAsync(input, plain, passphrase, ct);

    private async Task<byte[]> GetOrDeriveKeyAsync(
        string passphrase,
        byte[] salt,
        ushort memMib,
        byte iters,
        byte parallel)
    {
        var passHash = SHA256.HashData(Encoding.UTF8.GetBytes(passphrase));
        var cacheKey = new CacheKey(salt, memMib, iters, parallel, passHash);
        if (_keyCache.TryGetValue(cacheKey, out var cached))
            return (byte[])cached.Clone();

        var derived = await _deriveKey(passphrase, salt, memMib, iters, parallel).ConfigureAwait(false);
        // ponytail: bound naive (svuota tutto al limite), basta per un salt di scrittura + pochi salt di altri dispositivi; LRU se servisse.
        if (_keyCache.Count >= MaxCachedKeys)
        {
            foreach (var k in _keyCache.Keys)
                if (_keyCache.TryRemove(k, out var old))
                    CryptographicOperations.ZeroMemory(old);
        }
        var canonical = _keyCache.GetOrAdd(cacheKey, _ => (byte[])derived.Clone());
        CryptographicOperations.ZeroMemory(derived);
        return (byte[])canonical.Clone();
    }

    private static SyncFileException CreateException(EnvelopeErrorKind kind, string message) =>
        kind switch
        {
            EnvelopeErrorKind.BadMagic => new SyncFileException(SyncFileError.NotASyncFile, "Not a Recordum sync file."),
            EnvelopeErrorKind.UnsupportedVersion or
            EnvelopeErrorKind.UnsupportedKdf or
            EnvelopeErrorKind.KdfParamsOutOfRange or
            EnvelopeErrorKind.OversizedPayload => new SyncFileException(SyncFileError.UnsupportedEnvelope, message),
            EnvelopeErrorKind.Truncated or
            EnvelopeErrorKind.AuthenticationFailed => new SyncFileException(
                SyncFileError.CannotOpen,
                "Wrong passphrase or incomplete sync file."),
            _ => new SyncFileException(SyncFileError.CannotOpen, message),
        };

    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        private readonly byte[] _salt;
        private readonly ushort _memMib;
        private readonly byte _iters;
        private readonly byte _parallel;
        private readonly byte[] _passphraseSha256;
        private readonly int _hashCode;

        public CacheKey(
            ReadOnlySpan<byte> salt,
            ushort memMib,
            byte iters,
            byte parallel,
            ReadOnlySpan<byte> passphraseSha256)
        {
            _salt = salt.ToArray();
            _memMib = memMib;
            _iters = iters;
            _parallel = parallel;
            _passphraseSha256 = passphraseSha256.ToArray();

            var hc = new HashCode();
            hc.AddBytes(salt);
            hc.Add(memMib);
            hc.Add(iters);
            hc.Add(parallel);
            hc.AddBytes(passphraseSha256);
            _hashCode = hc.ToHashCode();
        }

        public bool Equals(CacheKey other) =>
            _memMib == other._memMib &&
            _iters == other._iters &&
            _parallel == other._parallel &&
            _salt.AsSpan().SequenceEqual(other._salt) &&
            CryptographicOperations.FixedTimeEquals(_passphraseSha256, other._passphraseSha256);

        public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        public override int GetHashCode() => _hashCode;
    }
}
