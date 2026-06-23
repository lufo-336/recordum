using System.Text;
using FluentAssertions;
using Recordum.Core.Backup;
using Recordum.Data.Backup;
using Xunit;

namespace Recordum.Data.Tests.Backup;

public class AesGcmBackupCipherTests
{
    private const string Pass = "correct-horse-battery-staple";

    [Fact]
    public async Task Roundtrip_recovers_original_payload()
    {
        var payload = Encoding.UTF8.GetBytes("hello recordum backup");
        var cipher = new AesGcmBackupCipher();

        using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(payload), encrypted, Pass);

        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        await cipher.DecryptAsync(encrypted, decrypted, Pass);

        decrypted.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task Wrong_passphrase_throws_invalid_passphrase()
    {
        var cipher = new AesGcmBackupCipher();
        using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(Encoding.UTF8.GetBytes("x")), encrypted, Pass);

        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        var ex = await Assert.ThrowsAsync<RestoreException>(
            () => cipher.DecryptAsync(encrypted, decrypted, "wrong-passphrase-here"));
        ex.Code.Should().Be("invalid_passphrase");
    }

    [Fact]
    public async Task Tampered_ciphertext_throws_invalid_passphrase()
    {
        var cipher = new AesGcmBackupCipher();
        using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(Encoding.UTF8.GetBytes("yyy")), encrypted, Pass);

        var bytes = encrypted.ToArray();
        bytes[^1] ^= 0x42;  // flip a byte in the tag region

        using var decrypted = new MemoryStream();
        var ex = await Assert.ThrowsAsync<RestoreException>(
            () => cipher.DecryptAsync(new MemoryStream(bytes), decrypted, Pass));
        ex.Code.Should().Be("invalid_passphrase");
    }

    [Fact]
    public async Task Bad_magic_throws_invalid_format()
    {
        var cipher = new AesGcmBackupCipher();
        using var decrypted = new MemoryStream();
        var bad = new byte[64];
        bad[0] = (byte)'X';  // wrong magic

        var ex = await Assert.ThrowsAsync<RestoreException>(
            () => cipher.DecryptAsync(new MemoryStream(bad), decrypted, Pass));
        ex.Code.Should().Be("invalid_format");
    }

    [Fact]
    public async Task Decrypt_rejects_out_of_range_kdf_memory()  // CR3
    {
        var cipher = new AesGcmBackupCipher();
        using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(Encoding.UTF8.GetBytes("z")), encrypted, Pass);

        var bytes = encrypted.ToArray();
        // Header offset 9..10 = Argon2id memory (MiB, UInt16 BE). Forge ~64 GiB (65535 MiB).
        bytes[9] = 0xFF;
        bytes[10] = 0xFF;

        using var decrypted = new MemoryStream();
        var ex = await Assert.ThrowsAsync<RestoreException>(
            () => cipher.DecryptAsync(new MemoryStream(bytes), decrypted, Pass));
        ex.Code.Should().Be("invalid_format");  // rejected BEFORE the giant allocation
    }

    [Fact]
    public async Task Decrypt_rejects_out_of_range_kdf_iterations()  // CR3
    {
        var cipher = new AesGcmBackupCipher();
        using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(Encoding.UTF8.GetBytes("z")), encrypted, Pass);

        var bytes = encrypted.ToArray();
        bytes[11] = 0xFF;  // header offset 11 = iterations (byte); 255 >> MaxIters

        using var decrypted = new MemoryStream();
        var ex = await Assert.ThrowsAsync<RestoreException>(
            () => cipher.DecryptAsync(new MemoryStream(bytes), decrypted, Pass));
        ex.Code.Should().Be("invalid_format");
    }

    [Fact]
    public async Task Larger_payload_roundtrips()
    {
        var rng = new Random(42);
        var payload = new byte[1_048_576];  // 1 MiB
        rng.NextBytes(payload);

        var cipher = new AesGcmBackupCipher();
        using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(payload), encrypted, Pass);

        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        await cipher.DecryptAsync(encrypted, decrypted, Pass);

        decrypted.ToArray().Should().Equal(payload);
    }
}
