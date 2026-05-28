namespace Recordum.Core.Backup;

public sealed class RestoreException(string code, string? message = null) : Exception(message ?? code)
{
    public string Code { get; } = code;
}
