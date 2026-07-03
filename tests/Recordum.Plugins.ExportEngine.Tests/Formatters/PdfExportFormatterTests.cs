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
}
