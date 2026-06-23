using FluentAssertions;
using Recordum.Data.Backup;
using Xunit;

namespace Recordum.Data.Tests.Backup;

public class SchemaVersionGuardTests
{
    [Theory]
    // CR2: the old major-only compare returned 0 for these, so the restore guard never fired.
    [InlineData("2.10", "2.7", true)]   // 2.10 IS newer than 2.7 (naive string/float compare gets this wrong)
    [InlineData("2.11", "2.10", true)]
    [InlineData("3.0", "2.10", true)]
    [InlineData("2.7", "2.10", false)]
    [InlineData("2.10", "2.10", false)]
    [InlineData("2.9", "2.10", false)]
    [InlineData("1.0", "2.10", false)]
    public void IsNewer_compares_major_then_minor_numerically(string candidate, string current, bool expected)
        => SchemaVersionGuard.IsNewer(candidate, current).Should().Be(expected);
}
