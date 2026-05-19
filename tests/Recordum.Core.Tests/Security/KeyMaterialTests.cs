using Recordum.Core.Abstractions.Security;
using Xunit;

namespace Recordum.Core.Tests.Security;

public class KeyMaterialTests
{
    [Fact]
    public void Dispose_zeroes_buffer()
    {
        var bytes = new byte[32];
        new Random(0).NextBytes(bytes);
        var km = new KeyMaterial(bytes);
        Assert.Contains(bytes, b => b != 0);
        km.Dispose();
        Assert.All(bytes, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Rejects_wrong_length()
    {
        Assert.Throws<ArgumentException>(() => new KeyMaterial(new byte[16]));
    }
}
