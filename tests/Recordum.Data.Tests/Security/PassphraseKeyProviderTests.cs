using Recordum.Core.Errors;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

public class PassphraseKeyProviderTests
{
    [Fact]
    public async Task Derives_deterministic_key_from_passphrase_and_salt()
    {
        var saltPath = Path.Combine(Path.GetTempPath(), $"recordum-salt-{Guid.NewGuid():N}.bin");
        try
        {
            await File.WriteAllBytesAsync(saltPath, new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
            var p = new PassphraseKeyProvider("correct horse battery staple", saltPath);
            using var a = await p.GetKeyAsync();
            var copy = a.RawKey.ToArray();
            using var b = await p.GetKeyAsync();
            Assert.Equal(copy, b.RawKey);
        }
        finally { if (File.Exists(saltPath)) File.Delete(saltPath); }
    }

    [Fact]
    public async Task Missing_salt_throws_KeyUnavailable()
    {
        var p = new PassphraseKeyProvider("hunter2hunter2", Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.bin"));
        await Assert.ThrowsAsync<KeyUnavailableException>(() => p.GetKeyAsync().AsTask());
    }

    [Fact]
    public void Rejects_short_passphrase()
    {
        Assert.Throws<ArgumentException>(() => new PassphraseKeyProvider("short", "irrelevant"));
    }
}
