using System.Text;
using FluentAssertions;
using Recordum.Data.Sync;

namespace Recordum.Data.Tests.Sync;

public sealed class FolderSyncFileStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"recordum-sync-store-{Guid.NewGuid():N}");

    public FolderSyncFileStoreTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public async Task ListsPeerRcsyncFilesExcludingOwnAndTempFiles()
    {
        const string ownName = "recordum-Fedora-11111111.rcsync";
        var store = new FolderSyncFileStore(_folder, ownName);

        await File.WriteAllTextAsync(Path.Combine(_folder, ownName), "mine");
        await File.WriteAllTextAsync(Path.Combine(_folder, ownName + ".tmp"), "partial");
        await File.WriteAllTextAsync(Path.Combine(_folder, "notes.txt"), "unrelated");
        await File.WriteAllTextAsync(Path.Combine(_folder, "recordum-Laptop-22222222.rcsync"), "peer-b");
        await File.WriteAllTextAsync(Path.Combine(_folder, "recordum-Phone-33333333.rcsync"), "peer-c");

        var peers = await store.ListPeerFilesAsync(CancellationToken.None);

        peers.Select(p => p.DisplayName).Should().Equal(
            "recordum-Laptop-22222222.rcsync",
            "recordum-Phone-33333333.rcsync");
        peers.Should().AllSatisfy(p => p.LastModified.Should().NotBeNull());

        await using var stream = await store.OpenReadAsync(peers[0], CancellationToken.None);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        (await reader.ReadToEndAsync()).Should().Be("peer-b");
    }

    [Fact]
    public async Task CrashDuringWriteKeepsPreviousOwnFile()
    {
        const string ownName = "recordum-Fedora-11111111.rcsync";
        var store = new FolderSyncFileStore(_folder, ownName);

        await store.WriteOwnAsync(
            s => s.WriteAsync(Encoding.UTF8.GetBytes("original-valid-content")).AsTask(),
            CancellationToken.None);

        var crash = () => store.WriteOwnAsync(
            async s =>
            {
                await s.WriteAsync(Encoding.UTF8.GetBytes("half-written-corrupted"));
                throw new IOException("simulated disk crash mid-write");
            },
            CancellationToken.None);

        await crash.Should().ThrowAsync<IOException>().WithMessage("simulated disk crash mid-write");

        File.Exists(Path.Combine(_folder, ownName)).Should().BeTrue();
        File.Exists(Path.Combine(_folder, ownName) + ".tmp").Should().BeFalse();
        (await File.ReadAllTextAsync(Path.Combine(_folder, ownName))).Should().Be("original-valid-content");
    }
}
