namespace Recordum.Core.Backup;

public interface IRestoreService
{
    Task<RestorePreview> PreviewAsync(string sourcePath, string passphrase, CancellationToken ct = default);
    Task<RestoreResult> RestoreAsync(string sourcePath, string passphrase, RestoreOptions options, CancellationToken ct = default);
}
