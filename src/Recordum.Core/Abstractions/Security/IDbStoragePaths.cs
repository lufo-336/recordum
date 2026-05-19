namespace Recordum.Core.Abstractions.Security;

public interface IDbStoragePaths
{
    string DatabasePath { get; }
    string KeyFilePath { get; }
    string SaltFilePath { get; }
}
