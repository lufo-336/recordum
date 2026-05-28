using FluentAssertions;
using Recordum.Core.Backup;
using Xunit;

namespace Recordum.Core.Tests.Backup;

public class BackupManifestTests
{
    [Fact]
    public void Manifest_carries_full_payload()
    {
        var m = new BackupManifest(
            SchemaVersion: "2.7",
            AppVersion: "3.10.0",
            CreatedAtUtc: new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc),
            Kind: BackupKind.Manual,
            SizeBytesUncompressed: 1234,
            Counts: new BackupCounts(2, 10, 1, 0),
            Plugins: new[]
            {
                new PluginManifestSnapshot("io.recordum.chart-engine", "3.10.0", true, false),
            },
            Permissions: new PermissionState(new Dictionary<string, IReadOnlyList<string>>()));

        m.Counts.Habits.Should().Be(2);
        m.Plugins.Should().ContainSingle();
        m.Permissions.GrantsByPlugin.Should().BeEmpty();
    }
}
