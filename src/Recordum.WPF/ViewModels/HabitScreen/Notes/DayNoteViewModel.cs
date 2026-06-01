using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

/// <summary>
/// Brief editor for the note attached to a single calendar day (the unified Note with a TargetDate).
/// Lives in the Abitudine tab; the rich markdown editor for the same notes is the Notes column.
/// </summary>
public sealed partial class DayNoteViewModel : ObservableObject
{
    private readonly INoteService _notes;
    private readonly IClock _clock;
    private Guid _habitNodeId;
    private Note? _current;

    public DayNoteViewModel(INoteService notes, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(clock);
        _notes = notes;
        _clock = clock;
    }

    [ObservableProperty] private DateOnly? _date;
    [ObservableProperty] private bool _hasDate;
    [ObservableProperty] private string _text = string.Empty;

    public void SetContext(Guid habitNodeId)
    {
        _habitNodeId = habitNodeId;
        _current = null;
        Date = null;
        HasDate = false;
        Text = string.Empty;
    }

    /// <summary>Loads the (most recent) note anchored to <paramref name="date"/> into the editor, or an empty editor.</summary>
    public async Task LoadDayAsync(DateOnly date, CancellationToken ct = default)
    {
        Date = date;
        HasDate = true;
        var list = await _notes.ListByHabitAndDateAsync(_habitNodeId, date, ct).ConfigureAwait(true);
        _current = list.Count > 0 ? list[0] : null;
        Text = _current?.BodyMd ?? string.Empty;
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Save is enabled only when a day is selected and the text differs from what's stored
    /// (and, for a brand-new note, is non-empty).</summary>
    public bool CanSave =>
        HasDate && (_current is null ? !string.IsNullOrWhiteSpace(Text) : Text != _current.BodyMd);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (Date is not { } d) return;
        if (_current is null)
        {
            var draft = new Note
            {
                HabitNodeId = _habitNodeId,
                BodyMd = Text,
                TargetDate = d,
                UpdatedAtMs = _clock.UtcNowMs,
            };
            await _notes.CreateAsync(draft, ct).ConfigureAwait(true);
            _current = draft;
        }
        else
        {
            var body = Text;
            await _notes.UpdateAsync(_current.Id, n => n.BodyMd = body, ct).ConfigureAwait(true);
            _current.BodyMd = body;
        }
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasDateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }
}
