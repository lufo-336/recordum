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
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen.Notes;

public sealed partial class NotesViewModel : ObservableObject, IDisposable
{
    private const string ImageFileFilter =
        "Image files (*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp";

    private readonly INoteService _notes;
    private readonly IAttachmentService _attachments;
    private readonly IPhotoImporter _photoImporter;
    private readonly IOpenFileDialog _dialog;
    private readonly IConfirmationDialog _confirm;
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
    // Su un diario le note di prima si mostrano, non se ne creano di nuove.
    [ObservableProperty] private bool _isReadOnlyLegacy;

    // Su un diario le voci sono Note anche loro (categoria Diario): qui farebbero doppione, e cancellarle da questa colonna
    // lascerebbe orfana la registrazione accoppiata. Si elencano solo le note libere.
    private List<Note> Listed(IReadOnlyList<Note> list) =>
        IsReadOnlyLegacy ? list.Where(n => n.Category == NoteCategory.Veloce).ToList() : list.ToList();

    // ---- lightbox (view-only state, flat on this VM — mirrors IsShowingHistory/IsConfirmingDelete
    // rather than a child VM, matching this file's existing style) ----
    [ObservableProperty] private bool _isLightboxOpen;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LightboxOriginalElsewhereText))]
    private AttachmentViewModel? _lightboxAttachment;

    /// <summary>Full photo bytes of <see cref="LightboxAttachment"/>, read from the database when it changes.</summary>
    [ObservableProperty] private byte[]? _lightboxContent;

    /// <summary>Didascalia del visore quando l'originale della foto aperta sta su un altro dispositivo.</summary>
    public string? LightboxOriginalElsewhereText => LightboxAttachment?.OriginalElsewhereText;

    partial void OnLightboxAttachmentChanged(AttachmentViewModel? value)
    {
        LightboxContent = null;
        if (value is not null) _ = LoadLightboxContentAsync(value);
    }

    private async Task LoadLightboxContentAsync(AttachmentViewModel vm)
    {
        var bytes = await _attachments.ReadContentAsync(vm.Id, CancellationToken.None).ConfigureAwait(true);
        // Foto ricevuta senza originale ("Unisci anche le foto" spento altrove): il visore mostra la miniatura.
        if (LightboxAttachment == vm) LightboxContent = bytes is { Length: > 0 } ? bytes : vm.Thumbnail; // not if the lightbox moved on meanwhile
    }

    /// <summary>Prev/Next only make sense with more than one photo.</summary>
    public bool CanNavigateLightbox => Attachments.Count > 1;

    [ObservableProperty] private EntriesRecordsMode _mode = EntriesRecordsMode.Periodo;
    [ObservableProperty] private EntriesPeriod _selectedPeriod = EntriesPeriod.Tutto;
    [ObservableProperty] private DateOnly? _customFrom;
    [ObservableProperty] private DateOnly? _customTo;

    public IEditorAvailability Availability { get; }

    public NoteHistoryViewModel History { get; }

    public NotesViewModel(
        INoteService notes,
        IEventBus events,
        IClock clock,
        IEditorAvailability availability,
        IAttachmentService attachments,
        IPhotoImporter photoImporter,
        IOpenFileDialog dialog,
        IConfirmationDialog confirm)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(photoImporter);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(confirm);
        _notes = notes;
        _clock = clock;
        _attachments = attachments;
        _photoImporter = photoImporter;
        _dialog = dialog;
        _confirm = confirm;
        Availability = availability;
        History = new NoteHistoryViewModel(notes);
        History.Restored += (_, _) => IsShowingHistory = false;
        Attachments.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanNavigateLightbox));
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
        // Si rilegge l'elenco tenendo la selezione e il testo non salvato (ApplyFilter): un salvataggio fatto altrove
        // (la nota del giorno, un'altra nota) non svuota l'editor.
        var list = await _notes.ListByHabitAsync(_habitNodeId).ConfigureAwait(true);
        _all = Listed(list);
        ApplyFilter();
    }

    private DayBoundary _day = DayBoundary.Utc;

    public async Task LoadAsync(Guid habitNodeId, DayBoundary day, CancellationToken ct = default)
    {
        _habitNodeId = habitNodeId;
        _day = day;
        var list = await _notes.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(true);
        _all = Listed(list);
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

    private DateOnly AnchorOf(Note n) => NoteAnchor.For(n, _day);

    private void ApplyFilter()
    {
        IEnumerable<Note> filtered;
        if (Mode == EntriesRecordsMode.Giorno)
        {
            filtered = _calendarDay is { } d ? _all.Where(n => AnchorOf(n) == d) : [];
        }
        else
        {
            var today = _day.Today(_clock);
            var (from, to) = EntriesPeriodRange.Resolve(SelectedPeriod, today, CustomFrom, CustomTo);
            filtered = _all.Where(n =>
            {
                var a = AnchorOf(n);
                return (from is null || a >= from) && (to is null || a <= to);
            });
        }

        // Ricostruire l'elenco non è un cambio di nota: la selezione sopravvive e con lei il testo non salvato; se la nota
        // esce dall'elenco, il testo si salva (mai perso).
        var kept = SelectedNote;
        var draft = IsDirty ? EditorMarkdown : null;
        _rebuilding = true;
        try
        {
            Notes.Clear();
            foreach (var n in filtered.OrderByDescending(n => n.UpdatedAtMs)) Notes.Add(new NoteRowViewModel(n, _day));
            SelectedNote = kept is null ? null : Notes.FirstOrDefault(r => r.Id == kept.Id);
        }
        finally { _rebuilding = false; }
        if (kept is not null && SelectedNote?.Id == kept.Id)
        {
            // La stessa nota (riga nuova, stesso id): allegati e visore restano, l'editor prende la bozza o il testo salvato.
            EditorMarkdown = draft ?? SelectedNote.Note.BodyMd;
            OnPropertyChanged(nameof(IsDirty));
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }
        ApplySelection(SelectedNote);
        if (draft is not null) SaveLeftNote(kept!, draft);
    }

    private bool _rebuilding;

    /// <summary>Cambiare nota non perde il testo: la nota lasciata con modifiche si salva (non c'è una domanda da fare).</summary>
    partial void OnSelectedNoteChanging(NoteRowViewModel? oldValue, NoteRowViewModel? newValue)
    {
        if (_rebuilding || oldValue is null || EditorMarkdown == oldValue.Note.BodyMd) return;
        SaveLeftNote(oldValue, EditorMarkdown);
    }

    /// <summary>Salva le modifiche in sospeso della nota scelta (la colonna che si chiude). No-op senza modifiche.</summary>
    public void SavePending()
    {
        if (IsDirty) SaveLeftNote(SelectedNote!, EditorMarkdown);
    }

    private async void SaveLeftNote(NoteRowViewModel row, string body)
    {
        try
        {
            // Dopo il cambio di nota, non dentro: il bus pubblica NoteUpdated in modo sincrono e la rilettura che ne segue
            // vedrebbe ancora la vecchia selezione a metà del setter (misurato: la selezione finiva a null).
            await Task.Yield();
            await PersistAsync(row, body, CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // fire-and-forget da un setter: l'errore si registra, non deve abbattere l'app
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SaveLeftNote failed: {ex}"); }
#pragma warning restore CA1031
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
        if (IsReadOnlyLegacy) return;
        var draft = new Note { HabitNodeId = _habitNodeId, BodyMd = string.Empty, UpdatedAtMs = _clock.UtcNowMs };
        await _notes.CreateAsync(draft, ct).ConfigureAwait(true);
        _all.Insert(0, draft);
        var row = new NoteRowViewModel(draft, _day);
        Notes.Insert(0, row);
        SelectedNote = row;
    }

    private bool CanSave() => IsDirty;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save(CancellationToken ct)
    {
        if (SelectedNote is not { } row) return;
        await PersistAsync(row, EditorMarkdown, ct).ConfigureAwait(true);
    }

    private async Task PersistAsync(NoteRowViewModel row, string body, CancellationToken ct)
    {
        await _notes.UpdateAsync(row.Id, n => n.BodyMd = body, ct).ConfigureAwait(true);
        row.Note.BodyMd = body;
        row.Note.UpdatedAtMs = _clock.UtcNowMs;
        row.Refresh();
        OnPropertyChanged(nameof(IsDirty));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedNoteChanged(NoteRowViewModel? value)
    {
        if (!_rebuilding) ApplySelection(value);   // a elenco ricostruito ci pensa ApplyFilter (o ConfirmDelete)
    }

    private void ApplySelection(NoteRowViewModel? value)
    {
        EditorMarkdown = value?.Note.BodyMd ?? string.Empty;
        OnPropertyChanged(nameof(IsDirty));
        SaveCommand.NotifyCanExecuteChanged();

        Attachments.Clear();
        CloseLightbox();
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
        var items = new List<AttachmentViewModel>();
        foreach (var a in list)
            items.Add(new AttachmentViewModel(a, await _attachments.ReadThumbnailAsync(a.Id, ct).ConfigureAwait(true)));
        if (SelectedNote?.Id != noteId) return; // moved on while reading the thumbnails
        Attachments.Clear();
        foreach (var vm in items) Attachments.Add(vm);
    }

    [RelayCommand]
    private async Task AddAttachment(CancellationToken ct)
    {
        if (SelectedNote is not { } row) return;
        var noteId = row.Id;
        var paths = await _dialog.PromptMultipleAsync(ImageFileFilter);
        foreach (var path in paths)
        {
            try
            {
                var photo = await _photoImporter.ImportAsync(path, ct).ConfigureAwait(true);
                var attachment = await _attachments.AddAsync(noteId, photo, ct).ConfigureAwait(true);
                // The add itself (File.Copy + SaveChangesAsync) is a real await boundary — if the
                // user switched to a different note mid-batch, this note's Attachments collection
                // has already been cleared/reloaded for the new selection. Still persist the file
                // to the originally-selected note (the user's intent), just don't let it land in a
                // list that now represents a different note; a later reselect reloads it correctly.
                if (SelectedNote?.Id == noteId)
                    Attachments.Add(new AttachmentViewModel(attachment, photo.Thumbnail));
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
        var ok = await _confirm.ConfirmAsync(
            Strings.Notes_Attachments_DeleteConfirm_Title, Strings.Notes_Attachments_DeleteConfirm_Message).ConfigureAwait(true);
        if (!ok) return;
        await _attachments.DeleteAsync(vm.Id, ct).ConfigureAwait(true);
        Attachments.Remove(vm);
        // Deleting the photo currently open in the lightbox must close it — nothing left to show.
        if (LightboxAttachment == vm) CloseLightbox();
    }

    [RelayCommand]
    private void OpenLightbox(AttachmentViewModel? attachment)
    {
        if (attachment is null) return;
        LightboxAttachment = attachment;
        IsLightboxOpen = true;
    }

    [RelayCommand]
    private void CloseLightbox()
    {
        IsLightboxOpen = false;
        LightboxAttachment = null;
    }

    [RelayCommand]
    private void LightboxNext() => StepLightbox(1);

    [RelayCommand]
    private void LightboxPrevious() => StepLightbox(-1);

    private void StepLightbox(int direction)
    {
        if (Attachments.Count == 0 || LightboxAttachment is not { } current) return;
        var idx = Attachments.IndexOf(current);
        if (idx < 0) return;
        var nextIdx = (idx + direction + Attachments.Count) % Attachments.Count;
        LightboxAttachment = Attachments[nextIdx];
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
        // L'indice prima di attendere: l'evento NoteDeleted può far rileggere l'elenco (righe nuove) nel frattempo.
        var idx = Notes.IndexOf(row);
        await _notes.DeleteAsync(row.Id, ct).ConfigureAwait(true);
        _all.RemoveAll(n => n.Id == row.Id);
        _rebuilding = true;   // la nota eliminata non si salva lasciandola
        try
        {
            if (Notes.FirstOrDefault(r => r.Id == row.Id) is { } shown) Notes.Remove(shown);
            SelectedNote = Notes.Count > 0 ? Notes[Math.Clamp(idx, 0, Notes.Count - 1)] : null;
        }
        finally { _rebuilding = false; }
        ApplySelection(SelectedNote);
        IsConfirmingDelete = false;
    }

    public void Dispose()
    {
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
    }
}
