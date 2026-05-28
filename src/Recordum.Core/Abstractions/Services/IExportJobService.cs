using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IExportJobService
{
    ValueTask<Guid> RecordAsync(ExportJob job, CancellationToken ct = default);
    ValueTask<IReadOnlyList<ExportJob>> ListAsync(CancellationToken ct = default);
}
