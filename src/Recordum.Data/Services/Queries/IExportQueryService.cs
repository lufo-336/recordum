using Recordum.Plugins.Contracts.Export;

namespace Recordum.Data.Services.Queries;

public interface IExportQueryService
{
    ValueTask<ExportModel> BuildAsync(ExportRequest request, CancellationToken ct = default);
}
