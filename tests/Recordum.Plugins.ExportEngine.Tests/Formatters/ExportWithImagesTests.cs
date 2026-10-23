using System.Text;
using Recordum.Core.Text;
using Recordum.Plugins.Contracts.Export;
using Recordum.Plugins.ExportEngine;
using Recordum.Plugins.ExportEngine.Formatters;

namespace Recordum.Plugins.ExportEngine.Tests.Formatters;

public sealed class ExportWithImagesTests
{
    private static readonly Guid IdA = Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
    private static readonly Guid IdB = Guid.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
    private static readonly byte[] BytesA = [1, 2, 3];
    private static readonly byte[] BytesB = [4, 5, 6];

    private static ExportAttachmentRef Att(Guid id, string name, byte[]? content)
        => new(id, name, "image/jpeg", $"images/{id:N}.jpg") { Content = content };

    private static ExportModel Model(string body, params ExportAttachmentRef[] atts)
    {
        var template = SampleModel.WithNoteAttachments(null);
        var habit = template.Habits[0];
        var note = habit.Notes[0] with { BodyMd = body, Attachments = atts };
        return template with { Habits = [habit with { Notes = [note] }] };
    }

    private static string Text(ExportOutput o) => Encoding.UTF8.GetString(o.Document);

    [Fact]
    public void Markdown_links_the_photo_in_the_images_folder_and_returns_the_file()
    {
        var o = new MarkdownExportFormatter().Write(Model($"look {PhotoRefs.Format(IdA, "a.jpg")} here", Att(IdA, "a.jpg", BytesA)));

        Assert.Contains($"![a.jpg](images/{IdA:N}.jpg)", Text(o), StringComparison.Ordinal);
        Assert.DoesNotContain("photo:", Text(o), StringComparison.Ordinal);
        var file = Assert.Single(o.Files);
        Assert.Equal($"images/{IdA:N}.jpg", file.RelativePath);
        Assert.Equal(BytesA, file.Content);
    }

    [Fact]
    public void Markdown_two_photos_with_the_same_filename_give_two_distinct_files()
    {
        var body = PhotoRefs.Format(IdA, "IMG_0001.jpg") + " " + PhotoRefs.Format(IdB, "IMG_0001.jpg");
        var o = new MarkdownExportFormatter().Write(Model(body, Att(IdA, "IMG_0001.jpg", BytesA), Att(IdB, "IMG_0001.jpg", BytesB)));

        Assert.Equal(2, o.Files.Select(f => f.RelativePath).Distinct().Count());
        Assert.Equal(BytesB, o.Files.Single(f => f.RelativePath.Contains(IdB.ToString("N"), StringComparison.Ordinal)).Content);
    }

    [Fact]
    public void Markdown_photo_without_content_makes_no_file_and_a_visible_marker()
    {
        var o = new MarkdownExportFormatter().Write(Model(PhotoRefs.Format(IdA, "a.jpg"), Att(IdA, "a.jpg", null)));

        Assert.Empty(o.Files);
        Assert.Contains("[a.jpg — missing]", Text(o), StringComparison.Ordinal);
        Assert.DoesNotContain("photo:", Text(o), StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_keeps_the_whole_entry_under_a_date_time_heading()
    {
        var body = "# Title\n\n- one\n- two\n\nclosing";
        var md = Text(new MarkdownExportFormatter().Write(Model(body)));

        Assert.Contains("#### 2026-05-20 09:00\n\n# Title\n\n- one\n- two\n\nclosing", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_appends_an_unreferenced_attachment_after_the_text()
    {
        var o = new MarkdownExportFormatter().Write(Model("plain", Att(IdA, "a.jpg", BytesA)));

        Assert.Contains($"plain\n\n![a.jpg](images/{IdA:N}.jpg)", Text(o), StringComparison.Ordinal);
        Assert.Single(o.Files);
    }

    [Fact]
    public void Html_puts_the_image_where_the_reference_was()
    {
        var o = new HtmlExportFormatter().Write(Model($"before {PhotoRefs.Format(IdA, "a.jpg")} after", Att(IdA, "a.jpg", BytesA)));
        var html = Text(o);

        var b64 = Convert.ToBase64String(BytesA);
        Assert.Contains($"before <img src=\"data:image/jpeg;base64,{b64}\" alt=\"a.jpg\" /> after", html, StringComparison.Ordinal);
        Assert.DoesNotContain("photo:", html, StringComparison.Ordinal);
        Assert.Equal(1, html.Split("base64," + b64).Length - 1);
        Assert.Empty(o.Files);
    }

    [Fact]
    public void Pdf_drops_the_reference_syntax_from_the_text()
    {
        Assert.Equal("see  now", ExportFormatterBase.StripPhotoRefs($"see {PhotoRefs.Format(IdA, "a.jpg")} now"));
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var o = new PdfExportFormatter().Write(Model($"see {PhotoRefs.Format(IdA, "a.jpg")} now", Att(IdA, "a.jpg", null)));
        Assert.Equal((byte)'%', o.Document[0]);
        Assert.Empty(o.Files);
    }

    [Fact]
    public void Csv_and_json_have_no_accessory_files_and_keep_the_text_as_is()
    {
        var model = Model(PhotoRefs.Format(IdA, "a.jpg"), Att(IdA, "a.jpg", BytesA));
        Assert.Empty(new CsvExportFormatter().Write(model).Files);
        var json = new JsonExportFormatter().Write(model);
        Assert.Empty(json.Files);
        Assert.Contains("photo:", Text(json), StringComparison.Ordinal);
    }
}
