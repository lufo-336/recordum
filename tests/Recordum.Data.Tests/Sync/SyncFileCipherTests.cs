using System.Text;
using FluentAssertions;
using Recordum.Core.Backup;
using Recordum.Data.Backup;
using Recordum.Data.Sync;

namespace Recordum.Data.Tests.Sync;

public class SyncFileCipherTests
{
    private const string Pass = "correct-horse-battery-staple";

    [Fact]
    public async Task RoundTrip()
    {
        var payload = Encoding.UTF8.GetBytes("hello recordum sync envelope");
        var cipher = new SyncFileCipher();

        using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(payload), encrypted, Pass);

        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        await cipher.DecryptAsync(encrypted, decrypted, Pass);

        decrypted.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task BackupFileIsNotASyncFile()
    {
        var payload = Encoding.UTF8.GetBytes("cross-format check");
        var backupCipher = new AesGcmBackupCipher();
        var syncCipher = new SyncFileCipher();

        using var recbak = new MemoryStream();
        await backupCipher.EncryptAsync(new MemoryStream(payload), recbak, Pass);
        recbak.Position = 0;

        using var syncOut = new MemoryStream();
        var syncEx = await Assert.ThrowsAsync<SyncFileException>(
            () => syncCipher.DecryptAsync(recbak, syncOut, Pass));
        syncEx.Error.Should().Be(SyncFileError.NotASyncFile);

        using var rcsync = new MemoryStream();
        await syncCipher.EncryptAsync(new MemoryStream(payload), rcsync, Pass);
        rcsync.Position = 0;

        using var backupOut = new MemoryStream();
        var restoreEx = await Assert.ThrowsAsync<RestoreException>(
            () => backupCipher.DecryptAsync(rcsync, backupOut, Pass));
        restoreEx.Code.Should().Be("invalid_format");
    }

    [Fact]
    public async Task TruncatedFileCannotOpen()
    {
        var payload = Encoding.UTF8.GetBytes(new string('a', 512));
        var cipher = new SyncFileCipher();

        using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(payload), encrypted, Pass);

        var bytes = encrypted.ToArray();
        var truncated = bytes[..^100];

        using var decrypted = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SyncFileException>(
            () => cipher.DecryptAsync(new MemoryStream(truncated), decrypted, Pass));
        ex.Error.Should().Be(SyncFileError.CannotOpen);
    }

    [Fact]
    public async Task WrongPassphraseCannotOpen()
    {
        var payload = Encoding.UTF8.GetBytes("secret sync data");
        var cipher = new SyncFileCipher();

        using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(payload), encrypted, Pass);

        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SyncFileException>(
            () => cipher.DecryptAsync(encrypted, decrypted, "wrong-passphrase-here"));
        ex.Error.Should().Be(SyncFileError.CannotOpen);
    }

    [Fact]
    public async Task KeyDerivedOncePerSalt()
    {
        var payload = Encoding.UTF8.GetBytes("cached key derivation payload");
        var writerDerivations = 0;
        var writer = new SyncFileCipher((pass, salt, mem, iters, par) =>
        {
            Interlocked.Increment(ref writerDerivations);
            return AesGcmEnvelope.DeriveKeyArgon2idAsync(pass, salt, mem, iters, par);
        });

        using var encrypted = new MemoryStream();
        await writer.EncryptAsync(new MemoryStream(payload), encrypted, Pass);
        var encryptedBytes = encrypted.ToArray();
        writerDerivations.Should().Be(1);

        var readerDerivations = 0;
        var reader = new SyncFileCipher((pass, salt, mem, iters, par) =>
        {
            Interlocked.Increment(ref readerDerivations);
            return AesGcmEnvelope.DeriveKeyArgon2idAsync(pass, salt, mem, iters, par);
        });

        using var plain1 = new MemoryStream();
        await reader.DecryptAsync(new MemoryStream(encryptedBytes), plain1, Pass);
        using var plain2 = new MemoryStream();
        await reader.DecryptAsync(new MemoryStream(encryptedBytes), plain2, Pass);

        readerDerivations.Should().Be(1);
        plain1.ToArray().Should().Equal(payload);
        plain2.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task OutOfRangeKdfParametersThrowUnsupportedEnvelope()
    {
        var cipher = new SyncFileCipher();
        using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(Encoding.UTF8.GetBytes("z")), encrypted, Pass);

        var bytes = encrypted.ToArray();
        bytes[9] = 0xFF;
        bytes[10] = 0xFF;

        using var decrypted = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SyncFileException>(
            () => cipher.DecryptAsync(new MemoryStream(bytes), decrypted, Pass));
        ex.Error.Should().Be(SyncFileError.UnsupportedEnvelope);
    }

    [Fact]
    public async Task EvictionKeepsWriteSaltAndForeignKeysRederive()
    {
        var count = 0;
        var cipher = new SyncFileCipher((_, _, _, _, _) =>
        {
            Interlocked.Increment(ref count);
            return Task.FromResult(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray());
        });
        var payload = Encoding.UTF8.GetBytes("x");

        using var mine = new MemoryStream();
        await cipher.EncryptAsync(new MemoryStream(payload), mine, Pass);
        count.Should().Be(1);

        // 20 file di altri dispositivi (salt diversi): superano il limite e fanno evictare.
        var foreign = new List<byte[]>();
        for (var n = 0; n < 20; n++)
        {
            var other = new SyncFileCipher((_, _, _, _, _) => Task.FromResult(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray()));
            using var ms = new MemoryStream();
            await other.EncryptAsync(new MemoryStream(payload), ms, Pass);
            foreign.Add(ms.ToArray());
        }
        foreach (var f in foreign)
            await cipher.DecryptAsync(new MemoryStream(f), new MemoryStream(), Pass);
        var afterForeign = count;
        afterForeign.Should().Be(21);

        // il salt di scrittura è sopravvissuto: nessuna nuova derivazione
        await cipher.DecryptAsync(new MemoryStream(mine.ToArray()), new MemoryStream(), Pass);
        count.Should().Be(afterForeign);

        // la prima chiave altrui è stata evictata: si ricalcola
        await cipher.DecryptAsync(new MemoryStream(foreign[0]), new MemoryStream(), Pass);
        count.Should().Be(afterForeign + 1);
    }
}
