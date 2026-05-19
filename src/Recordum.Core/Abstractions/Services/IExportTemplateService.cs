using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IExportTemplateService
{
    ValueTask<Guid> CreateAsync(ExportTemplate draft, CancellationToken ct = default);
    ValueTask UpdateAsync(Guid id, Action<ExportTemplate> mutate, CancellationToken ct = default);
    ValueTask DeleteAsync(Guid id, CancellationToken ct = default);
    ValueTask<ExportTemplate?> GetAsync(Guid id, CancellationToken ct = default);
    ValueTask<IReadOnlyList<ExportTemplate>> ListAsync(CancellationToken ct = default);
}
