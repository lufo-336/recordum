using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

public sealed class DataDirectoryLockTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "recordum-lock-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* best effort */ }
    }

    [Fact]
    public void A_second_instance_is_refused_while_the_first_holds_the_folder()
    {
        using var first = DataDirectoryLock.TryAcquire(_dir);

        Assert.NotNull(first);
        Assert.Null(DataDirectoryLock.TryAcquire(_dir));
    }

    [Fact]
    public void The_folder_is_free_again_once_the_first_instance_is_gone()
    {
        DataDirectoryLock.TryAcquire(_dir)!.Dispose();

        using var second = DataDirectoryLock.TryAcquire(_dir);

        Assert.NotNull(second);
    }
}
