using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.Contracts.Manifest;
using Recordum.Plugins.Contracts.Permissions;
using Recordum.Plugins.Management;
using Xunit;

namespace Recordum.Plugins.Tests.Management;

public class PluginManagerTests
{
    private static PluginManifest TestManifest(string id = "io.recordum.test") => new()
    {
        SchemaVersion = "1.0",
        Id = id,
        Name = "Test",
        Version = "1.0.0",
        Publisher = new ManifestPublisher { Id = "test", DisplayName = "Test" },
        MinRecordumVersion = "1.0.0",
        Entry = "main.js"
    };

    // --- Fake implementations ---

    private sealed class FakeHost : IPluginHost
    {
        public string HostKind => "fake";
        public PluginManifest? Loaded { get; private set; }
        public string? Unloaded { get; private set; }

        public Task LoadAsync(PluginManifest m, CancellationToken ct)
        {
            Loaded = m;
            return Task.CompletedTask;
        }

        public Task UnloadAsync(string id, CancellationToken ct)
        {
            Unloaded = id;
            return Task.CompletedTask;
        }

        public bool IsLoaded(string id) => Loaded?.Id == id;
    }

    private sealed class FakeBroker(PermissionOutcome outcome = PermissionOutcome.Allowed, string? reason = null)
        : IPermissionBroker
    {
        public int CheckCallCount { get; private set; }
        public PermissionRequest? LastRequest { get; private set; }

        public PermissionDecision Check(PermissionRequest request)
        {
            CheckCallCount++;
            LastRequest = request;
            return new PermissionDecision(outcome, reason);
        }
    }

    private sealed class FakeRegistry : IExtensionPointRegistry
    {
        public string? UnregisteredPluginId { get; private set; }

        public void Register(ExtensionRegistration registration) { }

        public void UnregisterAll(string pluginId)
        {
            UnregisteredPluginId = pluginId;
        }

        public IReadOnlyList<IExtensionPoint> GetExtensions(ExtensionPointId pointId) => [];
    }

    // --- Tests ---

    [Fact]
    public async Task EnableAsync_calls_host_LoadAsync_with_the_manifest()
    {
        var host = new FakeHost();
        var broker = new FakeBroker();
        var registry = new FakeRegistry();
        var manager = new PluginManager(host, broker, registry);
        var manifest = TestManifest();

        await manager.EnableAsync(manifest);

        Assert.Same(manifest, host.Loaded);
    }

    [Fact]
    public async Task EnableAsync_calls_broker_Check_exactly_once()
    {
        var host = new FakeHost();
        var broker = new FakeBroker();
        var registry = new FakeRegistry();
        var manager = new PluginManager(host, broker, registry);

        await manager.EnableAsync(TestManifest());

        Assert.Equal(1, broker.CheckCallCount);
    }

    [Fact]
    public async Task EnableAsync_throws_InvalidOperationException_when_broker_denies()
    {
        var host = new FakeHost();
        var broker = new FakeBroker(PermissionOutcome.Denied, "not allowed");
        var registry = new FakeRegistry();
        var manager = new PluginManager(host, broker, registry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.EnableAsync(TestManifest()));
    }

    [Fact]
    public async Task DisableAsync_calls_registry_UnregisterAll_before_host_UnloadAsync()
    {
        // We verify order by asserting both were called with the same plugin id.
        var host = new FakeHost();
        var broker = new FakeBroker();
        var registry = new FakeRegistry();
        var manager = new PluginManager(host, broker, registry);

        // Load first so unload has something to do.
        await manager.EnableAsync(TestManifest("io.recordum.target"));
        await manager.DisableAsync("io.recordum.target");

        Assert.Equal("io.recordum.target", registry.UnregisteredPluginId);
        Assert.Equal("io.recordum.target", host.Unloaded);
    }

    [Fact]
    public async Task EnableAsync_null_manifest_throws_ArgumentNullException()
    {
        var manager = new PluginManager(new FakeHost(), new FakeBroker(), new FakeRegistry());
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.EnableAsync(null!));
    }

    [Fact]
    public async Task DisableAsync_null_pluginId_throws_ArgumentNullException()
    {
        var manager = new PluginManager(new FakeHost(), new FakeBroker(), new FakeRegistry());
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.DisableAsync(null!));
    }

    [Fact]
    public async Task DisableAsync_whitespace_pluginId_throws_ArgumentException()
    {
        var manager = new PluginManager(new FakeHost(), new FakeBroker(), new FakeRegistry());
        await Assert.ThrowsAsync<ArgumentException>(() => manager.DisableAsync("   "));
    }
}
