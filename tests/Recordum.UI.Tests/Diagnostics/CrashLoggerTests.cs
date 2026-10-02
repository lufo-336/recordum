using FluentAssertions;
using Recordum.UI.Diagnostics;
using Xunit;

namespace Recordum.UI.Tests.Diagnostics;

public class CrashLoggerTests
{
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    [Fact]
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
}
