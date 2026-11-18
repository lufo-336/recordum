using FluentAssertions;
using Recordum.Core.Text;
using Xunit;

namespace Recordum.Core.Tests.Text;

public class TagNamesTests
{
    [Theory]
    [InlineData("Sto malissimo", "sto-malissimo")]
    [InlineData("Così  così!", "così-così")]
    [InlineData(" -Bene- ", "bene")]
    [InlineData("È ok", "è-ok")]
    [InlineData("7", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void From_label(string? label, string? expected) => TagNames.FromLabel(label).Should().Be(expected);

    [Fact]
    public void Long_label_is_cut_to_64() => TagNames.FromLabel(new string('a', 80)).Should().HaveLength(64);
}
