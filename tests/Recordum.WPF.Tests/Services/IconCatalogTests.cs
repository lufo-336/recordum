using FluentAssertions;
using Recordum.Presentation.Services;
using Xunit;

namespace Recordum.WPF.Tests.Services;

public class IconCatalogTests
{
    [Fact]
    public void All_is_non_empty()
        => IconCatalog.All.Should().NotBeEmpty();

    [Fact]
    public void Search_empty_returns_all()
        => IconCatalog.Search("").Should().Equal(IconCatalog.All);

    [Fact]
    public void Search_null_returns_all()
        => IconCatalog.Search(null).Should().Equal(IconCatalog.All);

    [Fact]
    public void Search_is_case_insensitive_substring_over_the_name()
    {
        // "Icon.Dumbbell" should match "dumb"
        IconCatalog.All.Should().Contain("Icon.Dumbbell");
        IconCatalog.Search("dumb").Should().Contain("Icon.Dumbbell");
        IconCatalog.Search("DUMB").Should().Contain("Icon.Dumbbell");
    }

    [Fact]
    public void Search_no_match_is_empty()
        => IconCatalog.Search("zzzznomatch").Should().BeEmpty();
}
