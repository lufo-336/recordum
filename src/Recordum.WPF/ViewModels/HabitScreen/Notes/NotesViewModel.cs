using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Services;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Services;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

public sealed partial class NotesViewModel : ObservableObject, IDisposable
{
    private const string ImageFileFilter =
        "Image files (*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp";

    private readonly INoteService _notes;
    private readonly IAttachmentService _attachments;
    private readonly IOpenFileDialog _dialog;
    private readonly IClock _clock;
    private readonly List<IDisposable> _subs = [];
    private Guid _habitNodeId;
    private List<Note> _all = [];
    private DateOnly? _calendarDay;

    public ObservableCollection<NoteRowViewModel> Notes { get; } = [];
    public ObservableCollection<AttachmentViewModel> Attachments { get; } = [];

    [ObservableProperty] private NoteRowViewModel? _selectedNote;
    [ObservableProperty] private string _editorMarkdown = string.Empty;
    [ObservableProperty] private bool _isConfirmingDelete;
    [ObservableProperty] private bool _isShowingHistory;

    [ObservableProperty] private EntriesRecordsMode _mode = EntriesRecordsMode.Periodo;
    [ObservableProperty] private EntriesPeriod _selectedPeriod = EntriesPeriod.Tutto;
    [ObservableProperty] private DateOnly? _customFrom;
    [ObservableProperty] private DateOnly? _customTo;

    public IWebView2Availability Availability { get; }

    public NoteHistoryViewModel History { get; }

    public NotesViewModel(
        INoteService notes,
        IEventBus events,
        IClock clock,
        IWebView2Availability availability,
        IAttachmentService attachments,
        IOpenFileDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(dialog);
        _notes = notes;
        _clock = clock;
        _attachments = attachments;
        _dialog = dialog;
        Availability = availability;
        History = new NoteHistoryViewModel(notes);
        History.Restored += (_, _) => IsShowingHistory = false;
        _subs.Add(events.SubscribeOnUi(WellKnownEvents.NoteCreated, OnNoteEvent));
        _subs.Add(events.SubscribeOnUi(WellKnownEvents.NoteUpdated, OnNoteEvent));
        _subs.Add(events.SubscribeOnUi(WellKnownEvents.NoteDeleted, OnNoteEvent));
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
        _all = list.ToList();
        ApplyFilter();
        SelectedNote = null;
        EditorMarkdown = string.Empty;
    }

    /// <summary>Mirrors the calendar selection into Day mode (parallels HabitRecordsViewModel).</summary>
    public void SetCalendarDay(DateOnly day)
    {
        _calendarDay = day;
        if (Mode == EntriesRecordsMode.Giorno) ApplyFilter();
    }

    private static DateOnly AnchorOf(Note n) => n.TargetDate ?? DateOnly.FromDateTime(n.CreatedAt);

    private void ApplyFilter()
    {
        IEnumerable<Note> filtered;
        if (Mode == EntriesRecordsMode.Giorno)
        {
            filtered = _calendarDay is { } d ? _all.Where(n => AnchorOf(n) == d) : [];
        }
        else
        {
            var today = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow.UtcDateTime, _clock.LocalTimeZone));
            var (from, to) = EntriesPeriodRange.Resolve(SelectedPeriod, today, CustomFrom, CustomTo);
            filtered = _all.Where(n =>
            {
                var a = AnchorOf(n);
                return (from is null || a >= from) && (to is null || a <= to);
            });
        }

        var keptSelection = SelectedNote?.Id;
        Notes.Clear();
        foreach (var n in filtered.OrderByDescending(n => n.UpdatedAtMs)) Notes.Add(new NoteRowViewModel(n));
        if (keptSelection is { } sel) SelectedNote = Notes.FirstOrDefault(r => r.Id == sel);
    }

    partial void OnModeChanged(EntriesRecordsMode value) => ApplyFilter();
    partial void OnSelectedPeriodChanged(EntriesPeriod value) { if (Mode == EntriesRecordsMode.Periodo) ApplyFilter(); }
    partial void OnCustomFromChanged(DateOnly? value) => ReloadIfCustom();
    partial void OnCustomToChanged(DateOnly? value) => ReloadIfCustom();

    private void ReloadIfCustom()
    {
        if (Mode == EntriesRecordsMode.Periodo && SelectedPeriod == EntriesPeriod.Personalizzato) ApplyFilter();
    }

    public bool IsDirty => SelectedNote is { } row && EditorMarkdown != row.Note.BodyMd;

    [RelayCommand]
    private async Task New(CancellationToken ct)
    {
        var draft = new Note { HabitNodeId = _habitNodeId, BodyMd = string.Empty, UpdatedAtMs = _clock.UtcNowMs };
        await _notes.CreateAsync(draft, ct).ConfigureAwait(true);
        _all.Insert(0, draft);
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

        Attachments.Clear();
        // Fire-and-forget: note switching in this tab is a deliberate user click (not the
        // rapid-fire habit-tree navigation CR14/CR18/CR19 added generation guards for), so a
        // plain reload is enough — the note list itself is small and each load is a single
        // indexed query. If this tab ever grows fast programmatic note switching, revisit.
        if (value is not null) _ = LoadAttachmentsAsync(value.Id, CancellationToken.None);
    }

    private async Task LoadAttachmentsAsync(Guid noteId, CancellationToken ct)
    {
        var list = await _attachments.ListByNoteAsync(noteId, ct).ConfigureAwait(true);
        // Guard against a stale reload landing after the selection moved on again.
        if (SelectedNote?.Id != noteId) return;
        Attachments.Clear();
        foreach (var a in list) Attachments.Add(new AttachmentViewModel(a, _attachments));
    }

    [RelayCommand]
    private async Task AddAttachment(CancellationToken ct)
    {
        if (SelectedNote is not { } row) return;
        var paths = _dialog.PromptMultiple(ImageFileFilter);
        foreach (var path in paths)
        {
            try
            {
                var attachment = await _attachments.AddAsync(row.Id, path, ct).ConfigureAwait(true);
                Attachments.Add(new AttachmentViewModel(attachment, _attachments));
            }
#pragma warning disable CA1031 // one bad file (unreadable/moved) must not abort the rest of the batch
            catch (Exception)
#pragma warning restore CA1031
            {
                // Best-effort batch add — no per-file UI surface for this yet (Task 5 gallery/lightbox
                // work adds the richer UI); skipping the failed file and continuing is acceptable here.
            }
        }
    }

    [RelayCommand]
    private async Task DeleteAttachment(AttachmentViewModel? vm, CancellationToken ct)
    {
        if (vm is null) return;
        await _attachments.DeleteAsync(vm.Id, ct).ConfigureAwait(true);
        Attachments.Remove(vm);
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
        _all.RemoveAll(n => n.Id == row.Id);
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
