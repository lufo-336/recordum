using System.IO;
using Recordum.Core.Abstractions.Security;

namespace Recordum.WPF.Bootstrap;

public sealed class RecordumOptions : IDbStoragePaths
{
    public required string DatabasePath { get; init; }
    public required string KeyFilePath { get; init; }
    public required string SaltFilePath { get; init; }

    public static RecordumOptions Default()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Recordum");
        Directory.CreateDirectory(dir);
        return new RecordumOptions
        {
            DatabasePath = Path.Combine(dir, "recordum.db"),
            KeyFilePath = Path.Combine(dir, "db.key"),
            SaltFilePath = Path.Combine(dir, "db.salt"),
        };
    }

    public string ConnectionString => $"Data Source={DatabasePath}";
}
