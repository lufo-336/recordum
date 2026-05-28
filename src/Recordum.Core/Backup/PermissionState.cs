namespace Recordum.Core.Backup;

public sealed record PermissionState(IReadOnlyDictionary<string, IReadOnlyList<string>> GrantsByPlugin);
