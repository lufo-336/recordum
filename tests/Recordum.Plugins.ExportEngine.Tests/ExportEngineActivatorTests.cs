using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Plugins.Hosting;
using Recordum.Plugins.Management;
using Recordum.Plugins.Permissions;

namespace Recordum.Plugins.ExportEngine.Tests;

public sealed class ExportEngineActivatorTests
{
    private static (ExtensionPointRegistry, ExportEngineActivator) Make()
    {
        var registry = new ExtensionPointRegistry();
        var manager = new PluginManager(new InProcessPluginHost(), new AlwaysAllowPermissionBroker(), registry);
        return (registry, new ExportEngineActivator(manager, registry));
    }

    [Fact]
    public async Task ActivateAsync_registers_five_formatters()
    {
        var (registry, activator) = Make();
        await activator.ActivateAsync();
        var extensions = registry.GetExtensions(WellKnownExtensionPoints.ExportFormat);
        Assert.Equal(5, extensions.Count);
        Assert.All(extensions, e => Assert.Equal("io.recordum.export-engine", e.PluginId));
    }

    [Fact]
    public async Task ActivateAsync_is_idempotent()
    {
        var (registry, activator) = Make();
        await activator.ActivateAsync();
        await activator.ActivateAsync();
        Assert.Equal(5, registry.GetExtensions(WellKnownExtensionPoints.ExportFormat).Count);
    }
}
