using System.IO;
using FluentAssertions;
using Recordum.Presentation.Bootstrap;
using Xunit;

namespace Recordum.Presentation.Tests.Bootstrap;

public class RecordumOptionsTests
{
    [Fact]
    public void Default_creates_attachments_and_backups_directories()
    {
        var opts = RecordumOptions.Default();

        opts.AttachmentsDirectory.Should().EndWith(Path.Combine("Recordum", "attachments"));
        opts.BackupsDirectory.Should().EndWith(Path.Combine("Recordum", "backups"));
        opts.AutoBackupKeyPath.Should().EndWith(Path.Combine("Recordum", "autobackup.key"));
        Directory.Exists(opts.AttachmentsDirectory).Should().BeTrue();
        Directory.Exists(opts.BackupsDirectory).Should().BeTrue();
    }
}
