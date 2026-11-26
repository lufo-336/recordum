using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Plugins.Hosting;
using Recordum.Plugins.Management;
using Recordum.Plugins.Permissions;
using Xunit;

namespace Recordum.Plugins.ChartEngine.Tests;

public sealed class ChartEngineActivatorTests
{
    [Fact]
    public async Task ActivateAsync_enables_plugin_without_registering_a_renderer()
    {
        var registry = new ExtensionPointRegistry();
        var manager = new PluginManager(new InProcessPluginHost(), new AlwaysAllowPermissionBroker(), registry);
        var activator = new ChartEngineActivator(manager);

        await activator.ActivateAsync();

        // Il renderer WPF e' sparito con la WPF: Avalonia non ne ha ancora uno.
        Assert.Empty(registry.GetExtensions(WellKnownExtensionPoints.ChartRenderer));
        Assert.Equal("io.recordum.chart-engine", Assert.Single(manager.ListActivatedManifests()).Id);
    }

    [Fact]
    public async Task ActivateAsync_is_idempotent_on_repeated_calls()
    {
        var registry = new ExtensionPointRegistry();
        var manager = new PluginManager(new InProcessPluginHost(), new AlwaysAllowPermissionBroker(), registry);
        var activator = new ChartEngineActivator(manager);

        await activator.ActivateAsync();
        await activator.ActivateAsync(); // must not throw "already loaded".

        Assert.Single(manager.ListActivatedManifests());
    }
}
