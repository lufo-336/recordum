using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeExportJobService : IExportJobService
{
    public List<ExportJob> Recorded { get; } = [];

    public ValueTask<Guid> RecordAsync(ExportJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Id == Guid.Empty) job.Id = Guid.NewGuid();
        Recorded.Add(job);
        return ValueTask.FromResult(job.Id);
    }

    public ValueTask<IReadOnlyList<ExportJob>> ListAsync(CancellationToken ct = default)
        => ValueTask.FromResult<IReadOnlyList<ExportJob>>(Recorded);
}
