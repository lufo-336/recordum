using System.Text;
using FluentAssertions;
using Recordum.Data.Sync;

namespace Recordum.Data.Tests.Sync;

public sealed class SyncFormatDocTests
{
    [Fact]
    public void DocListsEveryPublicField()
    {
        var lines = File.ReadAllLines(DocPath(), Encoding.UTF8);
        SyncCatalog.Entities.Should().NotBeEmpty();

        foreach (var entity in SyncCatalog.Entities)
        {
            var heading = $"### {entity.PublicName}";
            var start = Array.FindIndex(lines, l => l.TrimEnd() == heading);
            start.Should().BeGreaterThanOrEqualTo(0, $"the doc must contain a section '{heading}'");

            var end = Array.FindIndex(lines, start + 1, l => l.StartsWith('#'));
            if (end < 0) end = lines.Length;
            var section = string.Join('\n', lines[start..end]);

            foreach (var field in entity.Fields)
            {
                section.Should().Contain($"`{field.PublicName}`",
                    $"section '{heading}' must list field `{field.PublicName}`");
            }
        }
    }

    private static string DocPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Recordum.slnx")))
                return Path.Combine(dir.FullName, "docs", "formats", "sync-file.md");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root (Recordum.slnx) not found.");
    }
}
