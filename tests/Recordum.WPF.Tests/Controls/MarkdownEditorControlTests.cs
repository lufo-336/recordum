using FluentAssertions;
using Microsoft.Web.WebView2.Core;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Controls;
using Xunit;

namespace Recordum.WPF.Tests.Controls;

public class MarkdownEditorControlTests
{
    private sealed class FakeAvailability(bool available) : IWebView2Availability
    {
        public bool IsAvailable { get; } = available;
        public CoreWebView2Environment? Environment => null;
        public Task ProbeAsync() => Task.CompletedTask;
    }


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

    [WpfFact]
    public void Fallback_TextBox_visible_when_WebView2_unavailable()
    {
        var sut = new MarkdownEditorControl { Availability = new FakeAvailability(false) };
        var host = new System.Windows.Window { Content = sut };
        host.Show();
        try
        {
            var box = (System.Windows.Controls.TextBox)sut.FindName("FallbackBox")!;
            box.Visibility.Should().Be(System.Windows.Visibility.Visible);
        }
        finally { host.Close(); }
    }

    [WpfFact]
    public void Fallback_TextBox_reflects_Markdown_DP_value()
    {
        var sut = new MarkdownEditorControl { Availability = new FakeAvailability(false) };
        var host = new System.Windows.Window { Content = sut };
        host.Show();
        try
        {
            sut.Markdown = "# Plain";
            var box = (System.Windows.Controls.TextBox)sut.FindName("FallbackBox")!;
            box.Text.Should().Be("# Plain");
        }
        finally { host.Close(); }
    }
}
