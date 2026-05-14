using Recordum.Plugins.Contracts.Manifest;
using Recordum.Plugins.Hosting;
using Xunit;

namespace Recordum.Plugins.Tests.Hosting;

public class InProcessPluginHostTests
{
    private static PluginManifest TestManifest(string id = "io.recordum.test") => new()
    {
        SchemaVersion = "1.0",
        Id = id,
        Name = "Test Plugin",
        Version = "1.0.0",
        Publisher = new ManifestPublisher { Id = "test", DisplayName = "Test Publisher" },
        MinRecordumVersion = "1.0.0",
        Entry = "main.js"
    };

    [Fact]
    public void HostKind_is_in_process()
    {
        var host = new InProcessPluginHost();
        Assert.Equal("in-process", host.HostKind);
    }

    [Fact]
    public async Task LoadAsync_then_IsLoaded_returns_true()
    {
        var host = new InProcessPluginHost();
        var manifest = TestManifest();

        await host.LoadAsync(manifest);

        Assert.True(host.IsLoaded(manifest.Id));
    }

    [Fact]
    public async Task LoadAsync_same_plugin_twice_throws_InvalidOperationException()
    {
        var host = new InProcessPluginHost();
        var manifest = TestManifest();

        await host.LoadAsync(manifest);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.LoadAsync(manifest));
    }

    [Fact]
    public async Task UnloadAsync_after_load_makes_IsLoaded_return_false()
    {
        var host = new InProcessPluginHost();
        var manifest = TestManifest();

        await host.LoadAsync(manifest);
        await host.UnloadAsync(manifest.Id);

        Assert.False(host.IsLoaded(manifest.Id));
    }

    [Fact]
    public async Task UnloadAsync_of_never_loaded_plugin_does_not_throw()
    {
        var host = new InProcessPluginHost();

        // Should complete without throwing
        await host.UnloadAsync("io.recordum.nonexistent");
    }

    [Fact]
    public async Task LoadAsync_null_manifest_throws_ArgumentNullException()
    {
        var host = new InProcessPluginHost();
        await Assert.ThrowsAsync<ArgumentNullException>(() => host.LoadAsync(null!));
    }

    [Fact]
    public async Task UnloadAsync_null_pluginId_throws_ArgumentNullException()
    {
        var host = new InProcessPluginHost();
        await Assert.ThrowsAsync<ArgumentNullException>(() => host.UnloadAsync(null!));
    }

    [Fact]
    public async Task UnloadAsync_whitespace_pluginId_throws_ArgumentException()
    {
        var host = new InProcessPluginHost();
        await Assert.ThrowsAsync<ArgumentException>(() => host.UnloadAsync("   "));
    }

    [Fact]
    public void IsLoaded_null_pluginId_throws_ArgumentNullException()
    {
        var host = new InProcessPluginHost();
        Assert.Throws<ArgumentNullException>(() => host.IsLoaded(null!));
    }
}
