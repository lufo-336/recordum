using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Backup;

namespace Recordum.Data.Backup;

public sealed class AesGcmBackupCipher : IBackupCipher
{
    private static readonly AesGcmEnvelope Envelope = new(
        "RCBK"u8,
        static (kind, message) => kind switch
        {
            EnvelopeErrorKind.AuthenticationFailed => new RestoreException("invalid_passphrase", message),
            _ => new RestoreException("invalid_format", message),
        });

    public Task EncryptAsync(Stream input, Stream output, string passphrase, CancellationToken ct = default) =>
        Envelope.EncryptAsync(input, output, passphrase, ct: ct);

    public Task DecryptAsync(Stream input, Stream output, string passphrase, CancellationToken ct = default) =>
        Envelope.DecryptAsync(input, output, passphrase, ct);
}

internal enum EnvelopeErrorKind
{
    BadMagic,
    UnsupportedVersion,
    UnsupportedKdf,
    KdfParamsOutOfRange,
    OversizedPayload,
    Truncated,
    AuthenticationFailed,
    NewerVersion,
}

internal sealed class AesGcmEnvelope
{
    internal const byte FormatVersion = 0x01;
    internal const byte KdfArgon2id = 0x01;
    internal const ushort DefaultMemMib = 64;
    internal const byte DefaultIters = 4;
    internal const byte DefaultParallel = 2;
    internal const int SaltLen = 16;
    internal const int NonceLen = 12;
    internal const int TagLen = 16;
    internal const int KeyLen = 32;
    internal const int HeaderLen = 4 + 1 + 3 + 1 + 2 + 1 + 1 + SaltLen + NonceLen;

    // CR3: the KDF parameters and ciphertext length below come from an UNTRUSTED header.
    // Our own writer uses memMib=64, iters=4, parallel=2; these ceilings leave head-room for
    // future hardening while blocking a malicious envelope from forcing a ~64 GiB Argon2id
    // allocation (DoS) or buffering an unbounded ciphertext into memory.
    internal const int MaxMemMib = 1024;          // 1 GiB ceiling on Argon2id memory
    internal const byte MaxIters = 24;
    internal const byte MaxParallel = 8;
    internal const long MaxCipherTextBytes = 2L * 1024 * 1024 * 1024;  // 2 GiB hard cap

    private readonly byte[] _magic;
    private readonly Func<EnvelopeErrorKind, string, Exception> _createException;
    private readonly Func<string, byte[], ushort, byte, byte, Task<byte[]>> _deriveKey;

    internal AesGcmEnvelope(
        ReadOnlySpan<byte> magic,
        Func<EnvelopeErrorKind, string, Exception> createException,
        Func<string, byte[], ushort, byte, byte, Task<byte[]>>? deriveKey = null)
    {
        if (magic.Length != 4)
            throw new ArgumentException("Envelope magic must be 4 bytes.", nameof(magic));
        ArgumentNullException.ThrowIfNull(createException);

        _magic = magic.ToArray();
        _createException = createException;
        _deriveKey = deriveKey ?? DeriveKeyArgon2idAsync;
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

        const ushort memMib = DefaultMemMib;
        const byte iters = DefaultIters;
        const byte parallel = DefaultParallel;
        var salt = fixedSalt.IsEmpty
            ? RandomNumberGenerator.GetBytes(SaltLen)
            : fixedSalt.ToArray();
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);

        var header = new byte[HeaderLen];
        var span = header.AsSpan();
        _magic.CopyTo(span); span = span[4..];
        span[0] = FormatVersion; span = span[1..];
        span[..3].Clear(); span = span[3..];      // reserved
        span[0] = KdfArgon2id; span = span[1..];
        BinaryPrimitives.WriteUInt16BigEndian(span, memMib); span = span[2..];
        span[0] = iters; span = span[1..];
        span[0] = parallel; span = span[1..];
        salt.CopyTo(span); span = span[SaltLen..];
        nonce.CopyTo(span);

        await output.WriteAsync(header, ct).ConfigureAwait(false);

        var key = await _deriveKey(passphrase, salt, memMib, iters, parallel).ConfigureAwait(false);
        try
        {
            // Difesa in profondità: una chiave tutta a zero (cache azzerata altrove) non cifra mai.
            if (CryptographicOperations.FixedTimeEquals(key, new byte[key.Length]))
                throw new CryptographicException("Derived key is all zero.");
            // Materialize plaintext (backups and sync files are bounded; streaming GCM here would add ad-hoc framing).
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms, ct).ConfigureAwait(false);
            var plain = ms.ToArray();
            var ct_buf = new byte[plain.Length];
            var tag = new byte[TagLen];
            using (var gcm = new AesGcm(key, TagLen))
            {
                gcm.Encrypt(nonce, plain, ct_buf, tag);
            }
            await output.WriteAsync(ct_buf, ct).ConfigureAwait(false);
            await output.WriteAsync(tag, ct).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    internal async Task DecryptAsync(Stream input, Stream output, string passphrase, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var header = new byte[HeaderLen];
        var read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct).ConfigureAwait(false);
        if (read < 4 || !header.AsSpan(0, 4).SequenceEqual(_magic))
            throw _createException(EnvelopeErrorKind.BadMagic, "Not a Recordum backup file.");
        if (read < header.Length)
            throw _createException(EnvelopeErrorKind.Truncated, "Truncated backup.");
        if (header[4] != FormatVersion)
            throw _createException(EnvelopeErrorKind.UnsupportedVersion, $"Unsupported backup version {header[4]}.");
        if (header[8] != KdfArgon2id)
            throw _createException(EnvelopeErrorKind.UnsupportedKdf, $"Unsupported KDF {header[8]}.");

        var memMib = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(9, 2));
        var iters = header[11];
        var parallel = header[12];
        var salt = header.AsSpan(13, SaltLen).ToArray();
        var nonce = header.AsSpan(13 + SaltLen, NonceLen).ToArray();

        // CR3: bound the untrusted KDF parameters BEFORE deriving (and thus allocating) anything.
        if (memMib is < 8 or > MaxMemMib)
            throw _createException(EnvelopeErrorKind.KdfParamsOutOfRange, "Backup KDF memory parameter out of range.");
        if (iters is < 1 or > MaxIters)
            throw _createException(EnvelopeErrorKind.KdfParamsOutOfRange, "Backup KDF iteration parameter out of range.");
        if (parallel is < 1 or > MaxParallel)
            throw _createException(EnvelopeErrorKind.KdfParamsOutOfRange, "Backup KDF parallelism parameter out of range.");

        var rest = await ReadAllBoundedAsync(input, MaxCipherTextBytes, ct).ConfigureAwait(false);
        if (rest.Length < TagLen)
            throw _createException(EnvelopeErrorKind.Truncated, "Truncated backup.");

        var cipherLen = rest.Length - TagLen;
        var cipherText = rest.AsSpan(0, cipherLen).ToArray();
        var tag = rest.AsSpan(cipherLen, TagLen).ToArray();

        var key = await _deriveKey(passphrase, salt, memMib, iters, parallel).ConfigureAwait(false);
        var plain = new byte[cipherLen];
        try
        {
            using var gcm = new AesGcm(key, TagLen);
            gcm.Decrypt(nonce, cipherText, tag, plain);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            _ = ex;
            throw _createException(EnvelopeErrorKind.AuthenticationFailed, "Wrong passphrase or backup integrity check failed.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        await output.WriteAsync(plain, ct).ConfigureAwait(false);
    }

    internal static async Task<byte[]> DeriveKeyArgon2idAsync(
        string passphrase,
        byte[] salt,
        ushort memMib,
        byte iters,
        byte parallel)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(passphrase))
        {
            Salt = salt,
            DegreeOfParallelism = parallel,
            Iterations = iters,
            MemorySize = memMib * 1024,
        };
        return await argon.GetBytesAsync(KeyLen).ConfigureAwait(false);
    }

    // CR3: read an untrusted stream fully but refuse to buffer more than <paramref name="maxBytes"/>,
    // so an oversized envelope cannot exhaust memory before the tag is even checked.
    private async Task<byte[]> ReadAllBoundedAsync(Stream input, long maxBytes, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            if (ms.Length + read > maxBytes)
                throw _createException(EnvelopeErrorKind.OversizedPayload, "Backup exceeds the maximum supported size.");
            ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
    }
}
