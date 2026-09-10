using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using AvaloniaEdit;
using FluentAssertions;
using Recordum.UI.Views;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>
/// Sonda Live Preview — la prova che conta, e NON è lo screenshot.
/// <para>Se i marcatori markdown sono davvero nascosti, la riga <b>visuale</b> è più corta della
/// riga del <b>documento</b>: il generatore ha sostituito <c>**</c> con un elemento a larghezza
/// zero che occupa comunque i suoi offset nel documento. Sulla riga del caret, invece, le due
/// lunghezze coincidono, perché lì la sintassi resta grezza per poterla editare.</para>
/// <para>Perché non uno screenshot: in headless il PRIMO AvaloniaEdit della sessione non fa il
/// proprio passaggio di layout (TextView 0x0, VisualLines lancia VisualLinesInvalidException) e il
/// PNG esce vuoto — misurato il 2026-09-10. Mostrare un'altra vista con un editor prima "sveglia"
/// il layout, ed è quello che fa <see cref="Prime"/>. Un test che guarda la struttura non dipende
/// da quel risveglio quanto una cattura di pixel, ed è comunque più preciso: un PNG non dice
/// QUANTI caratteri sono stati nascosti.</para>
/// </summary>
public class LivePreviewStructureTests
{
    private static (TextEditor Editor, LivePreviewProbeView View) ShowPrimed()
    {
        // Priming: un editor "sacrificale" in una vista diversa, mostrato e scartato.
        var priming = new Window { Width = 400, Height = 300, Content = new MarkdownEditorProbeView() };
        priming.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var view = new LivePreviewProbeView();
        var window = new Window { Width = 900, Height = 560, Content = view };
        window.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (view.GetLogicalDescendants().OfType<TextEditor>().Single(), view);
    }

    [AvaloniaFact]
    public void The_markers_are_hidden_on_every_line_except_the_one_with_the_caret()
    {
        var (editor, _) = ShowPrimed();
        var view = editor.TextArea.TextView;
        var doc = editor.Document;

        // Riga 1: "# Diario — 10 settembre" — non è la riga del caret (che è la 3), quindi il "# "
        // dev'essere collassato: la riga visuale è più corta di quella del documento.
        var docLine1 = doc.GetLineByNumber(1);
        var visual1 = view.GetOrConstructVisualLine(docLine1);
        visual1.VisualLength.Should().BeLessThan(docLine1.Length,
            "sulla riga 1 il marcatore '# ' dev'essere nascosto, quindi la riga visuale è più corta");

        // Riga 3: "Oggi ho **finito** ... *più semplice* ..." — È la riga del caret, quindi i
        // marcatori restano visibili e le due lunghezze coincidono.
        var docLine3 = doc.GetLineByNumber(3);
        var visual3 = view.GetOrConstructVisualLine(docLine3);
        visual3.VisualLength.Should().Be(docLine3.Length,
            "sulla riga del caret la sintassi resta grezza: nessun carattere nascosto");
    }

    [AvaloniaFact]
    public void Moving_the_caret_away_hides_the_markers_of_the_line_it_left()
    {
        var (editor, view) = ShowPrimed();
        var doc = editor.Document;
        var docLine3 = doc.GetLineByNumber(3);

        // Prima: caret sulla riga 3, marcatori visibili.
        editor.TextArea.TextView.GetOrConstructVisualLine(docLine3)
            .VisualLength.Should().Be(docLine3.Length);

        view.MoveEditorCaretTo(1, 1);
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Dopo: il caret è altrove, quindi la riga 3 si formatta e i suoi marcatori spariscono.
        editor.TextArea.TextView.GetOrConstructVisualLine(docLine3)
            .VisualLength.Should().BeLessThan(docLine3.Length,
                "lasciata dal caret, la riga dev'essere formattata come tutte le altre");
    }
}
