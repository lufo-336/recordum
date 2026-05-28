namespace Recordum.Core.Backup;

public sealed record RestoreResult(IReadOnlyList<string> UnavailablePlugins);
