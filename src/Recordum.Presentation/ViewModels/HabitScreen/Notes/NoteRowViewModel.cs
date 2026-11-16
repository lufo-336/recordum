using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen.Notes;

public sealed partial class NoteRowViewModel(Note note, DayBoundary day) : ObservableObject
{
    public Note Note { get; } = note;
    public Guid Id => Note.Id;

    private string FirstNonEmptyLine() =>
        Note.BodyMd.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => !string.IsNullOrEmpty(l)) ?? Recordum.Presentation.Resources.Strings.Notes_Untitled;

    public string Summary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Note.BodyMd)) return Recordum.Presentation.Resources.Strings.Notes_Untitled;
            var firstLine = FirstNonEmptyLine();
            return firstLine.Length <= 40 ? firstLine : firstLine[..40] + "…";
        }
    }

    /// <summary>Prima riga non vuota intera: tagliarla spezzerebbe i marcatori Markdown nella resa della riga.</summary>
    public string PreviewMarkdown =>
        string.IsNullOrWhiteSpace(Note.BodyMd)
            ? Recordum.Presentation.Resources.Strings.Notes_Untitled
            : FirstNonEmptyLine();

    public string UpdatedAtDisplay =>
        DateTimeOffset.FromUnixTimeMilliseconds(Note.UpdatedAtMs)
            .UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private long CreatedAtMs =>
        new DateTimeOffset(DateTime.SpecifyKind(Note.CreatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    /// <summary>Stable anchor: assigned calendar day, else creation day. Never changes on edit.</summary>
    public string AnchorDateDisplay =>
        NoteAnchor.For(Note, day)
            .ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    public bool IsDiario => Note.Category == NoteCategory.Diario;

    /// <summary>Time-of-day the note was written (HH:mm), from CreatedAt.</summary>
    public string CreatedAtTimeDisplay =>
        DateTime.SpecifyKind(Note.CreatedAt, DateTimeKind.Utc)
            .ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>True once the note has been edited after creation (1s tolerance for clock granularity).</summary>
    public bool WasModified => Note.UpdatedAtMs > CreatedAtMs + 1000;

#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
    public string ModifiedAtDisplay => string.Format(
        CultureInfo.CurrentCulture,
        Recordum.Presentation.Resources.Strings.Notes_ModifiedAt,
        DateTimeOffset.FromUnixTimeMilliseconds(Note.UpdatedAtMs).UtcDateTime
            .ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture));
#pragma warning restore CA1863

    public void Refresh()
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(PreviewMarkdown));
        OnPropertyChanged(nameof(AnchorDateDisplay));
        OnPropertyChanged(nameof(WasModified));
        OnPropertyChanged(nameof(ModifiedAtDisplay));
        OnPropertyChanged(nameof(UpdatedAtDisplay));
    }
}
