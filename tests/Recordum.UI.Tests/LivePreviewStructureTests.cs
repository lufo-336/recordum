using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using FluentAssertions;
using Recordum.UI.Controls;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>
/// Live Preview — la prova che conta, e NON e' lo screenshot.
/// <para>Se i marcatori markdown sono davvero nascosti, la riga <b>visuale</b> e' piu' corta della
/// riga del <b>documento</b>: il generatore ha sostituito <c>**</c> con un elemento a larghezza
/// zero che occupa comunque i suoi offset nel documento. Sulla riga del caret, invece, le due
/// lunghezze coincidono, perche' li' la sintassi resta grezza per poterla editare.</para>
/// <para>Perche' non uno screenshot: in headless il PRIMO AvaloniaEdit della sessione non fa il
/// proprio passaggio di layout (TextView 0x0, VisualLines lancia VisualLinesInvalidException) e il
/// PNG esce vuoto — misurato il 2026-09-10. Mostrare un altro editor prima "sveglia" il layout, ed
/// e' quello che fa <see cref="ShowPrimed"/>. Primare con la STESSA vista non funziona: provato.</para>
/// </summary>
public class LivePreviewStructureTests
{
    private static (TextEditor Editor, LivePreviewEditorControl Control, Window Window) ShowPrimed(ThemeVariant? variant = null)
    {
        // Priming: un editor sacrificale, di un ALTRO tipo, mostrato e scartato. Prima era una view
        // di sonda; ora e' il controllo nudo, che e' tutto cio' che quella view portava.
        var priming = new Window { Width = 400, Height = 300, Content = ControlGalleries.MarkdownEditor() };
        priming.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var control = ControlGalleries.LivePreviewEditor();
        var window = new Window
        {
            Width = 900, Height = 560, Content = control,
            RequestedThemeVariant = variant ?? ThemeVariant.Default,
        };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        return (control.GetLogicalDescendants().OfType<TextEditor>().Single(), control, window);
    }

    [AvaloniaFact]
    public void The_markers_are_hidden_on_every_line_except_the_one_with_the_caret()
    {
        var (editor, _, _) = ShowPrimed();
        var view = editor.TextArea.TextView;
        var doc = editor.Document;

        // Riga 1: "# Diario — 10 settembre" — non e' la riga del caret (che e' la 3), quindi il "# "
        // dev'essere collassato: la riga visuale e' piu' corta di quella del documento.
        var docLine1 = doc.GetLineByNumber(1);
        var visual1 = view.GetOrConstructVisualLine(docLine1);
        visual1.VisualLength.Should().BeLessThan(docLine1.Length,
            "sulla riga 1 il marcatore '# ' dev'essere nascosto, quindi la riga visuale e' piu' corta");

        // Riga 3: e' la riga del caret, quindi i marcatori restano visibili e le due lunghezze coincidono.
        var docLine3 = doc.GetLineByNumber(3);
        var visual3 = view.GetOrConstructVisualLine(docLine3);
        visual3.VisualLength.Should().Be(docLine3.Length,
            "sulla riga del caret la sintassi resta grezza: nessun carattere nascosto");
    }

    [AvaloniaFact]
    public void Moving_the_caret_away_hides_the_markers_of_the_line_it_left()
    {
        var (editor, control, _) = ShowPrimed();
        var doc = editor.Document;
        var docLine3 = doc.GetLineByNumber(3);

        editor.TextArea.TextView.GetOrConstructVisualLine(docLine3)
            .VisualLength.Should().Be(docLine3.Length);

        control.MoveCaretTo(1, 1);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        editor.TextArea.TextView.GetOrConstructVisualLine(docLine3)
            .VisualLength.Should().BeLessThan(docLine3.Length,
                "lasciata dal caret, la riga dev'essere formattata come tutte le altre");
    }

    // Le due varianti: il vecchio test di sonda le salvava entrambe, e i token dell'editor sono tra quelli
    // che cambiano col tema. La prova e' la struttura sopra; qui si salva il PNG per guardarlo.
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_live_preview_renders_for_the_eye_too(string variantName)
    {
        var (editor, _, window) = ShowPrimed(variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);
        editor.TextArea.TextView.Bounds.Width.Should().BeGreaterThan(0,
            "il priming deve aver svegliato il layout: una TextView 0x0 e' un PNG vuoto con il test verde");
        ScreenshotProbeTests.Capture(window, $"livepreview-{variantName.ToLowerInvariant()}");
    }
}
