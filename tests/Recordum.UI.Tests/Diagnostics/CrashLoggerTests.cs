using System.Runtime.CompilerServices;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Errors;
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
            ReadLog().Should().Contain("ricreato");
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
            for (var i = 0; i < 50 && !ReadLog().Contains(marker, StringComparison.Ordinal); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(20);
            }

            var log = ReadLog();
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

    // F2a-11: un errore di dominio sul thread dell'interfaccia (un timer non trovato, un'abitudine
    // sparita) non chiude più l'app: va nel log, l'app avvisa e continua. Un errore inatteso sì.
    [AvaloniaFact]
    public void A_domain_error_on_the_ui_thread_is_logged_and_reported_and_the_app_goes_on()
    {
        var root = Path.Combine(Path.GetTempPath(), $"recordum-crashlog-{Guid.NewGuid():N}");
        var reported = new List<Exception>();
        void OnRecoverable(Exception ex) => reported.Add(ex);
        try
        {
            CrashLogger.ResetForTests();
            CrashLogger.WireUp(root);
            CrashLogger.RecoverableError += OnRecoverable;
            var id = Guid.NewGuid();

            Avalonia.Threading.Dispatcher.UIThread.Post(() => throw new ActiveTimerNotFoundException(id));
            var run = () => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            run.Should().NotThrow("l'errore di dominio è gestito, il ciclo dell'interfaccia continua");
            reported.Should().ContainSingle().Which.Should().BeOfType<ActiveTimerNotFoundException>();
            ReadLog().Should().Contain(id.ToString());
        }
        finally
        {
            CrashLogger.RecoverableError -= OnRecoverable;
            CrashLogger.ResetForTests();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [AvaloniaFact]
    public void An_unexpected_error_on_the_ui_thread_is_not_swallowed()
    {
        var root = Path.Combine(Path.GetTempPath(), $"recordum-crashlog-{Guid.NewGuid():N}");
        var reported = new List<Exception>();
        void OnRecoverable(Exception ex) => reported.Add(ex);
        try
        {
            CrashLogger.ResetForTests();
            CrashLogger.WireUp(root);
            CrashLogger.RecoverableError += OnRecoverable;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => throw new InvalidOperationException("un bug vero"));
            var run = () => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            run.Should().Throw<InvalidOperationException>("un errore inatteso continua a chiudere l'app");
            reported.Should().BeEmpty();
        }
        finally
        {
            CrashLogger.RecoverableError -= OnRecoverable;
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

            var service = AppComposition.CreateUnlockService(options, new Recordum.Data.Security.NullSecretStore());
            var result = await service.UnlockAsync(DbUnlockState.NeedsCreation, passphrase, remember: RememberChoice.LeaveAsIs);

            result.Outcome.Should().Be(UnlockOutcome.Failed);
            var log = ReadLog();
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

    // Windows: il logger tiene il file aperto in scrittura; si legge condividendo (CLAUDE.md §7).
    private static string ReadLog()
    {
        using var fs = new FileStream(CrashLogger.LogPath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd();
    }
}
