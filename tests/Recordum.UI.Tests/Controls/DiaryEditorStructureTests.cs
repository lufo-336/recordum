using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using FluentAssertions;
using Recordum.Presentation.Resources;
using Recordum.UI.Controls;
using Recordum.UI.Tests.HabitScreen;
using Xunit;

namespace Recordum.UI.Tests.Controls;

/// <summary>
/// La resa del diario, provata sulla struttura e non sullo screenshot.
/// <para>Se un marcatore è sostituito, la riga <b>visuale</b> ha meno colonne della riga del <b>documento</b>:
/// l'elemento che lo sostituisce occupa i suoi offset nel documento ma una sola colonna (AvaloniaEdit non ammette
/// elementi da zero colonne). Sulla riga del cursore le due lunghezze coincidono.</para>
/// <para>Niente priming: il "primo AvaloniaEdit headless che non fa il layout" era il template di AvaloniaEdit che
/// mancava dagli stili dell'applicazione e che Markdown.Avalonia caricava al primo uso. Ora sta in <c>App.axaml</c>, e
/// <see cref="Show"/> lo prova a ogni test: la TextView del PRIMO editor mostrato ha una larghezza.</para>
/// </summary>
public class DiaryEditorStructureTests
{
    private const int QuoteLine = 9, SavedPhotoLine = 11, UnsavedPhotoLine = 12, CaretLine = 13, TagLine = 3;

    private static (TextEditor Editor, DiaryEditor Control, Window Window) Show(
        ThemeVariant? variant = null, Action<DiaryEditor>? setup = null, double width = 900)
    {
        var control = ControlGalleries.DiaryEditor(JournalFixtures.PngBytes(320, 180));
        setup?.Invoke(control);
        var window = new Window
        {
            Width = width, Height = 900, Content = control,
            RequestedThemeVariant = variant ?? ThemeVariant.Default,
        };
        window.Show();
        Settle();

        var editor = control.GetLogicalDescendants().OfType<TextEditor>().Single();
        editor.TextArea.TextView.Bounds.Width.Should().BeGreaterThan(0,
            "il template di AvaloniaEdit dev'essere negli stili dell'app: una TextView 0x0 non mostra niente");
        return (editor, control, window);
    }

    private static void Settle()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static VisualLine Visual(TextEditor editor, int lineNumber) =>
        editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(lineNumber));

    private static T InlineControl<T>(VisualLine line) where T : Control =>
        line.Elements.OfType<InlineObjectElement>().Select(e => e.Element).OfType<T>().Single();

    [AvaloniaFact]
    public void A_quote_becomes_a_bar_off_the_caret_line_and_stays_raw_on_it()
    {
        var (editor, control, _) = Show();
        var docLine = editor.Document.GetLineByNumber(QuoteLine);

        var rendered = Visual(editor, QuoteLine);
        rendered.GetVisualColumn(2).Should().Be(1, "i due caratteri di '> ' diventano la sola colonna della barra");
        rendered.VisualLength.Should().Be(docLine.Length - 1);
        InlineControl<Border>(rendered).Width.Should().Be(3, "al posto di '> ' c'è la barra");

        control.CaretOffset = docLine.Offset + 4;
        Settle();

        Visual(editor, QuoteLine).VisualLength.Should().Be(docLine.Length, "sulla riga del cursore la sintassi resta grezza");
    }

    [AvaloniaFact]
    public void The_caret_line_stays_raw_and_the_others_are_rendered()
    {
        var (editor, _, _) = Show();

        Visual(editor, CaretLine).VisualLength.Should().Be(editor.Document.GetLineByNumber(CaretLine).Length);
        foreach (var line in new[] { 1, 3, 4, 5, 6, QuoteLine })
            Visual(editor, line).VisualLength.Should().BeLessThan(editor.Document.GetLineByNumber(line).Length,
                $"la riga {line} non è quella del cursore: i suoi marcatori sono sostituiti");
    }

    [AvaloniaFact]
    public void A_tag_is_painted_in_the_accent_colour()
    {
        var (editor, control, _) = Show();
        var docLine = editor.Document.GetLineByNumber(TagLine);
        var tagColumn = editor.Document.GetText(docLine).IndexOf("#lavoro", StringComparison.Ordinal);
        var visual = Visual(editor, TagLine);

        var element = visual.Elements.Single(e => e.RelativeTextOffset == tagColumn);
        control.TryFindResource("Brush.Accent", control.ActualThemeVariant, out var accent).Should().BeTrue();
        element.TextRunProperties.ForegroundBrush.Should().BeAssignableTo<ISolidColorBrush>()
            .Which.Color.Should().Be(((ISolidColorBrush)accent!).Color);
    }

    [AvaloniaFact]
    public void A_photo_with_a_thumbnail_is_an_image_and_one_without_is_a_placeholder()
    {
        var (editor, _, _) = Show();

        InlineControl<Image>(Visual(editor, SavedPhotoLine)).Bounds.Width.Should().Be(320,
            "la miniatura sta nel layout alla sua misura, non solo costruita");
        InlineControl<TextBlock>(Visual(editor, UnsavedPhotoLine)).Text.Should().Be(Strings.Diario_Editor_PhotoNotSaved);
    }

    [AvaloniaFact]
    public void A_photo_is_never_wider_than_the_line()
    {
        var (editor, _, _) = Show(width: 220);

        var image = InlineControl<Image>(Visual(editor, SavedPhotoLine)).Bounds.Width;
        image.Should().BeGreaterThan(100, "si rimpicciolisce, non sparisce");
        image.Should().BeLessThanOrEqualTo(editor.TextArea.TextView.Bounds.Width);
    }

    [AvaloniaFact]
    public void A_photo_whose_content_is_missing_says_so()
    {
        var (editor, _, _) = Show(setup: c => c.Photos = new Dictionary<Guid, byte[]?> { [ControlGalleries.SavedPhotoId] = null });

        InlineControl<TextBlock>(Visual(editor, SavedPhotoLine)).Text.Should().Be(Strings.Diario_Photo_Missing);
    }

    [AvaloniaFact]
    public void Read_only_renders_every_line_caret_line_included()
    {
        var (editor, _, _) = Show(setup: c => c.IsReadOnly = true);

        for (var n = 1; n <= editor.Document.LineCount; n++)
        {
            var docLine = editor.Document.GetLineByNumber(n);
            var raw = editor.Document.GetText(docLine);
            if (raw.Length == 0 || raw.StartsWith("1", StringComparison.Ordinal)) continue; // vuote e numerate: niente da nascondere
            Visual(editor, n).VisualLength.Should().BeLessThan(docLine.Length, $"in sola lettura nessuna riga resta grezza (riga {n}: {raw})");
        }
    }

    [AvaloniaFact]
    public void The_caret_bounds_sit_on_the_caret_line_inside_the_control()
    {
        var (_, control, _) = Show();

        var caret = control.CaretBounds();

        caret.Should().NotBeNull();
        caret!.Value.Height.Should().BeGreaterThan(0);
        caret.Value.Bottom.Should().BeLessThanOrEqualTo(control.Bounds.Height);
        caret.Value.Top.Should().BeGreaterThan(200, "il cursore è sull'ultima riga, sotto la miniatura");
    }

    // La prova è la struttura qui sopra (e l'asserzione dentro Show); qui si salva il PNG per guardarlo.
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_diary_renders_for_the_eye_too(string variantName)
    {
        var (editor, _, window) = Show(variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);
        InlineControl<Image>(Visual(editor, SavedPhotoLine)).Bounds.Width.Should().Be(320);
        ScreenshotProbeTests.Capture(window, $"diary-editor-{variantName.ToLowerInvariant()}");
    }
}
