using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeExportTemplateService : IExportTemplateService
{
    public List<ExportTemplate> Store { get; } =
    [
        new ExportTemplate { Id = Guid.NewGuid(), Name = "Full report", LayoutJson = new ExportLayout().ToJson() },
        new ExportTemplate { Id = Guid.NewGuid(), Name = "Data only", LayoutJson = new ExportLayout(Heatmap: false, Notes: false).ToJson() },
    ];

    public ValueTask<Guid> CreateAsync(ExportTemplate draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Store.Add(draft);
        return ValueTask.FromResult(draft.Id);
    }
    public ValueTask UpdateAsync(Guid id, Action<ExportTemplate> mutate, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask<ExportTemplate?> GetAsync(Guid id, CancellationToken ct = default) => ValueTask.FromResult(Store.FirstOrDefault(t => t.Id == id));
    public ValueTask<IReadOnlyList<ExportTemplate>> ListAsync(CancellationToken ct = default) => ValueTask.FromResult<IReadOnlyList<ExportTemplate>>(Store);
}
