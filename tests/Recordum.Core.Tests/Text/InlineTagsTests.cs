using FluentAssertions;
using Recordum.Core.Text;
using Xunit;

namespace Recordum.Core.Tests.Text;

public class InlineTagsTests
{
    [Fact]
    public void Tags_come_in_order_of_first_appearance()
        => InlineTags.Parse("Pasta #pranzo e #Casa").Should().Equal("pranzo", "Casa");

    [Theory]
    [InlineData("# Titolo")]
    [InlineData("#2")]
    [InlineData("C#")]
    [InlineData("url#frag")]
    [InlineData("`#no`")]
    [InlineData("```\n#no\n```")]
    [InlineData("```\n#no senza chiusura")]
    public void Things_that_are_not_tags(string text)
        => InlineTags.Parse(text).Should().BeEmpty();

    [Fact]
    public void Find_gives_the_position_of_every_tag_repeats_included_and_skips_code()
        => InlineTags.Find("`#no` #pranzo e #Pranzo").Should().Equal((6, 7), (16, 7));

    [Fact]
    public void Tags_are_distinct_ignoring_case_first_spelling_wins()
        => InlineTags.Parse("#pranzo #Pranzo").Should().Equal("pranzo");

    [Fact]
    public void Letters_digits_underscore_and_hyphen_are_allowed()
        => InlineTags.Parse("#città-vecchia_2").Should().Equal("città-vecchia_2");

    [Fact]
    public void A_name_over_64_characters_is_not_a_tag()
    {
        InlineTags.Parse("#" + new string('a', 65)).Should().BeEmpty();
        InlineTags.Parse("#" + new string('a', 64)).Should().HaveCount(1);
    }

    [Fact]
    public void A_tag_after_an_opening_bracket_or_at_text_start_counts()
        => InlineTags.Parse("#a (#b)").Should().Equal("a", "b");

    [Theory]
    [InlineData("pranzo", true)]
    [InlineData("città-vecchia_2", true)]
    [InlineData("pranzo fuori", false)]
    [InlineData("#pranzo", false)]
    [InlineData("a.b", false)]
    [InlineData("2026", false)]
    [InlineData("", false)]
    public void IsValidName_is_true_only_when_the_text_reads_back_as_that_one_tag(string name, bool expected)
        => InlineTags.IsValidName(name).Should().Be(expected);

    [Fact]
    public void IsValidName_refuses_a_name_over_the_length_cap()
        => InlineTags.IsValidName(new string('a', 65)).Should().BeFalse();
}
