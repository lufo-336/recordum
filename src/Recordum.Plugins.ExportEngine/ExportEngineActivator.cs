using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.ExportEngine.Formatters;

namespace Recordum.Plugins.ExportEngine;

public sealed class ExportEngineActivator(IPluginManager manager, IExtensionPointRegistry registry) : IExportEngineActivator
{
    public async Task ActivateAsync(CancellationToken ct = default)
    {
        var already = registry.GetExtensions(WellKnownExtensionPoints.ExportFormat);
        foreach (var e in already)
        {
            if (e.PluginId == ExportEngineManifest.PluginId) return;
        }

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var manifest = ExportEngineManifest.Create();
        await manager.EnableAsync(manifest, ct).ConfigureAwait(false);

        ExportFormatterBase[] formatters =
        [
            new JsonExportFormatter(),
            new CsvExportFormatter(),
            new MarkdownExportFormatter(),
            new HtmlExportFormatter(),
            new PdfExportFormatter(),
        ];
        foreach (var f in formatters)
            registry.Register(new ExtensionRegistration(manifest.Id, WellKnownExtensionPoints.ExportFormat, f));
    }
}
