using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitScreen.Notes;

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
    private Task? _saving;

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
    [ObservableProperty] private string _summaryText = string.Empty;

    /// <summary>Lucide icon resource key for the done-state glyph (CheckCircle when done), or null
    /// when not done — rendered as a line icon instead of a text glyph (design brief).</summary>
    [ObservableProperty] private string? _summaryIconKey;

    [ObservableProperty] private bool _showEditor = true;

    public void SetContext(Guid habitNodeId) => SetContext(habitNodeId, isJournal: false);

    /// <summary>For journals the QuickAdd markdown editor is the diary editor, so the brief day-note
    /// editor is hidden (ShowEditor = false). For other types it stays as the quick-note editor.</summary>
    public void SetContext(Guid habitNodeId, bool isJournal)
    {
        _habitNodeId = habitNodeId;
        _current = null;
        Date = null;
        HasDate = false;
        Text = string.Empty;
        SummaryText = string.Empty;
        SummaryIconKey = null;
        ShowEditor = !isJournal;
    }

    /// <summary>The host supplies the day's summary (done-state + magnitude + note count) pre-formatted in the domain layer.</summary>
    public void SetDaySummary(bool done, string? magnitudeLabel, int noteCount)
    {
        if (!done)
        {
            SummaryIconKey = null;
            SummaryText = Strings.Abitudine_DaySummary_NotDone;
            return;
        }
        SummaryIconKey = "Icon.CheckCircle";
        var parts = new List<string> { Strings.Abitudine_DaySummary_Done };
        if (!string.IsNullOrEmpty(magnitudeLabel)) parts.Add(magnitudeLabel);
#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
        if (noteCount > 0) parts.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Abitudine_DaySummary_Notes, noteCount));
#pragma warning restore CA1863
        SummaryText = string.Join(" · ", parts);
    }

    /// <summary>Loads the (most recent) note anchored to <paramref name="date"/> into the editor, or an empty editor.</summary>
    public async Task LoadDayAsync(DateOnly date, CancellationToken ct = default)
    {
        if (_saving is { IsCompleted: false } saving) await saving.ConfigureAwait(true);
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
        if (_saving is { IsCompleted: false } saving)
        {
            await saving.ConfigureAwait(true);
            if (!CanSave) return;
        }
        _saving = SaveCoreAsync(ct);
        await _saving.ConfigureAwait(true);
    }

    private async Task SaveCoreAsync(CancellationToken ct)
    {
        if (Date is not { } d) return;
        if (_current is not { } current)
        {
            var draft = new Note
            {
                HabitNodeId = _habitNodeId,
                BodyMd = Text,
                TargetDate = d,
                UpdatedAtMs = _clock.UtcNowMs,
            };
            await _notes.CreateAsync(draft, ct).ConfigureAwait(true);
            if (Date == d) _current = draft;
        }
        else
        {
            var body = Text;
            await _notes.UpdateAsync(current.Id, n => n.BodyMd = body, ct).ConfigureAwait(true);
            if (Date == d) current.BodyMd = body;
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
