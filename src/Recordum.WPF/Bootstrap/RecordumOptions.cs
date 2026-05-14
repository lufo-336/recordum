using System.IO;

namespace Recordum.WPF.Bootstrap;

public sealed class RecordumOptions
{
    public required string DatabasePath { get; init; }

    public static RecordumOptions Default()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Recordum");
        Directory.CreateDirectory(dir);
        return new RecordumOptions { DatabasePath = Path.Combine(dir, "recordum.db") };
    }

    public string ConnectionString => $"Data Source={DatabasePath}";
}
