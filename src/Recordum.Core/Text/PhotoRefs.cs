using System.Text.RegularExpressions;

namespace Recordum.Core.Text;

/// <summary>Un riferimento a una foto nel testo: <paramref name="Start"/>/<paramref name="Length"/> coprono tutta
/// la sintassi <c>![nome](photo:id)</c>.</summary>
public readonly record struct PhotoRef(int Start, int Length, Guid Id);

/// <summary>I riferimenti alle foto allegate dentro il testo di una voce: <c>![nome](photo:&lt;id:N&gt;)</c>.
/// Dentro il codice (inline o a blocchi) non contano, come per <see cref="InlineTags"/>.</summary>
public static partial class PhotoRefs
{
    [GeneratedRegex(@"```.*?(```|\z)", RegexOptions.Singleline)]
    private static partial Regex FencedCode();

    [GeneratedRegex(@"`[^`\n]*`")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"!\[[^\]\r\n]*\]\(photo:([0-9a-fA-F]{32})\)")]
    private static partial Regex Candidate();

    public static string Format(Guid id, string filename)
    {
        var name = filename.Replace("]", "").Replace("\r", "").Replace("\n", "");
        return $"![{name}](photo:{id:N})";
    }

    public static IReadOnlyList<PhotoRef> Find(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return [];
        // Il codice si maschera con spazi della stessa lunghezza: gli indici restano quelli del testo originale.
        var text = InlineCode().Replace(FencedCode().Replace(markdown, Blank), Blank);
        return [.. Candidate().Matches(text).Select(m => new PhotoRef(m.Index, m.Length, Guid.ParseExact(m.Groups[1].Value, "N")))];
    }

    public static string Replace(string markdown, Func<PhotoRef, string> replacement)
    {
        var refs = Find(markdown);
        if (refs.Count == 0) return markdown;
        var sb = new System.Text.StringBuilder(markdown.Length);
        var pos = 0;
        foreach (var r in refs)
        {
            sb.Append(markdown, pos, r.Start - pos).Append(replacement(r));
            pos = r.Start + r.Length;
        }
        return sb.Append(markdown, pos, markdown.Length - pos).ToString();
    }

    private static string Blank(Match m) => new(' ', m.Length);
}
