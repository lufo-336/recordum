using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

public sealed partial class NoteRowViewModel(Note note) : ObservableObject
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

    /// <summary>Day this note is anchored to (e.g. "1 Jun"), or the "General" label for un-anchored notes.</summary>
    public string DateBadge =>
        Note.TargetDate is { } d
            ? d.ToString("d MMM", CultureInfo.CurrentCulture)
            : Recordum.WPF.Resources.Strings.Notes_Badge_General;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(UpdatedAtDisplay));
        OnPropertyChanged(nameof(DateBadge));
    }
}
