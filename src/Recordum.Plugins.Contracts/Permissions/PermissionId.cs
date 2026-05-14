namespace Recordum.Plugins.Contracts.Permissions;

public readonly record struct PermissionId(string Category, string Action)
{
    public override string ToString() => $"{Category}:{Action}";

    public static PermissionId Parse(string raw)
    {
        var parts = raw.Split(':', 2);
        if (parts.Length != 2) throw new FormatException($"Invalid permission id: '{raw}'");
        return new PermissionId(parts[0], parts[1]);
    }
}
