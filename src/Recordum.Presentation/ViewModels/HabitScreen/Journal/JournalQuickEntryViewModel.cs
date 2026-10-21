using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen.Journal;

/// <summary>Composer "nota veloce" della colonna Diario (Onda 2.4, U7). Ogni salvataggio e' una
/// voce di diario vera — Nota(Diario) + Completion via IDiaryService — cosi' streak, heatmap e
/// cifra chiave funzionano senza semantica nuova. Le foto si importano (normalizzano) quando le scegli, cosi'
/// l'errore su un file si vede subito, e restano in memoria come <see cref="ImportedPhoto"/> fino a "Salva":
/// testo, riga Tag e foto viaggiano nella stessa transazione, e se fallisce non si perde niente.</summary>
public sealed partial class JournalQuickEntryViewModel : ObservableObject
{
    private const string ImageFileFilter =
        "Image files (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp";

    private readonly IDiaryService _diary;
    private readonly INoteService _notes;
    private readonly IAttachmentService _attachments;
    private readonly IPhotoImporter _photoImporter;
    private readonly ITagService _tags;
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

    private IReadOnlyList<string> _knownTags = [];
    private string _inlineText = string.Empty;
    private TagQuery? _inlineQuery;

    /// <summary>Foto scelte e gia' normalizzate, non ancora salvate.</summary>
    public ObservableCollection<ImportedPhoto> PendingPhotos { get; } = [];

    /// <summary>La riga Tag della voce nuova.</summary>
    public TagEditorViewModel PropertyTags { get; } = new();

    /// <summary>Tag esistenti proposti dal menu <c>#</c> mentre si scrive nel testo (vuoto = menu chiuso).</summary>
    [ObservableProperty] private IReadOnlyList<string> _inlineSuggestions = [];

    public ObservableCollection<JournalEntryRowViewModel> Entries { get; } = [];

    public JournalQuickEntryViewModel(
        IDiaryService diary,
        INoteService notes,
        IAttachmentService attachments,
        IPhotoImporter photoImporter,
        ITagService tags,
        IOpenFileDialog dialog,
        IConfirmationDialog confirm,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(diary);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(photoImporter);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(clock);
        _diary = diary;
        _notes = notes;
        _attachments = attachments;
        _photoImporter = photoImporter;
        _tags = tags;
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
        PropertyTags.Tags.Clear();
        InlineSuggestions = [];
        await RefreshKnownTagsAsync(ct).ConfigureAwait(true);
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
        var tagsByNote = await _diary.GetEntryTagsAsync(ofDay.Select(n => n.Id).ToList(), ct).ConfigureAwait(true);
        if (generation != _reloadGeneration) return; // superseded while awaiting the tags

        var rows = new List<JournalEntryRowViewModel>();
        foreach (var n in ofDay)
        {
            var row = new JournalEntryRowViewModel(
                n, tagsByNote.TryGetValue(n.Id, out var t) ? t : [], _clock);
            foreach (var a in await _attachments.ListByNoteAsync(n.Id, ct).ConfigureAwait(true))
                row.Photos.Add(new AttachmentViewModel(a, await _attachments.ReadThumbnailAsync(a.Id, ct).ConfigureAwait(true)));
            if (generation != _reloadGeneration) return; // superseded mid-build; drop this partial batch
            rows.Add(row);
        }

        if (generation != _reloadGeneration) return; // bail before ever touching Entries
        Entries.Clear();
        foreach (var row in rows) Entries.Add(row);
    }

    // I nomi per il menu e per la riga Tag. Un elenco che non si legge non deve fermare la nota: niente suggerimenti.
    private async Task RefreshKnownTagsAsync(CancellationToken ct)
    {
        try
        {
            _knownTags = (await _tags.ListAsync(ct).ConfigureAwait(true)).Select(t => t.Name).ToList();
            PropertyTags.KnownNames = _knownTags;
        }
#pragma warning disable CA1031 // suggerimenti opzionali
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            System.Diagnostics.Debug.WriteLine($"JournalQuickEntryViewModel tag list failed: {ex}");
        }
    }

    /// <summary>Il testo o il cursore sono cambiati: aggiorna il menu <c>#</c>.</summary>
    public void UpdateInlineQuery(string text, int caret)
    {
        _inlineText = text ?? string.Empty;
        _inlineQuery = TagCompletion.Find(_inlineText, caret);
        InlineSuggestions = _inlineQuery is { } q ? TagEditorViewModel.Match(_knownTags, q.Prefix, []) : [];
    }

    /// <summary>L'utente sceglie un suggerimento del menu <c>#</c>: torna il testo e il cursore nuovi (la view li applica).</summary>
    public (string Text, int Caret) AcceptInlineSuggestion(string name)
    {
        if (_inlineQuery is not { } q) return (_inlineText, _inlineText.Length);
        var result = TagCompletion.Apply(_inlineText, q, name);
        _inlineQuery = null;
        InlineSuggestions = [];
        return result;
    }

#pragma warning disable CA1863 // messaggi d'errore rari, non vale una CompositeFormat
    private static string Format(string template, string filename) => string.Format(CultureInfo.CurrentCulture, template, filename);
#pragma warning restore CA1863

    private JournalEntryRowViewModel? EditingRow => Entries.FirstOrDefault(r => r.IsEditing);

    [RelayCommand]
    private async Task AddPhotoAsync(CancellationToken ct)
    {
        // Mai durante un salvataggio: le foto scelte nel frattempo non finirebbero nella transazione e il successo le svuoterebbe.
        if (IsSaving) return;
        // Durante una modifica le foto vanno alla voce che si sta modificando, non al composer.
        ObservableCollection<ImportedPhoto> target = EditingRow?.AddedPhotos ?? PendingPhotos;
        var errors = new List<string>();
        foreach (var path in await _dialog.PromptMultipleAsync(ImageFileFilter).ConfigureAwait(true))
        {
            try
            {
                target.Add(await _photoImporter.ImportAsync(path, ct).ConfigureAwait(true));
            }
            catch (PhotoImportException ex)
            {
                errors.Add(Format(ex.Reason switch
                {
                    PhotoImportFailure.Unsupported => Strings.Diario_QuickEntry_Error_PhotoUnsupported,
                    PhotoImportFailure.TooLarge => Strings.Diario_QuickEntry_Error_PhotoTooLarge,
                    _ => Strings.Diario_QuickEntry_Error_PhotoUnreadable,
                }, ex.Filename));
            }
#pragma warning disable CA1031 // un file illeggibile non deve fermare le altre foto della stessa scelta
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                errors.Add(Format(Strings.Diario_QuickEntry_Error_PhotoUnreadable, path[(path.LastIndexOfAny(['\\', '/']) + 1)..]));
            }
        }
        ErrorMessage = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
    }

    [RelayCommand]
    private void RemovePendingPhoto(ImportedPhoto? photo)
    {
        if (photo is null) return;
        PendingPhotos.Remove(photo);
        EditingRow?.AddedPhotos.Remove(photo);
    }

    /// <summary>Toglie, in modifica, una foto gia' salvata: sparisce davvero solo con "Salva".</summary>
    [RelayCommand]
    private void RemoveEditPhoto(AttachmentViewModel? photo)
    {
        if (photo is not null) EditingRow?.RemovePhoto(photo);
    }

    [RelayCommand]
    private void BeginEdit(JournalEntryRowViewModel? row)
    {
        if (row is null) return;
        CancelEdit();
        row.BeginEdit(_knownTags);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        foreach (var r in Entries.Where(r => r.IsEditing)) r.CancelEdit();
    }

    [RelayCommand]
    private async Task Save(CancellationToken ct)
    {
        // Salvare mentre una scelta di foto e' in corso perderebbe quelle che arrivano dopo lo snapshot.
        if (_habitNodeId == Guid.Empty || IsSaving || AddPhotoCommand.IsRunning) return;
        ErrorMessage = null;

        // Una modifica in riga in corso vince sul composer: e' l'azione che l'utente sta guardando.
        if (EditingRow is { } editing)
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
                var edit = new DiaryEntryEdit(
                    editText, editing.EditTags?.Tags.ToList() ?? [], editing.AddedPhotos.ToList(), editing.RemovedPhotoIds.ToList());
                await _diary.EditEntryAsync(editing.Id, edit, ct).ConfigureAwait(true);
                editing.Text = editText;
                editing.IsEditing = false;
                await RefreshKnownTagsAsync(ct).ConfigureAwait(true);
                await ReloadEntriesAsync(ct).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ErrorMessage = ex.Message; // la modifica resta aperta, con tutto quello che c'era
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
            var draft = new DiaryEntryDraft(text, PropertyTags.Tags.ToList(), PendingPhotos.ToList());
            await _diary.SaveEntryAsync(_habitNodeId, _day, draft, ct).ConfigureAwait(true);

            // Solo dopo il successo si svuota: un salvataggio che fallisce non toglie niente all'utente.
            Draft = string.Empty;
            PendingPhotos.Clear();
            PropertyTags.Tags.Clear();
            InlineSuggestions = [];
            await RefreshKnownTagsAsync(ct).ConfigureAwait(true);
            await ReloadEntriesAsync(ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
