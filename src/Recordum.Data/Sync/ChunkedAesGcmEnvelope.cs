using System.Buffers.Binary;
using System.Security.Cryptography;
using Recordum.Data.Backup;

namespace Recordum.Data.Sync;

/// <summary>
/// Busta binaria autenticata a blocchi (<c>Argon2id</c> + <c>AES-256-GCM</c>, versione <c>0x02</c>).
/// Cifra e decifra flussi in memoria costante: ogni blocco porta il proprio tag da 16 byte, l'ultimo blocco ha sempre
/// meno di <c>chunkSize</c> byte in chiaro (vuoto se il chiaro finisce sul confine) e ogni blocco autentica i 41 byte
/// di intestazione come AAD.
/// </summary>
internal sealed class ChunkedAesGcmEnvelope
{
    internal const byte FormatVersion = 0x02;
    internal const int DefaultChunkSize = 1 << 20;

    private readonly byte[] _magic;
    private readonly Func<EnvelopeErrorKind, string, Exception> _createException;
    private readonly Func<string, byte[], ushort, byte, byte, Task<byte[]>> _deriveKey;
    private readonly int _chunkSize;

    internal ChunkedAesGcmEnvelope(
        ReadOnlySpan<byte> magic,
        Func<EnvelopeErrorKind, string, Exception> createException,
        Func<string, byte[], ushort, byte, byte, Task<byte[]>> deriveKey,
        int chunkSize = DefaultChunkSize)
    {
        if (magic.Length != 4)
            throw new ArgumentException("Envelope magic must be 4 bytes.", nameof(magic));
        ArgumentNullException.ThrowIfNull(createException);
        ArgumentNullException.ThrowIfNull(deriveKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chunkSize, int.MaxValue - AesGcmEnvelope.TagLen);

        _magic = magic.ToArray();
        _createException = createException;
        _deriveKey = deriveKey;
        _chunkSize = chunkSize;
    }

    internal async Task EncryptAsync(
        Stream input,
        Stream output,
        string passphrase,
        ReadOnlyMemory<byte> fixedSalt = default,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        if (!fixedSalt.IsEmpty && fixedSalt.Length != AesGcmEnvelope.SaltLen)
            throw new ArgumentException($"Salt must be {AesGcmEnvelope.SaltLen} bytes.", nameof(fixedSalt));

        const ushort memMib = AesGcmEnvelope.DefaultMemMib;
        const byte iters = AesGcmEnvelope.DefaultIters;
        const byte parallel = AesGcmEnvelope.DefaultParallel;
        var salt = fixedSalt.IsEmpty
            ? RandomNumberGenerator.GetBytes(AesGcmEnvelope.SaltLen)
            : fixedSalt.ToArray();
        var baseNonce = RandomNumberGenerator.GetBytes(AesGcmEnvelope.NonceLen);

        var header = new byte[AesGcmEnvelope.HeaderLen];
        var span = header.AsSpan();
        _magic.CopyTo(span); span = span[4..];
        span[0] = FormatVersion; span = span[1..];
        span[..3].Clear(); span = span[3..];
        span[0] = AesGcmEnvelope.KdfArgon2id; span = span[1..];
        BinaryPrimitives.WriteUInt16BigEndian(span, memMib); span = span[2..];
        span[0] = iters; span = span[1..];
        span[0] = parallel; span = span[1..];
        salt.CopyTo(span); span = span[AesGcmEnvelope.SaltLen..];
        baseNonce.CopyTo(span);

        await output.WriteAsync(header, ct).ConfigureAwait(false);

        var key = await _deriveKey(passphrase, salt, memMib, iters, parallel).ConfigureAwait(false);
        var plainBuffer = new byte[_chunkSize];
        var cipherBuffer = new byte[_chunkSize + AesGcmEnvelope.TagLen];
        try
        {
            EnsureNonZeroKey(key);
            using var gcm = new AesGcm(key, AesGcmEnvelope.TagLen);

            ulong chunkIndex = 0;
            while (true)
            {
                if (chunkIndex > uint.MaxValue)
                    throw _createException(EnvelopeErrorKind.OversizedPayload, "Sync payload exceeds the maximum supported size.");

                var read = await input.ReadAtLeastAsync(plainBuffer, _chunkSize, throwOnEndOfStream: false, ct).ConfigureAwait(false);
                var isFinal = read < _chunkSize;

                EncryptChunk(
                    gcm,
                    baseNonce,
                    (uint)chunkIndex,
                    isFinal,
                    header,
                    plainBuffer.AsSpan(0, read),
                    cipherBuffer.AsSpan(0, read),
                    cipherBuffer.AsSpan(read, AesGcmEnvelope.TagLen));

                await output.WriteAsync(cipherBuffer.AsMemory(0, read + AesGcmEnvelope.TagLen), ct).ConfigureAwait(false);
                if (isFinal)
                    break;

                chunkIndex++;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBuffer);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    internal async Task DecryptAsync(
        Stream input,
        Stream output,
        string passphrase,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var header = new byte[AesGcmEnvelope.HeaderLen];
        var headerRead = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct).ConfigureAwait(false);
        if (headerRead < 4 || !header.AsSpan(0, 4).SequenceEqual(_magic))
            throw _createException(EnvelopeErrorKind.BadMagic, "Not a Recordum sync file.");
        if (headerRead < header.Length)
            throw _createException(EnvelopeErrorKind.Truncated, "Truncated sync file.");
        if (header[4] < FormatVersion)
            throw _createException(EnvelopeErrorKind.UnsupportedVersion, $"Unsupported sync envelope version {header[4]}.");
        if (header[4] > FormatVersion)
            throw _createException(EnvelopeErrorKind.NewerVersion, $"Sync envelope version {header[4]} requires a newer version of Recordum.");
        if (header[8] != AesGcmEnvelope.KdfArgon2id)
            throw _createException(EnvelopeErrorKind.UnsupportedKdf, $"Unsupported KDF {header[8]}.");

        var memMib = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(9, 2));
        var iters = header[11];
        var parallel = header[12];
        var salt = header.AsSpan(13, AesGcmEnvelope.SaltLen).ToArray();

        // Parametri KDF dall'header non fidato: validati prima di allocare memoria in Argon2id.
        if (memMib is < 8 or > AesGcmEnvelope.MaxMemMib)
            throw _createException(EnvelopeErrorKind.KdfParamsOutOfRange, "Sync KDF memory parameter out of range.");
        if (iters is < 1 or > AesGcmEnvelope.MaxIters)
            throw _createException(EnvelopeErrorKind.KdfParamsOutOfRange, "Sync KDF iteration parameter out of range.");
        if (parallel is < 1 or > AesGcmEnvelope.MaxParallel)
            throw _createException(EnvelopeErrorKind.KdfParamsOutOfRange, "Sync KDF parallelism parameter out of range.");

        var key = await _deriveKey(passphrase, salt, memMib, iters, parallel).ConfigureAwait(false);
        var plainBuffer = new byte[_chunkSize];
        var cipherBuffer = new byte[_chunkSize + AesGcmEnvelope.TagLen];
        try
        {
            EnsureNonZeroKey(key);
            using var gcm = new AesGcm(key, AesGcmEnvelope.TagLen);

            ulong chunkIndex = 0;
            while (true)
            {
                if (chunkIndex > uint.MaxValue)
                    throw _createException(EnvelopeErrorKind.OversizedPayload, "Sync payload exceeds the maximum supported size.");

                var read = await input.ReadAtLeastAsync(cipherBuffer, cipherBuffer.Length, throwOnEndOfStream: false, ct).ConfigureAwait(false);
                if (read < AesGcmEnvelope.TagLen)
                    throw _createException(EnvelopeErrorKind.Truncated, "Truncated sync file.");

                var isFinal = read < cipherBuffer.Length;
                var plainLen = read - AesGcmEnvelope.TagLen;

                DecryptChunk(
                    gcm,
                    header.AsSpan(13 + AesGcmEnvelope.SaltLen, AesGcmEnvelope.NonceLen),
                    (uint)chunkIndex,
                    isFinal,
                    header,
                    cipherBuffer.AsSpan(0, plainLen),
                    cipherBuffer.AsSpan(plainLen, AesGcmEnvelope.TagLen),
                    plainBuffer.AsSpan(0, plainLen));

                if (plainLen > 0)
                    await output.WriteAsync(plainBuffer.AsMemory(0, plainLen), ct).ConfigureAwait(false);

                if (isFinal)
                {
                    if (await input.ReadAtLeastAsync(cipherBuffer.AsMemory(0, 1), 1, throwOnEndOfStream: false, ct).ConfigureAwait(false) > 0)
                        throw _createException(EnvelopeErrorKind.AuthenticationFailed, "Trailing data after final sync chunk.");
                    break;
                }

                chunkIndex++;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBuffer);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private void DecryptChunk(
        AesGcm gcm,
        ReadOnlySpan<byte> baseNonce,
        uint chunkIndex,
        bool isFinal,
        ReadOnlySpan<byte> header,
        ReadOnlySpan<byte> cipherSpan,
        ReadOnlySpan<byte> tagSpan,
        Span<byte> plainSpan)
    {
        Span<byte> chunkNonce = stackalloc byte[AesGcmEnvelope.NonceLen];
        ComputeChunkNonce(baseNonce, chunkIndex, isFinal, chunkNonce);
        try
        {
            gcm.Decrypt(chunkNonce, cipherSpan, tagSpan, plainSpan, header);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            _ = ex;
            throw _createException(EnvelopeErrorKind.AuthenticationFailed, "Wrong passphrase or sync file integrity check failed.");
        }
    }

    private static void EncryptChunk(
        AesGcm gcm,
        ReadOnlySpan<byte> baseNonce,
        uint chunkIndex,
        bool isFinal,
        ReadOnlySpan<byte> header,
        ReadOnlySpan<byte> plainSpan,
        Span<byte> cipherSpan,
        Span<byte> tagSpan)
    {
        Span<byte> chunkNonce = stackalloc byte[AesGcmEnvelope.NonceLen];
        ComputeChunkNonce(baseNonce, chunkIndex, isFinal, chunkNonce);
        gcm.Encrypt(chunkNonce, plainSpan, cipherSpan, tagSpan, header);
    }

    private static void ComputeChunkNonce(
        ReadOnlySpan<byte> baseNonce,
        uint chunkIndex,
        bool isFinal,
        Span<byte> destination)
    {
        baseNonce.CopyTo(destination);
        destination[7] ^= (byte)(chunkIndex >> 24);
        destination[8] ^= (byte)(chunkIndex >> 16);
        destination[9] ^= (byte)(chunkIndex >> 8);
        destination[10] ^= (byte)chunkIndex;
        if (isFinal)
            destination[11] ^= 0x01;
    }

    private static void EnsureNonZeroKey(ReadOnlySpan<byte> key)
    {
        // Difesa in profondità: una chiave tutta a zero (cache azzerata altrove) non cifra né decifra mai.
        if (key.Length != AesGcmEnvelope.KeyLen)
            throw new CryptographicException($"Derived key must be {AesGcmEnvelope.KeyLen} bytes.");
        Span<byte> zeros = stackalloc byte[AesGcmEnvelope.KeyLen];
        if (CryptographicOperations.FixedTimeEquals(key, zeros))
            throw new CryptographicException("Derived key is all zero.");
    }
}
