using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Recordum.UI.Diagnostics;

/// <summary>
/// Best-effort global unhandled-exception logger. Appends to
/// <c>%LocalAppData%/Recordum/logs/recordum.log</c> from three .NET sinks:
/// UI thread (<see cref="Dispatcher.UnhandledException"/>),
/// non-UI thread (<see cref="AppDomain.UnhandledException"/>),
/// and unobserved <see cref="Task"/> exceptions.
/// </summary>
/// <remarks>
/// Defensive by design — never throws, swallows IO errors. If <c>WireUp</c>
/// can't initialize (no perms, disk full, …), logging is silently disabled
/// and the app boots normally.
/// </remarks>
public static class CrashLogger
{
    private static readonly Lock Gate = new();
    private static string? _logPath;
    private static bool _wired;

    public static string? LogPath => _logPath;

    /// <summary>Un errore di dominio sul thread dell'interfaccia è stato gestito: l'app continua, e chi
    /// ascolta avvisa l'utente (F2a-11).</summary>
    public static event Action<Exception>? RecoverableError;

    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <param name="logDirectory">Solo per i test: sostituisce <c>%LocalAppData%/Recordum/logs</c>, cosi' un
    /// test non scrive nella cartella vera dell'utente.</param>
    public static void WireUp(string? logDirectory = null)
    {
        if (_wired) return;
        _wired = true;
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = logDirectory ?? Path.Combine(appData, "Recordum", "logs");
            _logPath = Path.Combine(dir, "recordum.log");
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(dir);   // %LocalAppData% e' gia' privata per ACL
            }
            else
            {
                // Lo stack trace puo' portare nomi di abitudini e percorsi: col umask di default cartella e file
                // nascerebbero 0755/0644, leggibili da ogni utente locale. Una cartella dei log o un file
                // preesistenti piu' larghi vengono ristretti.
                // .NET applica la mode alla sola cartella foglia: il padre ("Recordum") si crea a parte, se manca.
                if (Path.GetDirectoryName(dir) is { Length: > 0 } parent) Directory.CreateDirectory(parent, OwnerDirectory);
                Directory.CreateDirectory(dir, OwnerDirectory);
                File.SetUnixFileMode(dir, OwnerDirectory);
                if (File.Exists(_logPath)) File.SetUnixFileMode(_logPath, OwnerFile);
            }

            WriteHeader();

            Dispatcher.UIThread.UnhandledException += OnDispatcher;
            AppDomain.CurrentDomain.UnhandledException += OnDomain;
            TaskScheduler.UnobservedTaskException += OnUnobservedTask;
        }
        catch
        {
            // Logger must never crash the host. Silently disable.
            _logPath = null;
        }
    }

    private static void WriteHeader()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "(unknown)";
        var os = RuntimeInformation.OSDescription;
        var runtime = RuntimeInformation.FrameworkDescription;
        Append($"=== Session start {Now()} version={version} runtime={runtime} os={os} ===");
    }

    private static void OnDispatcher(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (IsRecoverable(e.Exception))
        {
            // F2a-11: un errore di dominio (timer non trovato, abitudine sparita, registrazione non valida
            // per il tipo) è un'operazione rifiutata, non un'app rotta: si scrive, si avvisa, si continua.
            Log("Dispatcher.UnhandledException (errore di dominio, gestito: l'app continua)", e.Exception);
            e.Handled = true;
            RecoverableError?.Invoke(e.Exception);
            return;
        }
        Log("Dispatcher.UnhandledException (thread dell'interfaccia)", e.Exception);
        // Un errore inatteso: e.Handled resta false e l'app si chiude, come prima.
    }

    /// <summary>Vero per le sole eccezioni di dominio (anche avvolte in un'AggregateException).</summary>
    internal static bool IsRecoverable(Exception ex) => ex switch
    {
        Recordum.Core.Errors.RecordumDomainException => true,
        AggregateException agg => agg.Flatten().InnerExceptions.All(IsRecoverable),
        _ => false,
    };

    private static void OnDomain(object? sender, UnhandledExceptionEventArgs e)
    {
        Log(
            $"AppDomain.UnhandledException (terminating={e.IsTerminating})",
            e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log("TaskScheduler.UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    public static void Info(string line)
    {
        if (_logPath is null) return;
        try
        {
            Append($"[{Now()}] {line}");
        }
        catch
        {
            // Swallow — logger must never throw.
        }
    }

    public static void Log(string source, Exception? ex)
    {
        if (_logPath is null) return;
        try
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(Now()).Append("] ").AppendLine(source);
            if (ex is null)
            {
                sb.AppendLine("(no exception object)");
            }
            else
            {
                AppendException(sb, ex, depth: 0);
            }
            Append(sb.ToString());
        }
        catch
        {
            // Swallow — logger must never throw.
        }
    }

    private static void AppendException(StringBuilder sb, Exception ex, int depth)
    {
        var pad = new string(' ', depth * 2);
        sb.Append(pad).Append(ex.GetType().FullName).Append(": ").AppendLine(ex.Message);
        if (ex.StackTrace is { } st)
        {
            foreach (var raw in st.Split('\n'))
            {
                sb.Append(pad).Append("  ").AppendLine(raw.TrimEnd('\r'));
            }
        }
        if (ex is AggregateException agg)
        {
            foreach (var inner in agg.InnerExceptions)
            {
                sb.Append(pad).AppendLine("--- aggregate inner ---");
                AppendException(sb, inner, depth + 1);
            }
        }
        else if (ex.InnerException is { } inner)
        {
            sb.Append(pad).AppendLine("--- inner ---");
            AppendException(sb, inner, depth + 1);
        }
    }

    private static void Append(string text)
    {
        var path = _logPath;
        if (path is null) return;
        lock (Gate)
        {
            try
            {
                // UnixCreateMode vale solo alla creazione, e Append crea il file alla prima scrittura (e se
                // sparisce a meta' sessione): e' qui, non in WireUp, che il file nasce 0600.
                var options = new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write, Share = FileShare.Read };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = OwnerFile;
                using var stream = new FileStream(path, options);
                stream.Write(new UTF8Encoding(false).GetBytes(text + Environment.NewLine));
            }
            catch (IOException)
            {
                // disk full / file locked — best effort
            }
            catch (UnauthorizedAccessException)
            {
                // perms changed mid-session — disable rather than retry
                _logPath = null;
            }
        }
    }

    // Eccezione alla regola "il tempo passa da IClock": e' un logger statico che parte prima della DI, quindi senza
    // IClock, e usa l'istante solo come timestamp di riga.
    private static string Now() =>
        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    [Conditional("DEBUG")]
    public static void ResetForTests()
    {
        // Anche i gestori: senza, un secondo WireUp nello stesso processo di test li aggancia due volte
        // (stesso metodo statico, delegate uguale) e ogni guasto finisce nel log due volte.
        Dispatcher.UIThread.UnhandledException -= OnDispatcher;
        AppDomain.CurrentDomain.UnhandledException -= OnDomain;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTask;
        _logPath = null;
        _wired = false;
        RecoverableError = null;
    }
}
