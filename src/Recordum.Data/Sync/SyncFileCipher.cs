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
    private readonly object _cacheLock = new();
    private readonly Dictionary<CacheKey, byte[]> _keyCache = new();
    private readonly Func<string, byte[], ushort, byte, byte, Task<byte[]>> _deriveKey;
    private readonly ChunkedAesGcmEnvelope _envelope;

    public SyncFileCipher()
        : this(AesGcmEnvelope.DeriveKeyArgon2idAsync)
    {
    }

    internal SyncFileCipher(Func<string, byte[], ushort, byte, byte, Task<byte[]>> deriveKey)
        : this(deriveKey, ChunkedAesGcmEnvelope.DefaultChunkSize)
    {
    }

    internal SyncFileCipher(Func<string, byte[], ushort, byte, byte, Task<byte[]>> deriveKey, int chunkSize)
    {
        ArgumentNullException.ThrowIfNull(deriveKey);
        _deriveKey = deriveKey;
        _envelope = new ChunkedAesGcmEnvelope("RCSY"u8, CreateException, GetOrDeriveKeyAsync, chunkSize);
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
        var passBytes = Encoding.UTF8.GetBytes(passphrase);
        byte[] passHash;
        try { passHash = SHA256.HashData(passBytes); }
        finally { CryptographicOperations.ZeroMemory(passBytes); }
        var cacheKey = new CacheKey(salt, memMib, iters, parallel, passHash);

        // Lookup, clone, insert ed eviction stanno tutti sotto lo stesso lock: nessuno clona una chiave che un altro sta azzerando.
        lock (_cacheLock)
        {
            if (_keyCache.TryGetValue(cacheKey, out var cached))
                return (byte[])cached.Clone();
        }

        // Argon2id fuori dal lock: una derivazione doppia concorrente è innocua.
        var derived = await _deriveKey(passphrase, salt, memMib, iters, parallel).ConfigureAwait(false);
        lock (_cacheLock)
        {
            if (_keyCache.TryGetValue(cacheKey, out var existing))
            {
                CryptographicOperations.ZeroMemory(derived);
                return (byte[])existing.Clone();
            }
            // ponytail: bound naive (al limite si tolgono tutte le chiavi dei salt altrui, quelle del salt di scrittura restano); LRU se servisse.
            if (_keyCache.Count >= MaxCachedKeys)
            {
                foreach (var k in _keyCache.Keys.Where(k => !k.HasSalt(_processSalt)).ToList())
                {
                    CryptographicOperations.ZeroMemory(_keyCache[k]);
                    _keyCache.Remove(k);
                }
            }
            var result = (byte[])derived.Clone();
            _keyCache[cacheKey] = derived;
            return result;
        }
    }

    private static Exception CreateException(EnvelopeErrorKind kind, string message) =>
        kind switch
        {
            EnvelopeErrorKind.BadMagic => new SyncFileException(SyncFileError.NotASyncFile, "Not a Recordum sync file."),
            EnvelopeErrorKind.NewerVersion => new SyncFormatException(message, isNewerMajorVersion: true),
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

        public bool HasSalt(byte[] salt) => _salt.AsSpan().SequenceEqual(salt);

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
