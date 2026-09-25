using FluentAssertions;
using Xunit;

namespace Recordum.Data.Tests.Architecture;

/// <summary>Backup e restore scrivono dump del database in chiaro mentre lavorano: devono farlo nella cartella
/// temporanea della cartella dati (<c>IBackupStoragePaths.TempDirectory</c>), mai nella temp di sistema — su Linux
/// <c>/tmp</c> è leggibile da ogni utente locale (RV-4, Passo 0). Questo test fallisce se ricompare un
/// <c>Path.GetTempPath()</c> fuori whitelist in <c>src/</c>.</summary>
public class SystemTempGuardTests
{
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Recordum.Presentation/Bootstrap/BackupSchedulerHost.cs"] =
            "solo per distruggere i resti delle versioni precedenti, che scrivevano lì",
    };

    [Fact]
    public void Nothing_in_src_writes_to_the_system_temp_directory()
    {
        var srcRoot = Path.Combine(FindRepoRoot(), "src");
        var sep = Path.DirectorySeparatorChar;
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{sep}obj{sep}", StringComparison.Ordinal)
                || file.Contains($"{sep}bin{sep}", StringComparison.Ordinal)) continue;
            var relative = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
            if (Allowed.ContainsKey(relative)) continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                if (lines[i].Contains("GetTempPath", StringComparison.Ordinal))
                    offenders.Add($"{relative}:{i + 1}  {lines[i].Trim()}");
        }

        offenders.Should().BeEmpty("i file in chiaro vanno in IBackupStoragePaths.TempDirectory, dentro la cartella dati");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Recordum.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Recordum.slnx non trovato risalendo da " + AppContext.BaseDirectory);
    }
}
