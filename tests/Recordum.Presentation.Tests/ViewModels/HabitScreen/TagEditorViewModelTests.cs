using FluentAssertions;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public class TagEditorViewModelTests
{
    private static TagEditorViewModel Build(params string[] known) => new() { KnownNames = known };

    [Fact]
    public void A_prefix_lists_the_existing_tags_and_no_create_entry()
    {
        var sut = Build("Pranzo", "pranzo-fuori", "cena");

        sut.Query = "pra";

        sut.Suggestions.Should().Equal("Pranzo", "pranzo-fuori");
    }

    [Fact]
    public void An_existing_name_in_other_case_is_not_offered_for_creation()
    {
        var sut = Build("Pranzo", "pranzo-fuori");

        sut.Query = "pranzo";

        sut.Suggestions.Should().Equal("Pranzo", "pranzo-fuori");
    }

    [Fact]
    public void An_unknown_name_ends_the_list_with_the_create_entry()
    {
        var sut = Build("cena");

        sut.Query = "pranzo";

        sut.Suggestions.Should().Equal(string.Format(Strings.Diario_Tag_Create, "pranzo"));
    }

    [Fact]
    public void Chosen_tags_are_not_suggested_again_and_the_list_stops_at_eight()
    {
        var sut = Build(Enumerable.Range(1, 12).Select(i => $"t{i:00}").ToArray());
        sut.Add("t01");

        sut.Query = "t";

        sut.Suggestions.Should().HaveCount(8).And.NotContain("t01");
    }

    [Fact]
    public void Add_takes_the_create_entry_as_the_typed_name()
    {
        var sut = Build();
        sut.Query = "nuovo";

        sut.Add(sut.Suggestions[^1]);

        sut.Tags.Should().Equal("nuovo");
        sut.Query.Should().BeEmpty();
    }

    [Fact]
    public void Add_refuses_empty_too_long_and_duplicate_names()
    {
        var sut = Build();

        sut.Add("  ");
        sut.Add(new string('a', 65));
        sut.Add("Pranzo");
        sut.Add("pranzo");

        sut.Tags.Should().Equal("Pranzo");
    }

    [Fact]
    public void Remove_drops_the_tag()
    {
        var sut = Build();
        sut.Add("a");
        sut.Add("b");

        sut.Remove("a");

        sut.Tags.Should().Equal("b");
    }
}
