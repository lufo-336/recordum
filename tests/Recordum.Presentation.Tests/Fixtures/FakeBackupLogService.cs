using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeBackupLogService : IBackupLogService
{
    public List<BackupLog> Rows { get; } = [];
    public List<Guid> Deleted { get; } = [];
    public Task RecordAsync(BackupLog row, CancellationToken ct = default) { Rows.Add(row); return Task.CompletedTask; }
    public Task<IReadOnlyList<BackupLog>> ListAsync(int take = 50, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BackupLog>>(Rows.OrderByDescending(x => x.CreatedAt).Take(take).ToList());
    public Task<BackupLog?> GetAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Rows.FirstOrDefault(x => x.Id == id));
    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Rows.RemoveAll(x => x.Id == id);
        Deleted.Add(id);
        return Task.CompletedTask;
    }
}
