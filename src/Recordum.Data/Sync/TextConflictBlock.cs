using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Recordum.Data.Sync;

/// <summary>
/// Regola B (<c>docs/formats/sync-file.md</c> §6.3): due versioni dello stesso testo libero si tengono tutte e due. La
/// vincente resta in cima, l'altra va in coda in un blocco <c>"\n\n— dispositivo · yyyy-MM-dd HH:mm —\n" + testo</c>.
/// Il blocco è un dato, uguale in ogni lingua.
/// <para>Una versione già presente (in cima o come corpo di un blocco) non si riaggiunge. I blocchi in coda si tengono in
/// ordine cronologico (poi dispositivo, poi testo): così il risultato non dipende dall'ordine in cui arrivano i file.</para>
/// </summary>
public static partial class TextConflictBlock
{
    private const string Separator = "\n\n";
    private const string TimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>Il vincente con in coda le parti del perdente che non ha già. Senza niente di nuovo torna
    /// <paramref name="winner"/> invariato; con un vincente vuoto il risultato inizia dal blocco (senza righe vuote).</summary>
    public static string Merge(string winner, string loser, string? loserDevice, long loserUpdatedAtMs, TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(winner);
        ArgumentNullException.ThrowIfNull(loser);
        ArgumentNullException.ThrowIfNull(tz);

        var (head, blocks) = Parse(winner);
        var have = Pieces(head, blocks);
        var added = new List<Block>();
        var (loserHead, loserBlocks) = Parse(loser);
        if (loserHead.Length > 0 && have.Add(loserHead))
        {
            // Un a capo nel nome romperebbe il riconoscimento del blocco al giro dopo.
            var who = string.IsNullOrEmpty(loserDevice) ? "?" : loserDevice.ReplaceLineEndings(" ");
            var at = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(loserUpdatedAtMs), tz)
                .ToString(TimeFormat, CultureInfo.InvariantCulture);
            added.Add(new Block($"— {who} · {at} —", who, at, loserHead));
        }

        foreach (var block in loserBlocks)
        {
            if (block.Body.Length > 0 && have.Add(block.Body)) added.Add(block);
        }

        if (added.Count == 0) return winner;

        var sb = new StringBuilder(head);
        var ordered = blocks.Concat(added)
            .OrderBy(b => b.At, StringComparer.Ordinal)
            .ThenBy(b => b.Who, StringComparer.Ordinal)
            .ThenBy(b => b.Body, StringComparer.Ordinal);
        foreach (var block in ordered)
        {
            if (sb.Length > 0) sb.Append(Separator);
            sb.Append(block.Header).Append('\n').Append(block.Body);
        }

        return sb.ToString();
    }

    /// <summary>Quanti blocchi di conflitto contiene il testo.</summary>
    public static int Count(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Marker().Count(text);
    }

    /// <summary>Vero se ogni parte di <paramref name="loser"/> (il testo in cima e il corpo di ogni suo blocco) è già in
    /// <paramref name="text"/>, in cima o come corpo esatto di un blocco: un secondo giro non duplica.</summary>
    public static bool Contains(string text, string loser)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(loser);
        var (head, blocks) = Parse(text);
        var (loserHead, loserBlocks) = Parse(loser);
        return Pieces(head, blocks).IsSupersetOf(Pieces(loserHead, loserBlocks));
    }

    /// <summary>Vero se ogni parte di <paramref name="loser"/> sta già in uno dei testi <paramref name="known"/>: un
    /// testo fatto solo di parti già viste non porta niente di nuovo.</summary>
    public static bool Covers(IEnumerable<string> known, string loser)
    {
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(loser);
        var have = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in known)
        {
            var (head, blocks) = Parse(text);
            have.UnionWith(Pieces(head, blocks));
        }
        var (loserHead, loserBlocks) = Parse(loser);
        return have.IsSupersetOf(Pieces(loserHead, loserBlocks));
    }

    private static (string Head, List<Block> Blocks) Parse(string text)
    {
        var blocks = new List<Block>();
        var matches = Marker().Matches(text);
        if (matches.Count == 0) return (text, blocks);

        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var newline = text.AsSpan(m.Index).IndexOf('\n');
            var lineEnd = newline < 0 ? text.Length : m.Index + newline;
            var bodyStart = newline < 0 ? text.Length : lineEnd + 1;
            var last = i == matches.Count - 1;
            var body = text[bodyStart..(last ? text.Length : matches[i + 1].Index)];
            blocks.Add(new Block(text[m.Index..lineEnd], m.Groups["who"].Value, m.Groups["at"].Value, last ? body : TrimSeparator(body)));
        }

        return (TrimSeparator(text[..matches[0].Index]), blocks);
    }

    private static string TrimSeparator(string s) =>
        s.EndsWith(Separator, StringComparison.Ordinal) ? s[..^Separator.Length] : s.EndsWith('\n') ? s[..^1] : s;

    private static HashSet<string> Pieces(string head, List<Block> blocks)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (head.Length > 0) set.Add(head);
        foreach (var block in blocks)
        {
            if (block.Body.Length > 0) set.Add(block.Body);
        }
        return set;
    }

    private readonly record struct Block(string Header, string Who, string At, string Body);

    [GeneratedRegex(@"^— (?<who>[^\n]*) · (?<at>[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}) —", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex Marker();
}
