using FluentAssertions;
using Recordum.WPF.Controls;
using Xunit;

namespace Recordum.WPF.Tests.Controls;

public class MarkdownEditorControlTests
{
    [WpfFact]
    public void Markdown_DP_default_is_empty_string()
    {
        var sut = new MarkdownEditorControl();
        sut.Markdown.Should().Be(string.Empty);
    }

    [WpfFact]
    public void Markdown_DP_round_trips()
    {
        var sut = new MarkdownEditorControl();
        sut.Markdown = "# hello";
        sut.Markdown.Should().Be("# hello");
    }

    [WpfFact]
    public void IsReadOnly_DP_default_is_false()
    {
        var sut = new MarkdownEditorControl();
        sut.IsReadOnly.Should().BeFalse();
    }

    [WpfFact]
    public void Placeholder_DP_round_trips()
    {
        var sut = new MarkdownEditorControl();
        sut.Placeholder = "Write here";
        sut.Placeholder.Should().Be("Write here");
    }
}
