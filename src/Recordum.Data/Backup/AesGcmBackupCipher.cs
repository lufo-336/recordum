using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Backup;

namespace Recordum.Data.Backup;

public sealed class AesGcmBackupCipher : IBackupCipher
{
    private static readonly byte[] Magic = "RCBK"u8.ToArray();
    private const byte FormatVersion = 0x01;
    private const byte KdfArgon2id = 0x01;
    private const int SaltLen = 16;
    private const int NonceLen = 12;
    private const int TagLen = 16;
    private const int KeyLen = 32;

    // CR3: the KDF parameters and ciphertext length below come from an UNTRUSTED header.
    // Our own writer uses memMib=64, iters=4, parallel=2; these ceilings leave head-room for
    // future hardening while blocking a malicious .recbak from forcing a ~64 GiB Argon2id
    // allocation (DoS) or buffering an unbounded ciphertext into memory.
    private const int MaxMemMib = 1024;          // 1 GiB ceiling on Argon2id memory
    private const byte MaxIters = 24;
    private const byte MaxParallel = 8;
    private const long MaxCipherTextBytes = 2L * 1024 * 1024 * 1024;  // 2 GiB hard cap

    public async Task EncryptAsync(Stream input, Stream output, string passphrase, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        ushort memMib = 64;
        byte iters = 4;
        byte parallel = 2;
        var salt = RandomNumberGenerator.GetBytes(SaltLen);
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);

        var header = new byte[4 + 1 + 3 + 1 + 2 + 1 + 1 + SaltLen + NonceLen];
        var span = header.AsSpan();
        Magic.CopyTo(span); span = span[4..];
        span[0] = FormatVersion; span = span[1..];
        span[..3].Clear(); span = span[3..];      // reserved
        span[0] = KdfArgon2id; span = span[1..];
        BinaryPrimitives.WriteUInt16BigEndian(span, memMib); span = span[2..];
        span[0] = iters; span = span[1..];
        span[0] = parallel; span = span[1..];
        salt.CopyTo(span); span = span[SaltLen..];
        nonce.CopyTo(span);

        await output.WriteAsync(header, ct).ConfigureAwait(false);

        var key = await DeriveKeyAsync(passphrase, salt, memMib, iters, parallel).ConfigureAwait(false);

        // Materialize plaintext (backups are bounded; streaming GCM here would add ad-hoc framing).
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
        CryptographicOperations.ZeroMemory(key);
    }

    public async Task DecryptAsync(Stream input, Stream output, string passphrase, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var header = new byte[4 + 1 + 3 + 1 + 2 + 1 + 1 + SaltLen + NonceLen];
        var read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct).ConfigureAwait(false);
        if (read < header.Length || !header.AsSpan(0, 4).SequenceEqual(Magic))
            throw new RestoreException("invalid_format", "Not a Recordum backup file.");
        if (header[4] != FormatVersion)
            throw new RestoreException("invalid_format", $"Unsupported backup version {header[4]}.");
        if (header[8] != KdfArgon2id)
            throw new RestoreException("invalid_format", $"Unsupported KDF {header[8]}.");

        var memMib = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(9, 2));
        var iters = header[11];
        var parallel = header[12];
        var salt = header.AsSpan(13, SaltLen).ToArray();
        var nonce = header.AsSpan(13 + SaltLen, NonceLen).ToArray();

        // CR3: bound the untrusted KDF parameters BEFORE deriving (and thus allocating) anything.
        if (memMib is < 8 or > MaxMemMib)
            throw new RestoreException("invalid_format", "Backup KDF memory parameter out of range.");
        if (iters is < 1 or > MaxIters)
            throw new RestoreException("invalid_format", "Backup KDF iteration parameter out of range.");
        if (parallel is < 1 or > MaxParallel)
            throw new RestoreException("invalid_format", "Backup KDF parallelism parameter out of range.");

        var rest = await ReadAllBoundedAsync(input, MaxCipherTextBytes, ct).ConfigureAwait(false);
        if (rest.Length < TagLen)
            throw new RestoreException("invalid_format", "Truncated backup.");

        var cipherLen = rest.Length - TagLen;
        var cipherText = rest.AsSpan(0, cipherLen).ToArray();
        var tag = rest.AsSpan(cipherLen, TagLen).ToArray();

        var key = await DeriveKeyAsync(passphrase, salt, memMib, iters, parallel).ConfigureAwait(false);
        var plain = new byte[cipherLen];
        try
        {
            using var gcm = new AesGcm(key, TagLen);
            gcm.Decrypt(nonce, cipherText, tag, plain);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            _ = ex;
            throw new RestoreException("invalid_passphrase", "Wrong passphrase or backup integrity check failed.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        await output.WriteAsync(plain, ct).ConfigureAwait(false);
    }

    private static async Task<byte[]> DeriveKeyAsync(string passphrase, byte[] salt, ushort memMib, byte iters, byte parallel)
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
    // so an oversized .recbak cannot exhaust memory before the tag is even checked.
    private static async Task<byte[]> ReadAllBoundedAsync(Stream input, long maxBytes, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            if (ms.Length + read > maxBytes)
                throw new RestoreException("invalid_format", "Backup exceeds the maximum supported size.");
            ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
    }
}
