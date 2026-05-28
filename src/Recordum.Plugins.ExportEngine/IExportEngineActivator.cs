namespace Recordum.Plugins.ExportEngine;

public interface IExportEngineActivator
{
    Task ActivateAsync(CancellationToken ct = default);
}
