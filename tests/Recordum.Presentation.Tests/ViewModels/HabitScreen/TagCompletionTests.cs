using FluentAssertions;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public class TagCompletionTests
{
    [Fact]
    public void Find_returns_the_hash_word_that_ends_at_the_caret()
    {
        var q = TagCompletion.Find("Pasta #pra", 10);
        q.Should().Be(new TagQuery(6, "pra"));
    }

    [Fact]
    public void Find_ignores_a_hash_that_is_not_a_tag_word()
    {
        TagCompletion.Find("# Tit", 5).Should().BeNull("un titolo Markdown non è un tag");
        TagCompletion.Find("a#b", 3).Should().BeNull("il # conta solo a inizio parola");
        TagCompletion.Find("Pasta", 5).Should().BeNull();
    }

    [Fact]
    public void Find_accepts_a_bare_hash_so_the_menu_can_list_every_tag()
        => TagCompletion.Find("ciao #", 6).Should().Be(new TagQuery(5, ""));

    [Fact]
    public void Apply_replaces_the_word_and_puts_the_caret_after_the_space()
    {
        var q = TagCompletion.Find("Pasta #pra", 10)!.Value;

        TagCompletion.Apply("Pasta #pra", q, "pranzo").Should().Be(("Pasta #pranzo ", 14));
    }

    [Fact]
    public void Apply_keeps_the_text_after_the_caret()
    {
        var q = TagCompletion.Find("Pasta #pra oggi", 10)!.Value;

        TagCompletion.Apply("Pasta #pra oggi", q, "pranzo").Should().Be(("Pasta #pranzo  oggi", 14));
    }
}
