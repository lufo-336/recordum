using FluentAssertions;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.Contracts.Manifest;
using Recordum.Plugins.Contracts.Permissions;
using Recordum.Plugins.Hosting;
using Recordum.Plugins.Management;
using Recordum.Plugins.Permissions;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Plugins.Routing;
using Recordum.Plugins.Events;
using Xunit;

namespace Recordum.Plugins.Tests.Management;

public class PluginManagerActivationTests
{
    private static IPluginManager NewManager()
    {
        IExtensionPointRegistry registry = new ExtensionPointRegistry();
        IEventBus bus = new EventBus();
        IApiRouter router = new ApiRouter();
        IPluginHost host = new InProcessPluginHost();
        IPermissionBroker broker = new AlwaysAllowPermissionBroker();
        return new PluginManager(host, broker, registry);
    }

    private static PluginManifest Manifest(string id, string version) =>
        new()
        {
            SchemaVersion = "1.0",
            Id = id,
            Name = id,
            Version = version,
            Publisher = new() { Id = "test", DisplayName = "Test" },
            MinRecordumVersion = "3.0.0",
            Entry = "plugin.js",
            Permissions = new(),
            ExtensionPoints = [],
        };

    [Fact]
    public async Task ListActivatedManifests_returns_enabled_after_EnableAsync()
    {
        var mgr = NewManager();
        await mgr.EnableAsync(Manifest("io.test.alpha", "1.0.0"));
        await mgr.EnableAsync(Manifest("io.test.beta", "2.1.0"));

        var snaps = mgr.ListActivatedManifests();

        snaps.Should().HaveCount(2);
        snaps.Should().Contain(m => m.Id == "io.test.alpha" && m.Version == "1.0.0");
        snaps.Should().Contain(m => m.Id == "io.test.beta" && m.Version == "2.1.0");
    }

    [Fact]
    public async Task DisableAsync_removes_from_activated_list()
    {
        var mgr = NewManager();
        await mgr.EnableAsync(Manifest("io.test.alpha", "1.0.0"));
        await mgr.DisableAsync("io.test.alpha");

        mgr.ListActivatedManifests().Should().BeEmpty();
    }
}
