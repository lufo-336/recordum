using System.IO;
using Recordum.Core.Abstractions.Security;

namespace Recordum.Presentation.Bootstrap;

public sealed class RecordumOptions : IBackupStoragePaths
{
    /// <summary>Riga di comando: <c>--data-dir &lt;cartella&gt;</c>.</summary>
    public const string DataDirectoryArgument = "--data-dir";

    /// <summary>Variabile d'ambiente, per chi non passa argomenti (IDE, script).</summary>
    public const string DataDirectoryVariable = "RECORDUM_DATA_DIR";

    /// <summary>La radice di tutto: database, chiavi, allegati, backup, file temporanei.</summary>
    public required string DataDirectory { get; init; }

    /// <summary><c>true</c> quando i dati sono quelli veri dell'utente (la cartella di default). Gli strumenti di
    /// sviluppo che scrivono dati finti, come <c>--seed-demo</c>, lì si rifiutano (decisione B del Passo 0).</summary>
    public bool IsDefaultLocation { get; init; }

    public required string DatabasePath { get; init; }
    public required string KeyFilePath { get; init; }
    public required string SaltFilePath { get; init; }
    public required string AttachmentsDirectory { get; init; }
    public required string BackupsDirectory { get; init; }
    public required string AutoBackupKeyPath { get; init; }
    public required string TempDirectory { get; init; }

    /// <summary>La cartella dei dati veri di questo utente (<c>LocalApplicationData/Recordum</c>). Solo il percorso:
    /// niente viene creato.</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Recordum");

    public static RecordumOptions Default() => ForDirectory(DefaultDirectory);

    /// <summary><c>--data-dir</c> vince su <c>RECORDUM_DATA_DIR</c>, che vince sul default (decisione B del Passo 0,
    /// 2026-09-25): lo sviluppo lavora su una cartella sua, i dati veri restano dove sono.</summary>
    public static RecordumOptions Resolve(IReadOnlyList<string> args, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);

        string? dir = null;
        for (var i = 0; i < args.Count - 1; i++)
            if (args[i] == DataDirectoryArgument) dir = args[i + 1];
        dir ??= environment(DataDirectoryVariable);

        return string.IsNullOrWhiteSpace(dir) ? Default() : ForDirectory(Path.GetFullPath(dir));
    }

    private static RecordumOptions ForDirectory(string dir)
    {
        var attachments = Path.Combine(dir, "attachments");
        var backups = Path.Combine(dir, "backups");
        var temp = Path.Combine(dir, "tmp");
        foreach (var d in new[] { dir, attachments, backups, temp }) CreatePrivateDirectory(d);

        return new RecordumOptions
        {
            DataDirectory        = dir,
            // Anche un --data-dir che punta alla cartella vera è la cartella vera.
            IsDefaultLocation    = string.Equals(Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar),
                                                 Path.GetFullPath(DefaultDirectory).TrimEnd(Path.DirectorySeparatorChar),
                                                 OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            DatabasePath         = Path.Combine(dir, "recordum.db"),
            KeyFilePath          = Path.Combine(dir, "db.key"),
            SaltFilePath         = Path.Combine(dir, "db.salt"),
            AttachmentsDirectory = attachments,
            BackupsDirectory     = backups,
            AutoBackupKeyPath    = Path.Combine(dir, "autobackup.key"),
            TempDirectory        = temp,
        };
    }

    /// <summary>Su Unix la cartella dei dati è del solo utente (0700, come chiede XDG). Col umask di default
    /// nascerebbe 0755, e le foto allegate — in chiaro per scelta (Onda 1b) — sarebbero leggibili da ogni utente
    /// locale (RV-4). Una cartella che esiste già con permessi più larghi viene ristretta.</summary>
    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);   // %LocalAppData% è già privata per ACL
            return;
        }

        const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        Directory.CreateDirectory(path, OwnerOnly);
        File.SetUnixFileMode(path, OwnerOnly);
    }

    public string ConnectionString => $"Data Source={DatabasePath}";
}
