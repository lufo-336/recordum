using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Recordum.UI.Controls;

/// <summary>
/// Sonda Live Preview (branch avalonia-linux-slice, NON una feature): riconosce dentro il testo
/// di UNA riga titoli/grassetto/corsivo. Fonte di verità unica condivisa dai due consumer che
/// devono restare d'accordo su "cosa è marcatura": <see cref="MarkdownLivePreviewColorizer"/>
/// (stile: quale porzione ridimensionare/mettere in grassetto) e
/// <see cref="MarkdownLivePreviewMarkerGenerator"/> (quale porzione nascondere). Se le due
/// classi scansionassero il testo separatamente rischierebbero di desincronizzarsi — un
/// marcatore nascosto ma non stilizzato, o viceversa.
///
/// Deliberatamente semplice: markdown NON annidato, nessun escape (<c>\*</c>), niente liste/
/// link/code — sono lo scope dichiarato fuori budget del probe (vedi report, sezione "Il
/// costo"). Regex non-overlapping: il grassetto (<c>**</c>) viene riconosciuto per primo e le
/// sue porzioni vengono escluse dalla ricerca del corsivo, altrimenti <c>**x**</c> verrebbe
/// letto anche come corsivo (<c>*...*</c> dentro <c>**...**</c>).
/// </summary>
internal static class MarkdownLineScanner
{
    private static readonly Regex Heading = new(@"^(#{1,3})( +)", RegexOptions.Compiled);
    private static readonly Regex Bold = new(@"\*\*([^*\n]+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex ItalicStar =
        new(@"(?<!\*)\*(?!\*)([^*\n]+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
    private static readonly Regex ItalicUnderscore =
        new(@"(?<![\w_])_([^_\n]+?)_(?![\w_])", RegexOptions.Compiled);

    /// <summary>Titolo trovato a inizio riga. <c>MarkerLength</c> = lunghezza di "#…# " inclusi
    /// gli spazi, cioè quanto va nascosto perché il contenuto inizi.</summary>
    internal readonly record struct HeadingMatch(int Level, int MarkerLength);

    /// <summary>Un'estensione bold/italic. <c>Start</c>/<c>End</c> delimitano il CONTENUTO
    /// (senza marcatori), offset relativi all'inizio della riga.</summary>
    internal readonly record struct InlineSpan(int Start, int End, bool IsBold);

    /// <summary>Un intervallo di marcatura da nascondere (es. i due <c>*</c> del corsivo),
    /// offset relativo all'inizio della riga.</summary>
    internal readonly record struct MarkerRange(int Start, int Length);

    internal static HeadingMatch? MatchHeading(string lineText)
    {
        var m = Heading.Match(lineText);
        if (!m.Success) return null;
        return new HeadingMatch(m.Groups[1].Length, m.Length);
    }

    internal static IReadOnlyList<InlineSpan> MatchInlineSpans(string lineText)
    {
        var spans = new List<InlineSpan>();
        var consumed = new List<(int Start, int End)>();

        foreach (Match m in Bold.Matches(lineText))
        {
            spans.Add(new InlineSpan(m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length, true));
            consumed.Add((m.Index, m.Index + m.Length));
        }

        void TryItalic(Regex regex)
        {
            foreach (Match m in regex.Matches(lineText))
            {
                if (consumed.Exists(c => m.Index < c.End && m.Index + m.Length > c.Start)) continue;
                spans.Add(new InlineSpan(m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length, false));
                consumed.Add((m.Index, m.Index + m.Length));
            }
        }

        TryItalic(ItalicStar);
        TryItalic(ItalicUnderscore);

        spans.Sort((a, b) => a.Start.CompareTo(b.Start));
        return spans;
    }

    /// <summary>Tutti i marcatori da nascondere sulla riga: se è un titolo, solo il prefisso
    /// <c>#…# </c> (un titolo non porta anche marcatori bold/italic — vedi Colorizer); altrimenti
    /// i marcatori di ogni <see cref="InlineSpan"/> trovata da <see cref="MatchInlineSpans"/>.</summary>
    internal static IReadOnlyList<MarkerRange> MatchMarkerRanges(string lineText)
    {
        var ranges = new List<MarkerRange>();

        var heading = MatchHeading(lineText);
        if (heading != null)
        {
            ranges.Add(new MarkerRange(0, heading.Value.MarkerLength));
            return ranges;
        }

        foreach (var span in MatchInlineSpans(lineText))
        {
            var markerLength = span.IsBold ? 2 : 1;
            ranges.Add(new MarkerRange(span.Start - markerLength, markerLength));
            ranges.Add(new MarkerRange(span.End, markerLength));
        }

        ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
        return ranges;
    }
}
