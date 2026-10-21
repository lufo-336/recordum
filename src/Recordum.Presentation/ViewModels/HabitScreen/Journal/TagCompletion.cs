using Recordum.Core.Text;

namespace Recordum.Presentation.ViewModels.HabitScreen.Journal;

/// <summary>La parola <c>#…</c> sotto il cursore: <paramref name="Start"/> è la posizione del <c>#</c>.</summary>
public readonly record struct TagQuery(int Start, string Prefix);

/// <summary>Completamento dei tag nel testo di una voce. Stesse regole di <see cref="InlineTags"/> per cosa è una
/// parola-tag (<c>#</c> a inizio parola, poi lettere, cifre, <c>_</c>, <c>-</c>). Non guarda il codice: dentro un blocco
/// di codice il menu può comparire, ma <see cref="InlineTags.Parse"/> poi non lo conterà come tag.</summary>
public static class TagCompletion
{
    // Gli stessi caratteri che possono precedere il '#' nel Candidate() di InlineTags.
    private const string OpeningChars = "([{\"'«“";

    public static TagQuery? Find(string text, int caret)
    {
        if (string.IsNullOrEmpty(text) || caret < 1 || caret > text.Length) return null;
        var i = caret;
        while (i > 0 && IsTagChar(text[i - 1])) i--;
        if (i == 0 || text[i - 1] != '#') return null;
        var hash = i - 1;
        if (hash > 0 && !char.IsWhiteSpace(text[hash - 1]) && !OpeningChars.Contains(text[hash - 1])) return null;
        return new TagQuery(hash, text[i..caret]);
    }

    /// <summary>Sostituisce la parola con <c>#tagName </c>; il cursore va dopo lo spazio.</summary>
    public static (string Text, int Caret) Apply(string text, TagQuery query, string tagName)
    {
        var end = query.Start + 1 + query.Prefix.Length;
        var insert = $"#{tagName} ";
        return (text[..query.Start] + insert + text[end..], query.Start + insert.Length);
    }

    private static bool IsTagChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-';
}
