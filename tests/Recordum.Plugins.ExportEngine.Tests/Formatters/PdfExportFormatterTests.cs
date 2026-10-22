using Recordum.Core.Enums;
using Recordum.Plugins.ExportEngine.Formatters;

namespace Recordum.Plugins.ExportEngine.Tests.Formatters;

public sealed class PdfExportFormatterTests
{
    [Fact]
    public void Write_produces_a_pdf_document()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var f = new PdfExportFormatter();
        Assert.Equal(ExportFormat.Pdf, f.Format);
        var bytes = f.Write(SampleModel.One());
        Assert.NotEmpty(bytes);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    [Fact]
    public void Write_renders_periods_table_for_resistance_habit()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var bytes = new PdfExportFormatter().Write(SampleModel.WithAbstinencePeriods());
        Assert.NotEmpty(bytes);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    // Minimal valid 1x1 transparent PNG — QuestPDF/SkiaSharp decodes .Image(bytes), so the
    // attachment fixture needs a real image, not arbitrary bytes.
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    [Fact]
    public void Write_embeds_attachment_content_and_skips_one_without()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var with = new PdfExportFormatter().Write(SampleModel.WithNoteAttachments(Convert.FromBase64String(OnePixelPngBase64)));
        var without = new PdfExportFormatter().Write(SampleModel.WithNoteAttachments(null));
        Assert.Equal((byte)'%', with[0]);
        // The image is in the document: the same model without content is a smaller file.
        Assert.True(with.Length > without.Length, $"{with.Length} <= {without.Length}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Write_skips_a_zero_length_or_undecodable_attachment_instead_of_failing(int kind)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var bad = kind == 0 ? Array.Empty<byte>() : "not an image at all"u8.ToArray();

        var pdf = new PdfExportFormatter().Write(SampleModel.WithNoteAttachments(bad));

        Assert.Equal((byte)'%', pdf[0]);
    }
}
