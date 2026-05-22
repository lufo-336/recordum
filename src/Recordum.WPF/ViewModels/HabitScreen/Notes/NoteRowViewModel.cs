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
            if (string.IsNullOrWhiteSpace(Note.BodyMd)) return "(untitled)";
            var firstLine = Note.BodyMd.Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => !string.IsNullOrEmpty(l)) ?? "(untitled)";
            return firstLine.Length <= 40 ? firstLine : firstLine[..40] + "…";
        }
    }

    public string UpdatedAtDisplay =>
        DateTimeOffset.FromUnixTimeMilliseconds(Note.UpdatedAtMs)
            .UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public void Refresh()
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(UpdatedAtDisplay));
    }
}
