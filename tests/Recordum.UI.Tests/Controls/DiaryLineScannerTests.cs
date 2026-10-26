using FluentAssertions;
using Recordum.Core.Text;
using Recordum.UI.Controls;
using Xunit;

namespace Recordum.UI.Tests.Controls;

public class DiaryLineScannerTests
{
    [Theory]
    [InlineData("- uno")]
    [InlineData("* uno")]
    [InlineData("+ uno")]
    public void A_bullet_has_a_two_character_marker_drawn_as_a_bullet(string line)
    {
        var scan = DiaryLineScanner.Scan(line);

        scan.Block.Should().Be(DiaryBlock.Bullet);
        scan.MarkerLength.Should().Be(2);
        scan.Markers.Should().Equal(new DiaryMarker(0, 2, DiaryMarkerKind.Bullet));
        scan.Spans.Should().BeEmpty("'* uno' è un elenco, non l'inizio di un corsivo");
    }

    [Fact]
    public void A_numbered_item_keeps_its_number_visible()
    {
        var scan = DiaryLineScanner.Scan("12. dodici");

        scan.Block.Should().Be(DiaryBlock.Numbered);
        scan.Level.Should().Be(12);
        scan.MarkerLength.Should().Be(4);
        scan.Markers.Should().BeEmpty("il numero si attenua, non si nasconde");
    }

    [Fact]
    public void A_quote_has_a_two_character_marker_drawn_as_a_bar()
    {
        var scan = DiaryLineScanner.Scan("> citazione");

        scan.Block.Should().Be(DiaryBlock.Quote);
        scan.Markers.Should().Equal(new DiaryMarker(0, 2, DiaryMarkerKind.QuoteBar));
    }

    [Fact]
    public void A_hashtag_is_a_tag_span()
        => DiaryLineScanner.Scan("a #pranzo").Tags.Should().Equal((2, 7));

    [Fact]
    public void A_tag_inside_bold_is_still_a_tag_as_it_is_when_saved()
    {
        const string line = "**pranzo con #amici**";

        DiaryLineScanner.Scan(line).Tags.Should().Equal(InlineTags.Find(line)).And.Equal((13, 6));
    }

    [Fact]
    public void A_heading_hash_is_not_a_tag()
    {
        var scan = DiaryLineScanner.Scan("# Titolo");

        scan.Block.Should().Be(DiaryBlock.Heading);
        scan.Level.Should().Be(1);
        scan.Tags.Should().BeEmpty();
        scan.Markers.Should().Equal(new DiaryMarker(0, 2, DiaryMarkerKind.Hidden));
    }

    [Fact]
    public void A_photo_reference_is_one_image_with_its_id()
    {
        var id = Guid.NewGuid();
        var line = PhotoRefs.Format(id, "a_b_c #x.jpg");

        var scan = DiaryLineScanner.Scan(line);

        scan.Markers.Should().Equal(new DiaryMarker(0, line.Length, DiaryMarkerKind.Photo, id));
        scan.Tags.Should().BeEmpty("dentro il riferimento niente è un tag");
        scan.Spans.Should().BeEmpty("né un corsivo");
    }

    [Fact]
    public void Bold_and_italic_hide_their_markers_and_bold_is_not_read_as_italic()
    {
        var scan = DiaryLineScanner.Scan("**a** e *b*");

        scan.Spans.Should().Equal(new InlineSpan(2, 3, true), new InlineSpan(9, 10, false));
        scan.Markers.Select(m => (m.Start, m.Length)).Should().Equal((0, 2), (3, 2), (8, 1), (10, 1));
    }
}
