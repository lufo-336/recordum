using System.Runtime.Versioning;
using Recordum.Core.Errors;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

[SupportedOSPlatform("windows")]
[Trait("Category", "WindowsOnly")]
public class DpapiKeyProviderTests
{
    [Fact]
    public async Task First_call_generates_and_persists_key()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recordum-key-{Guid.NewGuid():N}.bin");
        try
        {
            var p = new DpapiKeyProvider(path);
            using var km = await p.GetKeyAsync();
            Assert.Equal(32, km.RawKey.Length);
            Assert.True(File.Exists(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Subsequent_calls_return_same_key_bytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recordum-key-{Guid.NewGuid():N}.bin");
        try
        {
            var p = new DpapiKeyProvider(path);
            using var a = await p.GetKeyAsync();
            var copy = a.RawKey.ToArray();
            using var b = await p.GetKeyAsync();
            Assert.Equal(copy, b.RawKey);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Corrupted_file_throws_KeyUnavailable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recordum-key-{Guid.NewGuid():N}.bin");
        try
        {
            await File.WriteAllBytesAsync(path, new byte[] { 0, 1, 2, 3 });
            var p = new DpapiKeyProvider(path);
            await Assert.ThrowsAsync<KeyUnavailableException>(() => p.GetKeyAsync().AsTask());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
