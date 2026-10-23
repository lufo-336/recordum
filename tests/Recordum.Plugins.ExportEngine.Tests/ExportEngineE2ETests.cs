using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Plugins.Hosting;
using Recordum.Plugins.Management;
using Recordum.Plugins.Permissions;

namespace Recordum.Plugins.ExportEngine.Tests;

public sealed class ExportEngineE2ETests
{
    [Theory]
    [InlineData(ExportFormat.Json)]
    [InlineData(ExportFormat.Csv)]
    [InlineData(ExportFormat.Markdown)]
    [InlineData(ExportFormat.Html)]
    [InlineData(ExportFormat.Pdf)]
    public async Task Each_format_produces_non_empty_bytes(ExportFormat format)
    {
        var registry = new ExtensionPointRegistry();
        var manager = new PluginManager(new InProcessPluginHost(), new AlwaysAllowPermissionBroker(), registry);
        await new ExportEngineActivator(manager, registry).ActivateAsync();

        var formatter = registry.GetExtensions(WellKnownExtensionPoints.ExportFormat)
            .OfType<IExportFormatter>()
            .Single(f => f.CanFormat(format));

        var bytes = formatter.Write(SampleModel.One()).Document;
        Assert.NotEmpty(bytes);
    }
}
