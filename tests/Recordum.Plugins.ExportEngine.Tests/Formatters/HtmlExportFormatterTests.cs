using System.Text;
using Recordum.Core.Enums;
using Recordum.Plugins.ExportEngine.Formatters;

namespace Recordum.Plugins.ExportEngine.Tests.Formatters;

public sealed class HtmlExportFormatterTests
{
    [Fact]
    public void Write_is_self_contained_html_with_title_and_embedded_heatmap()
    {
        var f = new HtmlExportFormatter();
        Assert.Equal(ExportFormat.Html, f.Format);
        var html = Encoding.UTF8.GetString(f.Write(SampleModel.One()));
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<title>Run</title>", html, StringComparison.Ordinal);
        Assert.Contains("data:image/png;base64,", html, StringComparison.Ordinal);
        Assert.Contains("felt good", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_prints_the_value_with_its_unit()
    {
        var html = Encoding.UTF8.GetString(new HtmlExportFormatter().Write(SampleModel.One()));
        Assert.Contains("<td>5 km</td><td>Distance</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_renders_periods_table_for_resistance_habit()
    {
        var html = Encoding.UTF8.GetString(new HtmlExportFormatter().Write(SampleModel.WithAbstinencePeriods()));
        Assert.Contains("<th>Started</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Ended</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Duration</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Outcome</th>", html, StringComparison.Ordinal);
        Assert.Contains("<td>2026-06-13 09:00</td><td>2026-07-01 19:00</td><td>18.10:00:00</td><td>relapsed</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>2026-07-02 08:00</td><td></td><td>0.04:30:00</td><td>open</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_embeds_existing_attachment_and_skips_missing_one()
    {
        var photoPath = Path.Combine(Path.GetTempPath(), "recordum-html-export-test-" + Guid.NewGuid().ToString("N") + ".jpg");
        File.WriteAllBytes(photoPath, "fake image bytes"u8.ToArray());
        try
        {
            var html = Encoding.UTF8.GetString(new HtmlExportFormatter().Write(SampleModel.WithNoteAttachments(photoPath)));
            var expectedBase64 = Convert.ToBase64String("fake image bytes"u8.ToArray());
            Assert.Contains($"data:image/jpeg;base64,{expectedBase64}", html, StringComparison.Ordinal);
            Assert.Contains("alt=\"lake.jpg\"", html, StringComparison.Ordinal);
            // The second attachment's AbsolutePath doesn't exist on disk — must be skipped silently.
            Assert.DoesNotContain("missing.png", html, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(photoPath);
        }
    }
}
