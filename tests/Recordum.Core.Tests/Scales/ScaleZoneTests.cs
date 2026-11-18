using FluentAssertions;
using Recordum.Core.Scales;
using Xunit;

namespace Recordum.Core.Tests.Scales;

public class ScaleZoneTests
{
    private static NamedAnchor A(int v, string n) => new(v, n, null);

    private static readonly NamedAnchor[] Mood =
        [A(100, "Benissimo"), A(1, "Malissimo"), A(50, "Così così"), A(25, "Male"), A(75, "Bene")];

    [Fact]
    public void Between_marks()
    {
        var r = ScaleZone.Read(Mood, 37);
        r.Nearest!.Name.Should().Be("Male");
        r.Below!.Name.Should().Be("Male");
        r.Above!.Name.Should().Be("Così così");
        r.Exact.Should().BeNull();
    }

    [Fact]
    public void Tie_goes_below()
        => ScaleZone.Read([A(25, "Male"), A(50, "Così così")], 37.5m).Nearest!.Name.Should().Be("Male");

    [Fact]
    public void Exact_hit()
    {
        var r = ScaleZone.Read(Mood, 50);
        r.Exact!.Name.Should().Be("Così così");
        r.Nearest!.Name.Should().Be("Così così");
    }

    [Fact]
    public void Under_the_lowest()
    {
        var r = ScaleZone.Read(Mood, 0);
        r.Below.Should().BeNull();
        r.Nearest!.Name.Should().Be("Malissimo");
    }

    [Fact]
    public void Over_the_highest() => ScaleZone.Read(Mood, 100).Above.Should().BeNull();

    [Fact]
    public void Empty_list()
    {
        var r = ScaleZone.Read([], 5);
        r.Should().Be(new ZoneReading(null, null, null, null));
    }
}
