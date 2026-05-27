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
    public async Task ActivateAsync_enables_plugin_and_registers_renderer()
    {
        var registry = new ExtensionPointRegistry();
        var manager = new PluginManager(new InProcessPluginHost(), new AlwaysAllowPermissionBroker(), registry);
        var activator = new ChartEngineActivator(manager, registry);

        await activator.ActivateAsync();

        var extensions = registry.GetExtensions(WellKnownExtensionPoints.ChartRenderer);
        var renderer = Assert.Single(extensions);
        Assert.IsType<ChartEngineRenderer>(renderer);
        Assert.Equal("io.recordum.chart-engine", renderer.PluginId);
    }

    [Fact]
    public async Task ActivateAsync_is_idempotent_on_repeated_calls()
    {
        var registry = new ExtensionPointRegistry();
        var manager = new PluginManager(new InProcessPluginHost(), new AlwaysAllowPermissionBroker(), registry);
        var activator = new ChartEngineActivator(manager, registry);

        await activator.ActivateAsync();
        await activator.ActivateAsync(); // must not throw "already loaded" nor double-register.

        Assert.Single(registry.GetExtensions(WellKnownExtensionPoints.ChartRenderer));
    }
}
