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
    public void Write_embeds_existing_attachment_and_skips_missing_one()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var photoPath = Path.Combine(Path.GetTempPath(), "recordum-pdf-export-test-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(photoPath, Convert.FromBase64String(OnePixelPngBase64));
        try
        {
            // Must not throw even though the second attachment's file doesn't exist on disk.
            var bytes = new PdfExportFormatter().Write(SampleModel.WithNoteAttachments(photoPath));
            Assert.NotEmpty(bytes);
            Assert.Equal((byte)'%', bytes[0]);
        }
        finally
        {
            File.Delete(photoPath);
        }
    }
}
