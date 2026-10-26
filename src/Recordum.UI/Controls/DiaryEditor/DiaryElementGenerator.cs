using System;
using AvaloniaEdit.Rendering;

namespace Recordum.UI.Controls;

/// <summary>
/// Sonda Live Preview (branch avalonia-linux-slice, NON una feature): nasconde i marcatori
/// markdown (<c>**</c>, <c>*</c>/<c>_</c>, <c>#…# </c>) — è QUESTA la parte che decide se la
/// sonda ha una risposta positiva o no (vedi report, "Verdict"). Stile invece va a
/// <see cref="MarkdownLivePreviewColorizer"/>.
///
/// Hook AvaloniaEdit: <c>VisualLineElementGenerator</c>, aggiunto a
/// <c>TextView.ElementGenerators</c>. Il motore chiama <see cref="GetFirstInterestedOffset"/>
/// con offset crescenti per trovare la prossima posizione "interessante" nella riga visuale in
/// costruzione, poi <see cref="ConstructElement"/> sull'offset trovato per ottenere l'elemento
/// sostitutivo. Nascondere = restituire un <c>FormattedTextElement</c> con testo vuoto ma
/// <c>documentLength</c> pari alla lunghezza del marcatore: occupa zero pixel a schermo ma zero
/// offset nel documento sottostante (undo/redo, selezione, ecc. restano intatti).
/// </summary>
internal sealed class MarkdownLivePreviewMarkerGenerator : VisualLineElementGenerator
{
    private readonly Func<int> _caretLine;

    public MarkdownLivePreviewMarkerGenerator(Func<int> caretLine) => _caretLine = caretLine;

    internal static int InvocationCount;

    public override int GetFirstInterestedOffset(int startOffset)
    {
        InvocationCount++;
        var document = CurrentContext?.Document;
        if (document is null || startOffset >= document.TextLength) return -1;

        var line = document.GetLineByOffset(startOffset);
        if (line.LineNumber == _caretLine()) return -1; // riga del caret: marcatori restano visibili // riga del caret: marcatori restano visibili

        var text = document.GetText(line);
        foreach (var range in MarkdownLineScanner.MatchMarkerRanges(text))
        {
            var absolute = line.Offset + range.Start;
            if (absolute >= startOffset) return absolute;
        }
        return -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var document = CurrentContext!.Document;
        var line = document.GetLineByOffset(offset);
        var text = document.GetText(line);
        foreach (var range in MarkdownLineScanner.MatchMarkerRanges(text))
        {
            if (line.Offset + range.Start == offset)
                return new FormattedTextElement(string.Empty, range.Length);
        }
        return null; // non dovrebbe accadere se GetFirstInterestedOffset è coerente con questo metodo
    }
}
