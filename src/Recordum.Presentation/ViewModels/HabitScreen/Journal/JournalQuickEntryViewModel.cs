using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen.Journal;

/// <summary>Composer "nota veloce" della colonna Diario (Onda 2.4, U7). Ogni salvataggio e' una
/// voce di diario vera — Nota(Diario) + Completion via IDiaryService — cosi' streak, heatmap e
/// cifra chiave funzionano senza semantica nuova. Le foto restano percorsi in memoria finche' non
/// esiste il noteId: allegare prima vorrebbe dire creare una nota-bozza che, se abbandoni, resta
/// orfana.</summary>
public sealed partial class JournalQuickEntryViewModel : ObservableObject
{
    private const string ImageFileFilter =
        "Image files (*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp";

    private readonly IDiaryService _diary;
    private readonly INoteService _notes;
    private readonly IAttachmentService _attachments;
    private readonly IPhotoImporter _photoImporter;
    private readonly IOpenFileDialog _dialog;
    private readonly IConfirmationDialog _confirm;
    private readonly IClock _clock;

    private Guid _habitNodeId;
    private DateOnly _day;
    // Bumped by every ReloadEntriesAsync call; a call that finds itself superseded (a newer one
    // started while it was awaiting) bails instead of touching Entries — the CR18/CR19 class of guard,
    // needed here because SetDay fires ReloadEntriesAsync without awaiting it (I4).
    private int _reloadGeneration;

    [ObservableProperty] private string _draft = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isSaving;

    /// <summary>Percorsi scelti e non ancora allegati. Diventano allegati solo dopo il salvataggio.</summary>
    public ObservableCollection<string> PendingPhotos { get; } = [];

    public ObservableCollection<JournalEntryRowViewModel> Entries { get; } = [];

    public JournalQuickEntryViewModel(
        IDiaryService diary,
        INoteService notes,
        IAttachmentService attachments,
        IPhotoImporter photoImporter,
        IOpenFileDialog dialog,
        IConfirmationDialog confirm,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(diary);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(photoImporter);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(clock);
        _diary = diary;
        _notes = notes;
        _attachments = attachments;
        _photoImporter = photoImporter;
        _dialog = dialog;
        _confirm = confirm;
        _clock = clock;
        _day = Today();
    }

    private DateOnly Today() => _dayBoundary.Today(_clock);

    private DayBoundary _dayBoundary = DayBoundary.Utc;

    public async Task LoadAsync(HabitNode node, DayBoundary dayBoundary, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        _habitNodeId = node.Id;
        // Il confine arriva dallo screen: va impostato PRIMA di ri-derivare il giorno corrente,
        // altrimenti il composer si apre sul giorno che diceva il vecchio confine.
        _dayBoundary = dayBoundary;
        _day = Today();
        Draft = string.Empty;
        ErrorMessage = null;
        PendingPhotos.Clear();
        await ReloadEntriesAsync(ct).ConfigureAwait(true);
    }

    /// <summary>Allinea il composer al giorno selezionato nel calendario di Cronologia — e' cosi'
    /// che si registra la cena di ieri, senza un secondo selettore di data. Fire-and-forget (nessun
    /// caller ha un CancellationToken da propagare da un click calendario), quindi guardato: una
    /// reload che scopre di essere stata superata da una piu' recente si ritira invece di sovrascrivere
    /// Entries con un mix di due giorni (I4), e un fallimento si traccia invece di sparire nel nulla.</summary>
    public void SetDay(DateOnly day)
    {
        _day = day;
        _ = ReloadEntriesGuardedAsync();
    }

    private async Task ReloadEntriesGuardedAsync()
    {
        try
        {
            await ReloadEntriesAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // fire-and-forget da SetDay: non c'e' nessun caller da cui propagare
        catch (Exception ex)
#pragma warning restore CA1031
        {
            System.Diagnostics.Debug.WriteLine($"JournalQuickEntryViewModel.ReloadEntriesAsync failed: {ex}");
        }
    }

    private async Task ReloadEntriesAsync(CancellationToken ct)
    {
        if (_habitNodeId == Guid.Empty) return;
        var generation = ++_reloadGeneration;
        // Capture the day this reload is FOR. Reading _day after the await would silently re-target a
        // superseded reload at whatever day is current by then — which also made the interleaving
        // regression test pass against the pre-guard code, since the late reload rebuilt the *new*
        // day's rows and looked correct (W24-R2).
        var day = _day;

        var all = await _notes.ListByHabitAsync(_habitNodeId, ct).ConfigureAwait(true);
        if (generation != _reloadGeneration) return; // superseded while awaiting the note list

        var ofDay = all
            .Where(n => n.Category == NoteCategory.Diario
                        && NoteAnchor.For(n, _dayBoundary) == day)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        // Build the day's rows in a local first — Entries is only ever touched once, atomically,
        // by whichever reload turns out to still be current. Two overlapping SetDay calls used to
        // interleave through a shared Entries.Clear() (I4); this can't, because neither call's
        // partial state is ever visible to the other.
        var rows = new List<JournalEntryRowViewModel>();
        foreach (var n in ofDay)
        {
            var row = new JournalEntryRowViewModel(n);
            foreach (var a in await _attachments.ListByNoteAsync(n.Id, ct).ConfigureAwait(true))
                row.Photos.Add(new AttachmentViewModel(a, await _attachments.ReadThumbnailAsync(a.Id, ct).ConfigureAwait(true)));
            if (generation != _reloadGeneration) return; // superseded mid-build; drop this partial batch
            rows.Add(row);
        }

        if (generation != _reloadGeneration) return; // bail before ever touching Entries
        Entries.Clear();
        foreach (var row in rows) Entries.Add(row);
    }

    [RelayCommand]
    private async Task AddPhotoAsync()
    {
        foreach (var path in await _dialog.PromptMultipleAsync(ImageFileFilter))
            PendingPhotos.Add(path);
    }

    [RelayCommand]
    private void RemovePendingPhoto(string? path)
    {
        if (path is not null) PendingPhotos.Remove(path);
    }

    [RelayCommand]
    private void BeginEdit(JournalEntryRowViewModel? row)
    {
        if (row is null) return;
        foreach (var r in Entries) r.IsEditing = false;
        row.EditDraft = row.Text;
        row.IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        foreach (var r in Entries) r.IsEditing = false;
    }

    [RelayCommand]
    private async Task Save(CancellationToken ct)
    {
        if (_habitNodeId == Guid.Empty) return;
        ErrorMessage = null;

        // Una modifica in riga in corso vince sul composer: e' l'azione che l'utente sta guardando.
        if (Entries.FirstOrDefault(r => r.IsEditing) is { } editing)
        {
            var editText = editing.EditDraft?.Trim() ?? string.Empty;
            if (editText.Length == 0)
            {
                // Stessa regola del composer piu' sotto: una voce non puo' restare senza testo.
                ErrorMessage = Strings.Diario_QuickEntry_Error_Empty;
                return;
            }
            IsSaving = true;
            try
            {
                await _diary.EditEntryAsync(editing.Id, editText, ct).ConfigureAwait(true);
                editing.Text = editText;
                editing.IsEditing = false;
            }
            finally
            {
                IsSaving = false;
            }
            return;
        }

        var text = Draft?.Trim() ?? string.Empty;
        if (text.Length == 0 && PendingPhotos.Count == 0)
        {
            ErrorMessage = Strings.Diario_QuickEntry_Error_Empty;
            return;
        }

        IsSaving = true;
        try
        {
            // Le foto si normalizzano qui e viaggiano nella stessa transazione della voce. Un file che
            // non si importa si salta (messaggio) e le altre entrano.
            var photos = new List<ImportedPhoto>();
            var failed = false;
            foreach (var path in PendingPhotos.ToList())
            {
                try
                {
                    photos.Add(await _photoImporter.ImportAsync(path, ct).ConfigureAwait(true));
                }
#pragma warning disable CA1031 // un file illeggibile non deve abortire il resto del batch
                catch (Exception)
#pragma warning restore CA1031
                {
                    failed = true;
                }
            }
            await _diary.SaveEntryAsync(_habitNodeId, _day, new DiaryEntryDraft(text, [], photos), ct).ConfigureAwait(true);

            if (failed) ErrorMessage = Strings.Diario_QuickEntry_Error_PhotoFailed;

            Draft = string.Empty;
            PendingPhotos.Clear();
            await ReloadEntriesAsync(ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task Delete(JournalEntryRowViewModel? row, CancellationToken ct)
    {
        if (row is null) return;
        var ok = await _confirm.ConfirmAsync(
            Strings.Diario_QuickEntry_DeleteConfirm_Title,
            Strings.Diario_QuickEntry_DeleteConfirm_Message).ConfigureAwait(true);
        if (!ok) return;
        await _diary.DeleteEntryAsync(row.Id, ct).ConfigureAwait(true);
        Entries.Remove(row);
    }
}
