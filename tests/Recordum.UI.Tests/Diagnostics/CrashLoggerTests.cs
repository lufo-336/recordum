using System.Runtime.CompilerServices;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.UI.Bootstrap;
using Recordum.UI.Diagnostics;
using Xunit;

namespace Recordum.UI.Tests.Diagnostics;

public class CrashLoggerTests
{
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    [AvaloniaFact]
    public void The_log_and_its_folders_are_private_to_the_user_on_unix()
    {
        if (OperatingSystem.IsWindows()) return;   // ACL, non modi Unix

        // Mai la cartella vera dell'utente: una radice casuale, con DUE livelli da creare.
        var root = Path.Combine(Path.GetTempPath(), $"recordum-crashlog-{Guid.NewGuid():N}");
        var logs = Path.Combine(root, "Recordum", "logs");
        try
        {
            CrashLogger.ResetForTests();
            CrashLogger.WireUp(logs);

            CrashLogger.LogPath.Should().Be(Path.Combine(logs, "recordum.log"));
            File.GetUnixFileMode(logs).Should().Be(OwnerDirectory, "la cartella dei log e' 0700");
            File.GetUnixFileMode(Path.Combine(root, "Recordum")).Should().Be(OwnerDirectory,
                "anche la cartella padre creata ora e' 0700");
            File.GetUnixFileMode(CrashLogger.LogPath!).Should().Be(OwnerFile,
                "il file nasce 0600 con la prima scrittura (l'intestazione di sessione)");

            // Il file sparisce a meta' sessione: la scrittura successiva lo ricrea, ancora 0600.
            File.Delete(CrashLogger.LogPath!);
            CrashLogger.Log("ricreato", new InvalidOperationException("motivo"));
            File.ReadAllText(CrashLogger.LogPath!).Should().Contain("ricreato");
            File.GetUnixFileMode(CrashLogger.LogPath!).Should().Be(OwnerFile);
        }
        finally
        {
            CrashLogger.ResetForTests();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [AvaloniaFact]
    public void Wiring_up_twice_in_one_process_does_not_log_a_failure_twice()
    {
        var root = Path.Combine(Path.GetTempPath(), $"recordum-crashlog-{Guid.NewGuid():N}");
        try
        {
            CrashLogger.ResetForTests();
            CrashLogger.WireUp(Path.Combine(root, "a"));
            CrashLogger.ResetForTests();   // deve staccare i gestori del primo giro
            CrashLogger.WireUp(Path.Combine(root, "b"));

            var marker = $"orfano-{Guid.NewGuid():N}";
            FaultAndAbandonATask(marker);
            // Il finalizzatore del Task parte quando vuole il GC: si insiste finche' il guasto compare.
            for (var i = 0; i < 50 && !File.ReadAllText(CrashLogger.LogPath!).Contains(marker, StringComparison.Ordinal); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(20);
            }

            var log = File.ReadAllText(CrashLogger.LogPath!);
            log.Should().Contain(marker, "il GC non ha finalizzato il Task nei tentativi: non e' un difetto dell'aggancio");
            // Il marker compare due volte in una voce sola (AggregateException + interna): si contano le voci.
            log.Split("] TaskScheduler.UnobservedTaskException").Count(entry => entry.Contains(marker, StringComparison.Ordinal))
                .Should().Be(1, "un guasto non osservato compare una volta nel log: con i gestori agganciati due volte, due");
        }
        finally
        {
            CrashLogger.ResetForTests();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // Non inlined: il Task deve essere irraggiungibile quando parte il GC, o il finalizzatore non lo vede.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FaultAndAbandonATask(string marker)
    {
        var task = Task.Run(() => throw new InvalidOperationException(marker));
        // Aspetta senza osservare l'eccezione (Wait la osserverebbe): il Task deve restare "non osservato".
        ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
    }

    [AvaloniaFact]
    public async Task A_failed_unlock_leaves_its_cause_in_the_log_and_never_the_passphrase()
    {
        var root = Path.Combine(Path.GetTempPath(), $"recordum-unlocklog-{Guid.NewGuid():N}");
        const string passphrase = "una-passphrase-lunga-e-segreta";
        try
        {
            CrashLogger.ResetForTests();
            CrashLogger.WireUp(Path.Combine(root, "logs"));
            var options = RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, Path.Combine(root, "data")], _ => null);
            // Un salt che esiste gia' senza un database: CreateAsync non lo tronca mai (CreateNew) e
            // l'IOException diventa "non riuscito". E' il percorso di guasto che l'utente non vede.
            File.WriteAllBytes(options.SaltFilePath, new byte[16]);

            var service = AppComposition.CreateUnlockService(options);
            var result = await service.UnlockAsync(DbUnlockState.NeedsCreation, passphrase);

            result.Outcome.Should().Be(UnlockOutcome.Failed);
            var log = File.ReadAllText(CrashLogger.LogPath!);
            log.Should().Contain("Unlock failed in state NeedsCreation")
                .And.Contain("IOException", "la causa vera, non solo il fatto che e' fallito");
            log.Should().NotContain(passphrase);
        }
        finally
        {
            CrashLogger.ResetForTests();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
