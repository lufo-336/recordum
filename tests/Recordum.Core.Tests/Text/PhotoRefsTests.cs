using FluentAssertions;
using Recordum.Core.Text;
using Xunit;

namespace Recordum.Core.Tests.Text;

public class PhotoRefsTests
{
    private static readonly Guid A = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid B = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Format_then_Find_roundtrips_covering_the_whole_syntax()
    {
        var s = PhotoRefs.Format(A, "pasta.jpg");
        s.Should().Be("![pasta.jpg](photo:11111111222233334444555555555555)");

        var text = "ciao " + s + " fine";
        PhotoRefs.Find(text).Should().Equal(new PhotoRef(5, s.Length, A));
    }

    [Fact]
    public void Format_strips_brackets_and_newlines_from_the_name()
    {
        PhotoRefs.Format(A, "a]b\r\nc.jpg").Should().Be("![abc.jpg](photo:" + A.ToString("N") + ")");
    }

    [Fact]
    public void Find_ignores_inline_and_fenced_code()
    {
        var r = PhotoRefs.Format(A, "x.jpg");
        PhotoRefs.Find($"`{r}`").Should().BeEmpty();
        PhotoRefs.Find($"```\n{r}\n```").Should().BeEmpty();
        PhotoRefs.Find($"`{r}` {r}").Should().ContainSingle().Which.Start.Should().Be(r.Length + 3);
    }

    [Fact]
    public void Find_ignores_invalid_ids()
    {
        PhotoRefs.Find("![x](photo:xyz)").Should().BeEmpty();
        PhotoRefs.Find("![x](photo:11111111-2222-3333-4444-555555555555)").Should().BeEmpty();
    }

    [Fact]
    public void Replace_substitutes_every_reference_and_leaves_the_rest()
    {
        var text = $"a {PhotoRefs.Format(A, "x.jpg")} b `{PhotoRefs.Format(A, "x.jpg")}` c {PhotoRefs.Format(B, "y.jpg")} d";
        PhotoRefs.Replace(text, r => r.Id == A ? "[A]" : "[B]")
            .Should().Be($"a [A] b `{PhotoRefs.Format(A, "x.jpg")}` c [B] d");
    }
}
