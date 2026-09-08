using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

public sealed partial class NoteRowViewModel(Note note, DayBoundary day) : ObservableObject
{
    public Note Note { get; } = note;
    public Guid Id => Note.Id;

    public string Summary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Note.BodyMd)) return Recordum.WPF.Resources.Strings.Notes_Untitled;
            var firstLine = Note.BodyMd.Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => !string.IsNullOrEmpty(l)) ?? Recordum.WPF.Resources.Strings.Notes_Untitled;
            return firstLine.Length <= 40 ? firstLine : firstLine[..40] + "…";
        }
    }

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

    /// <summary>Chip label: "Diary" for paired diary entries, "Quick note" for free notes.</summary>
    public string CategoryLabel => IsDiario
        ? Recordum.WPF.Resources.Strings.Notes_Category_Diary
        : Recordum.WPF.Resources.Strings.Notes_Category_Quick;

    /// <summary>Time-of-day the note was written (HH:mm), from CreatedAt.</summary>
    public string CreatedAtTimeDisplay =>
        DateTime.SpecifyKind(Note.CreatedAt, DateTimeKind.Utc)
            .ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>True once the note has been edited after creation (1s tolerance for clock granularity).</summary>
    public bool WasModified => Note.UpdatedAtMs > CreatedAtMs + 1000;

#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
    public string ModifiedAtDisplay => string.Format(
        CultureInfo.CurrentCulture,
        Recordum.WPF.Resources.Strings.Notes_ModifiedAt,
        DateTimeOffset.FromUnixTimeMilliseconds(Note.UpdatedAtMs).UtcDateTime
            .ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture));
#pragma warning restore CA1863

    public void Refresh()
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(AnchorDateDisplay));
        OnPropertyChanged(nameof(WasModified));
        OnPropertyChanged(nameof(ModifiedAtDisplay));
        OnPropertyChanged(nameof(UpdatedAtDisplay));
    }
}
