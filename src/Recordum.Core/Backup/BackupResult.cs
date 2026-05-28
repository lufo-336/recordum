namespace Recordum.Core.Backup;

public sealed record BackupResult(Guid Id, string Path, long SizeBytes, BackupManifest Manifest);
