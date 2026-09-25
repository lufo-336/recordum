using System.IO;
using FluentAssertions;
using Recordum.Presentation.Bootstrap;
using Xunit;

namespace Recordum.Presentation.Tests.Bootstrap;

/// <summary>Mai <c>RecordumOptions.Default()</c> qui: creerebbe (e restringerebbe) la cartella dei dati veri
/// dell'utente che lancia i test — proprio ciò che la decisione B del Passo 0 vuole tenere separato.</summary>
public sealed class RecordumOptionsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "recordum-options-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* best effort */ }
    }

    private string Sub(string name) => Path.Combine(_dir, name);

    [Fact]
    public void The_command_line_wins_over_the_environment_which_wins_over_the_default()
    {
        string? Env(string name) => name == RecordumOptions.DataDirectoryVariable ? Sub("from-env") : null;

        var fromArgs = RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, Sub("from-args")], Env);
        var fromEnv = RecordumOptions.Resolve([], Env);

        fromArgs.DataDirectory.Should().Be(Sub("from-args"));
        fromEnv.DataDirectory.Should().Be(Sub("from-env"));
        fromArgs.IsDefaultLocation.Should().BeFalse();
        fromEnv.IsDefaultLocation.Should().BeFalse();
    }

    [Fact]
    public void Every_path_lives_under_the_data_directory_and_the_folders_exist()
    {
        var o = RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, _dir], _ => null);

        new[] { o.DatabasePath, o.KeyFilePath, o.SaltFilePath, o.AttachmentsDirectory, o.BackupsDirectory,
                o.AutoBackupKeyPath, o.TempDirectory }
            .Should().OnlyContain(p => p.StartsWith(_dir + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        Directory.Exists(o.AttachmentsDirectory).Should().BeTrue();
        Directory.Exists(o.BackupsDirectory).Should().BeTrue();
        Directory.Exists(o.TempDirectory).Should().BeTrue();
    }

    [Fact]
    public void On_unix_the_data_folders_are_owner_only_even_if_they_existed_looser()
    {
        if (OperatingSystem.IsWindows()) return;                          // ACL, non modi Unix
        const UnixFileMode Loose = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                   | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                                   | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        var dataDir = Sub("data");
        Directory.CreateDirectory(Path.Combine(dataDir, "attachments"));
        File.SetUnixFileMode(dataDir, Loose);
        File.SetUnixFileMode(Path.Combine(dataDir, "attachments"), Loose);

        var o = RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, dataDir], _ => null);

        foreach (var d in new[] { o.DataDirectory, o.AttachmentsDirectory, o.BackupsDirectory, o.TempDirectory })
            File.GetUnixFileMode(d).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, d);
    }
}
