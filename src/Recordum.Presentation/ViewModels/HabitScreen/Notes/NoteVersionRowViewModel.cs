using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services.Dtos;

namespace Recordum.Presentation.ViewModels.HabitScreen.Notes;

public sealed partial class NoteVersionRowViewModel(NoteVersion version, bool isCurrent) : ObservableObject
{
    public NoteVersion Version { get; } = version;
    public Guid Id => Version.Id;
    public string BodyMd => Version.BodyMd;
    public bool IsCurrent { get; } = isCurrent;

    public string TimestampDisplay
    {
        get
        {
            var ts = Version.CreatedAt.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);
            return IsCurrent ? $"{ts} · {Recordum.Presentation.Resources.Strings.Notes_History_Current}" : ts;
        }
    }

    public string Summary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BodyMd)) return Recordum.Presentation.Resources.Strings.Notes_Untitled;
            var firstLine = BodyMd.Split('\n').Select(l => l.Trim())
                .FirstOrDefault(l => !string.IsNullOrEmpty(l)) ?? Recordum.Presentation.Resources.Strings.Notes_Untitled;
            return firstLine.Length <= 40 ? firstLine : firstLine[..40] + "…";
        }
    }
}
