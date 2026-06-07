using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Bootstrap;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

public sealed partial class NotesViewModel : ObservableObject, IDisposable
{
    private readonly INoteService _notes;
    private readonly IClock _clock;
    private readonly List<IDisposable> _subs = [];
    private Guid _habitNodeId;

    public ObservableCollection<NoteRowViewModel> Notes { get; } = [];

    [ObservableProperty] private NoteRowViewModel? _selectedNote;
    [ObservableProperty] private string _editorMarkdown = string.Empty;
    [ObservableProperty] private bool _isConfirmingDelete;
    [ObservableProperty] private bool _isShowingHistory;

    public IWebView2Availability Availability { get; }

    public NoteHistoryViewModel History { get; }

    public NotesViewModel(INoteService notes, IEventBus events, IClock clock, IWebView2Availability availability)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(availability);
        _notes = notes;
        _clock = clock;
        Availability = availability;
        History = new NoteHistoryViewModel(notes);
        History.Restored += (_, _) => IsShowingHistory = false;
        _subs.Add(events.Subscribe(WellKnownEvents.NoteCreated, OnNoteEvent));
        _subs.Add(events.Subscribe(WellKnownEvents.NoteUpdated, OnNoteEvent));
        _subs.Add(events.Subscribe(WellKnownEvents.NoteDeleted, OnNoteEvent));
    }

    private async void OnNoteEvent(EventEnvelope env)
    {
        var habitId = env.Payload switch
        {
            NoteCreatedPayload p => p.HabitNodeId,
            NoteUpdatedPayload p => p.HabitNodeId,
            NoteDeletedPayload p => p.HabitNodeId,
            _ => Guid.Empty,
        };
        if (habitId != _habitNodeId) return;
        var keptSelection = SelectedNote?.Id;
        await LoadAsync(_habitNodeId).ConfigureAwait(true);
        if (keptSelection is { } sel)
            SelectedNote = Notes.FirstOrDefault(r => r.Id == sel);
    }

    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        _habitNodeId = habitNodeId;
        var list = await _notes.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(true);
        Notes.Clear();
        foreach (var n in list) Notes.Add(new NoteRowViewModel(n));
        SelectedNote = null;
        EditorMarkdown = string.Empty;
    }

    public bool IsDirty => SelectedNote is { } row && EditorMarkdown != row.Note.BodyMd;

    [RelayCommand]
    private async Task New(CancellationToken ct)
    {
        var draft = new Note { HabitNodeId = _habitNodeId, BodyMd = string.Empty, UpdatedAtMs = _clock.UtcNowMs };
        await _notes.CreateAsync(draft, ct).ConfigureAwait(true);
        var row = new NoteRowViewModel(draft);
        Notes.Insert(0, row);
        SelectedNote = row;
    }

    private bool CanSave() => IsDirty;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save(CancellationToken ct)
    {
        if (SelectedNote is not { } row) return;
        var newBody = EditorMarkdown;
        await _notes.UpdateAsync(row.Id, n => n.BodyMd = newBody, ct).ConfigureAwait(true);
        row.Note.BodyMd = newBody;
        row.Note.UpdatedAtMs = _clock.UtcNowMs;
        row.Refresh();
        OnPropertyChanged(nameof(IsDirty));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedNoteChanged(NoteRowViewModel? value)
    {
        EditorMarkdown = value?.Note.BodyMd ?? string.Empty;
        OnPropertyChanged(nameof(IsDirty));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnEditorMarkdownChanged(string value)
    {
        OnPropertyChanged(nameof(IsDirty));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task ShowHistory(NoteRowViewModel? row, CancellationToken ct)
    {
        if (row is null) return;
        SelectedNote = row;
        await History.LoadAsync(row.Id, ct).ConfigureAwait(true);
        IsShowingHistory = true;
    }

    [RelayCommand]
    private void HideHistory() => IsShowingHistory = false;

    [RelayCommand]
    private void RequestDelete() => IsConfirmingDelete = true;

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    [RelayCommand]
    private async Task ConfirmDelete(CancellationToken ct)
    {
        if (SelectedNote is not { } row) { IsConfirmingDelete = false; return; }
        await _notes.DeleteAsync(row.Id, ct).ConfigureAwait(true);
        var idx = Notes.IndexOf(row);
        Notes.RemoveAt(idx);
        SelectedNote = Notes.Count > 0 ? Notes[Math.Min(idx, Notes.Count - 1)] : null;
        IsConfirmingDelete = false;
    }

    public void Dispose()
    {
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
    }
}
