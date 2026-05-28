using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IBackupLogService
{
    Task RecordAsync(BackupLog row, CancellationToken ct = default);
    Task<IReadOnlyList<BackupLog>> ListAsync(int take = 50, CancellationToken ct = default);
    Task<BackupLog?> GetAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
