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

    public static void WireUp()
    {
        if (_wired) return;
        _wired = true;
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "Recordum", "logs");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "recordum.log");

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
        Log("Dispatcher.UnhandledException (thread dell'interfaccia)", e.Exception);
        // Leave e.Handled = false → existing shutdown behaviour preserved.
    }

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
                File.AppendAllText(path, text + Environment.NewLine);
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

    private static string Now() =>
        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    [Conditional("DEBUG")]
    public static void ResetForTests()
    {
        _logPath = null;
        _wired = false;
    }
}
