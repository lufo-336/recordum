using FluentAssertions;
using Recordum.Core.Formatting;
using Xunit;

namespace Recordum.Core.Tests.Formatting;

public class ReferenceCodeSlugTests
{
    [Theory]
    [InlineData("Litri d'acqua", "litri-d-acqua")]
    [InlineData("  Café  crème ", "caf-cr-me")]   // non-ASCII fuori: [a-z0-9-] soltanto
    [InlineData("!!!", "custom")]                  // tutto illegale → fallback
    public void From_slugifies(string name, string expected) =>
        ReferenceCodeSlug.From(name, []).Should().Be(expected);

    [Fact]
    public void From_suffixes_on_collision_case_insensitive()
    {
        ReferenceCodeSlug.From("Litri", ["litri"]).Should().Be("litri-2");
        ReferenceCodeSlug.From("Litri", ["LITRI", "litri-2"]).Should().Be("litri-3");
    }
}
