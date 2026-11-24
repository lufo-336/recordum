using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Recordum.Data.Sync;

namespace Recordum.Data.Tests.Sync;

[CollectionDefinition("ChunkedEnvelope", DisableParallelization = true)]
public sealed class ChunkedEnvelopeCollection;

[Collection("ChunkedEnvelope")]
public sealed class ChunkedEnvelopeTests
{
    private const string Pass = "correct-horse-battery-staple";
    private const int TestChunkSize = 16;

    // Chiave deterministica da frase e salt, ignorando i parametri Argon2id: permette di provare l'AAD sull'header.
    private static Task<byte[]> FakeDeriveKeyAsync(string passphrase, byte[] salt, ushort mem, byte iters, byte parallel)
    {
        _ = mem;
        _ = iters;
        _ = parallel;
        var key = SHA256.HashData(Encoding.UTF8.GetBytes($"{passphrase}:{Convert.ToHexString(salt)}"));
        return Task.FromResult(key);
    }

    private static SyncFileCipher CreateSmallChunkCipher() => new(FakeDeriveKeyAsync, TestChunkSize);

    private static byte[] MakePlaintext(int size)
    {
        var data = new byte[size];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i * 31 + 7);
        return data;
    }

    private static async Task<byte[]> EncryptBytesAsync(SyncFileCipher cipher, byte[] plain, string passphrase = Pass)
    {
        using var input = new MemoryStream(plain);
        using var output = new MemoryStream();
        await cipher.EncryptAsync(input, output, passphrase);
        return output.ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(48)]
    [InlineData(1000)]
    public async Task Round_trip_for_sizes_around_the_chunk(int size)
    {
        var cipher = CreateSmallChunkCipher();
        var plain = MakePlaintext(size);
        var encrypted = await EncryptBytesAsync(cipher, plain);

        using var output = new MemoryStream();
        await cipher.DecryptAsync(new MemoryStream(encrypted), output, Pass);

        output.ToArray().Should().Equal(plain);
    }

    [Fact]
    public async Task Truncation_at_a_chunk_boundary_is_rejected()
    {
        var cipher = CreateSmallChunkCipher();
        var encrypted = await EncryptBytesAsync(cipher, MakePlaintext(48));

        // 48 byte con chunkSize=16: 3 blocchi pieni + 1 ultimo blocco vuoto da 16 B (solo tag).
        var truncated = encrypted[..^16];

        using var output = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SyncFileException>(
            () => cipher.DecryptAsync(new MemoryStream(truncated), output, Pass));
        ex.Error.Should().Be(SyncFileError.CannotOpen);
    }

    [Fact]
    public async Task Swapped_chunks_are_rejected()
    {
        var cipher = CreateSmallChunkCipher();
        var encrypted = await EncryptBytesAsync(cipher, MakePlaintext(48));

        // Header = 41 B; ogni blocco pieno = 16 B ciphertext + 16 B tag = 32 B.
        const int headerLen = 41;
        const int chunkDiskLen = TestChunkSize + 16;
        var swapped = (byte[])encrypted.Clone();
        encrypted.AsSpan(headerLen + chunkDiskLen, chunkDiskLen).CopyTo(swapped.AsSpan(headerLen, chunkDiskLen));
        encrypted.AsSpan(headerLen, chunkDiskLen).CopyTo(swapped.AsSpan(headerLen + chunkDiskLen, chunkDiskLen));

        using var output = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SyncFileException>(
            () => cipher.DecryptAsync(new MemoryStream(swapped), output, Pass));
        ex.Error.Should().Be(SyncFileError.CannotOpen);
    }

    [Fact]
    public async Task A_trailing_byte_after_the_final_chunk_is_rejected()
    {
        var cipher = CreateSmallChunkCipher();
        var encrypted = await EncryptBytesAsync(cipher, MakePlaintext(20));
        byte[] withTrailing = [.. encrypted, 0x00];

        using var output = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SyncFileException>(
            () => cipher.DecryptAsync(new MemoryStream(withTrailing), output, Pass));
        ex.Error.Should().Be(SyncFileError.CannotOpen);
    }

    [Fact]
    public async Task A_changed_header_byte_is_rejected()
    {
        var cipher = CreateSmallChunkCipher();
        var encrypted = await EncryptBytesAsync(cipher, MakePlaintext(32));

        // Byte 11 = iterazioni (4 -> 5, ancora nei limiti 1..24): con FakeDeriveKeyAsync la chiave non cambia, quindi fallisce solo l'AAD.
        encrypted[11] = 5;

        using var output = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SyncFileException>(
            () => cipher.DecryptAsync(new MemoryStream(encrypted), output, Pass));
        ex.Error.Should().Be(SyncFileError.CannotOpen);
    }

    [Fact]
    public async Task Wrong_passphrase_writes_nothing()
    {
        var cipher = CreateSmallChunkCipher();
        var encrypted = await EncryptBytesAsync(cipher, MakePlaintext(100));

        using var output = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SyncFileException>(
            () => cipher.DecryptAsync(new MemoryStream(encrypted), output, "wrong-passphrase"));
        ex.Error.Should().Be(SyncFileError.CannotOpen);
        output.Length.Should().Be(0);
    }

    [Fact]
    public async Task A_version_1_envelope_is_rejected()
    {
        var cipher = CreateSmallChunkCipher();
        var encrypted = await EncryptBytesAsync(cipher, MakePlaintext(32));
        encrypted[4] = 0x01;

        using var output = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SyncFileException>(
            () => cipher.DecryptAsync(new MemoryStream(encrypted), output, Pass));
        ex.Error.Should().Be(SyncFileError.UnsupportedEnvelope);
    }

    [Fact]
    public async Task A_newer_envelope_version_reports_newer_format()
    {
        var cipher = CreateSmallChunkCipher();
        var encrypted = await EncryptBytesAsync(cipher, MakePlaintext(32));
        encrypted[4] = 0x03;

        using var output = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SyncFormatException>(
            () => cipher.DecryptAsync(new MemoryStream(encrypted), output, Pass));
        ex.IsNewerMajorVersion.Should().BeTrue();
    }

    [Fact]
    public async Task Decrypting_64_MiB_allocates_little()
    {
        var writer = new SyncFileCipher(FakeDeriveKeyAsync);

        // Warmup di DecryptAsync prima di misurare, così le allocazioni una tantum del JIT non entrano nel conto.
        var warmEncrypted = await EncryptBytesAsync(writer, [1]);
        await writer.DecryptAsync(new MemoryStream(warmEncrypted), Stream.Null, Pass);

        var plain = new byte[64 * 1024 * 1024];
        RandomNumberGenerator.Fill(plain);
        using var encrypted = new MemoryStream();
        await writer.EncryptAsync(new MemoryStream(plain), encrypted, Pass);
        encrypted.Position = 0;

        var reader = new SyncFileCipher(FakeDeriveKeyAsync);
        var before = GC.GetTotalAllocatedBytes(precise: true);
        await reader.DecryptAsync(encrypted, Stream.Null, Pass);
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

        allocated.Should().BeLessThan(16L * 1024 * 1024);
    }
}
