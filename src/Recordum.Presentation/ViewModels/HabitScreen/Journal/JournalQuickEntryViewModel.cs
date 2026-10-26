using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
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
using Recordum.Core.Text;

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
    private readonly IAppSettingService _appSettings;

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

    /// <summary>Fissata al caricamento: nel diario (gesto Diary) la voce nuova e quella in modifica si scrivono nello stesso
    /// editor e le immagini sono riferimenti nel testo; in nota veloce nulla cambia.</summary>
    public bool IsDiaryMode { get; private set; }

    /// <summary>La voce che sta nell'editor al posto della nuova (solo in modalita' diario).</summary>
    public JournalEntryRowViewModel? EditingEntry => IsDiaryMode ? EditingRow : null;

    public string? EditingBanner => EditingEntry is { } e
        ? Format(Strings.Diario_Editor_EditingBanner, e.TimeDisplay)
        : null;

    /// <summary>Il testo che l'editor sta scrivendo: in modalita' diario quello della voce in modifica se c'e', altrimenti <see cref="Draft"/>.</summary>
    public string ActiveText
    {
        get => EditingEntry?.EditDraft ?? Draft;
        set
        {
            if (EditingEntry is { } e) e.EditDraft = value ?? string.Empty;
            else Draft = value ?? string.Empty;
        }
    }

    /// <summary>La riga Tag dell'editor: quella della voce in modifica (diario) o della voce nuova.</summary>
    public TagEditorViewModel ActiveTags => EditingEntry?.EditTags ?? PropertyTags;

    /// <summary>Miniature delle foto in attesa e di quelle delle voci del giorno, per id (null = mancante), per l'editor.</summary>
    public IReadOnlyDictionary<Guid, byte[]?> PhotoThumbnails
    {
        get
        {
            var map = new Dictionary<Guid, byte[]?>();
            foreach (var p in PendingPhotos) map[p.Id] = p.Thumbnail;
            foreach (var row in Entries)
            {
                foreach (var p in row.AddedPhotos) map[p.Id] = p.Thumbnail;
                foreach (var a in row.Photos) map[a.Id] = a.Thumbnail;
            }
            return map;
        }
    }

    partial void OnDraftChanged(string value)
    {
        OnPropertyChanged(nameof(ActiveText));
        ScheduleDraftWrite();
    }

    private void OnActiveSurfaceChanged()
    {
        _inlineQuery = null;
        InlineSuggestions = [];
        OnPropertyChanged(nameof(EditingEntry));
        OnPropertyChanged(nameof(EditingBanner));
        OnPropertyChanged(nameof(ActiveText));
        OnPropertyChanged(nameof(ActiveTags));
    }

    private void Watch(JournalEntryRowViewModel row)
    {
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(JournalEntryRowViewModel.IsEditing)) { OnActiveSurfaceChanged(); ScheduleDraftWrite(); }
            else if (e.PropertyName == nameof(JournalEntryRowViewModel.EditDraft)) { OnPropertyChanged(nameof(ActiveText)); ScheduleDraftWrite(); }
            else if (e.PropertyName == nameof(JournalEntryRowViewModel.EditTags))
            {
                OnPropertyChanged(nameof(ActiveTags));
                if (row.EditTags is { } et) et.Tags.CollectionChanged += (_, _) => ScheduleDraftWrite();
                ScheduleDraftWrite();
            }
        };
        row.Photos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PhotoThumbnails));
        row.AddedPhotos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PhotoThumbnails));
    }

    public JournalQuickEntryViewModel(
        IDiaryService diary,
        INoteService notes,
        IAttachmentService attachments,
        IPhotoImporter photoImporter,
        ITagService tags,
        IOpenFileDialog dialog,
        IConfirmationDialog confirm,
        IClock clock,
        IAppSettingService appSettings)
    {
        ArgumentNullException.ThrowIfNull(diary);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(photoImporter);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(appSettings);
        _diary = diary;
        _notes = notes;
        _attachments = attachments;
        _photoImporter = photoImporter;
        _tags = tags;
        _dialog = dialog;
        _confirm = confirm;
        _clock = clock;
        _appSettings = appSettings;
        PropertyTags.Tags.CollectionChanged += (_, _) => ScheduleDraftWrite();
        _day = Today();
        PendingPhotos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PhotoThumbnails));
        Entries.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(PhotoThumbnails)); OnActiveSurfaceChanged(); };
    }

    private DateOnly Today() => _dayBoundary.Today(_clock);

    private DayBoundary _dayBoundary = DayBoundary.Utc;

    public async Task LoadAsync(HabitNode node, DayBoundary dayBoundary, bool isDiaryMode, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        _habitNodeId = node.Id;
        IsDiaryMode = isDiaryMode;
        OnPropertyChanged(nameof(IsDiaryMode));
        // Il confine arriva dallo screen: va impostato PRIMA di ri-derivare il giorno corrente,
        // altrimenti il composer si apre sul giorno che diceva il vecchio confine.
        _dayBoundary = dayBoundary;
        _day = Today();
        // Una scrittura di bozza in volo finisce prima che la si rilegga.
        await FlushDraftAsync().ConfigureAwait(true);
        _draftRestoring = true; // azzerare lo stato qui non e' una modifica dell'utente: non deve riscrivere la bozza
        try
        {
            Draft = string.Empty;
            ErrorMessage = null;
            PendingPhotos.Clear();
            PropertyTags.Tags.Clear();
            InlineSuggestions = [];
        }
        finally { _draftRestoring = false; }
        await RefreshKnownTagsAsync(ct).ConfigureAwait(true);
        await ReloadEntriesAsync(ct).ConfigureAwait(true);
        await RestoreDraftAsync(ct).ConfigureAwait(true);
    }

    // ---- Bozza (decisione 3): ogni modifica la scrive in app_setting; LoadAsync la rilegge, mai la riscrive. ----

    private sealed record DraftDto(
        string? Text, List<string>? Tags, Guid? EditNoteId, string? EditText, List<string>? EditTags);

    private static readonly JsonSerializerOptions DraftJson = new(JsonSerializerDefaults.Web); // camelCase: text, tags, editNoteId...

    private string DraftKey => "diary.draft." + _habitNodeId.ToString("N");

    private bool _draftRestoring;
    private bool _draftWriting;
    private bool _draftDirty;
    private Task _draftTask = Task.CompletedTask;

    /// <summary>Attende la scrittura della bozza in volo (e quelle che si accodano). Per i test e per <see cref="LoadAsync"/>:
    /// la chiusura della tab non attende niente, la finestra di perdita e' una scrittura di pochi ms.</summary>
    public async Task FlushDraftAsync()
    {
        while (!_draftTask.IsCompleted) await _draftTask.ConfigureAwait(true);
    }

    private void ScheduleDraftWrite()
    {
        if (_draftRestoring || _habitNodeId == Guid.Empty) return;
        _draftDirty = true;
        if (_draftWriting) return; // la scrittura in volo, finita, rilegge lo stato e riscrive
        _draftWriting = true;
        _draftTask = WriteDraftLoopAsync();
    }

    // Coalescente, senza timer: al piu' una scrittura in volo; a fine scrittura, se nel frattempo e' cambiato qualcosa, ancora.
    private async Task WriteDraftLoopAsync()
    {
        try
        {
            while (_draftDirty)
            {
                _draftDirty = false;
                try
                {
                    await _appSettings.SetAsync(DraftKey, BuildDraftJson()).AsTask().ConfigureAwait(true);
                }
#pragma warning disable CA1031 // un errore di scrittura non ferma l'editor: si mostra e il testo resta
                catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
                {
                    System.Diagnostics.Debug.WriteLine($"JournalQuickEntryViewModel draft write failed: {ex}");
                    ErrorMessage = Strings.Diario_Editor_DraftFailed;
                }
            }
        }
        finally { _draftWriting = false; }
    }

    private string BuildDraftJson()
    {
        var tags = PropertyTags.Tags.ToList();
        var edit = IsDiaryMode ? EditingRow : null;
        var editTags = edit?.EditTags?.Tags.ToList();
        var dto = new DraftDto(Draft, tags, edit?.Id, edit?.EditDraft, editTags);
        // Niente da ricordare = stringa vuota.
        return Draft.Length == 0 && tags.Count == 0 && edit is null ? string.Empty : JsonSerializer.Serialize(dto, DraftJson);
    }

    private async Task RestoreDraftAsync(CancellationToken ct)
    {
        DraftDto? dto = null;
        try
        {
            var raw = await _appSettings.GetAsync(DraftKey, ct).ConfigureAwait(true);
            if (!string.IsNullOrEmpty(raw)) dto = JsonSerializer.Deserialize<DraftDto>(raw, DraftJson);
        }
#pragma warning disable CA1031 // valore illeggibile = nessuna bozza
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            System.Diagnostics.Debug.WriteLine($"JournalQuickEntryViewModel draft read failed: {ex}");
        }
        if (dto is null) return;
        _draftRestoring = true;
        try
        {
            Draft = dto.Text ?? string.Empty;
            foreach (var t in dto.Tags ?? []) PropertyTags.Tags.Add(t);
            // La parte di modifica torna solo se la voce e' fra quelle del giorno caricato.
            if (IsDiaryMode && dto.EditNoteId is { } id && Entries.FirstOrDefault(r => r.Id == id) is { } row)
            {
                row.BeginEdit(_knownTags);
                row.EditDraft = dto.EditText ?? row.Text;
                row.EditTags?.Tags.Clear();
                foreach (var t in dto.EditTags ?? []) row.EditTags?.Tags.Add(t);
            }
        }
        finally { _draftRestoring = false; }
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
            Watch(row);
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
        if (_inlineQuery is not { } q) { InlineSuggestions = []; return; }
        var list = TagEditorViewModel.Match(_knownTags, q.Prefix, []).Select(n => "#" + n).ToList();
        // Come la riga Tag: un prefisso che non e' ancora un tag noto offre "Crea «…»" in coda.
        if (InlineTags.IsValidName(q.Prefix)
            && !_knownTags.Contains(q.Prefix, StringComparer.OrdinalIgnoreCase))
            list.Add(TagEditorViewModel.CreateLabel("#" + q.Prefix));
        InlineSuggestions = list;
    }

    /// <summary>L'utente sceglie un suggerimento del menu <c>#</c>: torna il testo e il cursore nuovi (la view li applica).</summary>
    public (string Text, int Caret) AcceptInlineSuggestion(string name)
    {
        if (_inlineQuery is not { } q) return (_inlineText, _inlineText.Length);
        // Le voci mostrano il '#' (spec §5.1); nel testo ne va uno solo, quello che c'e' gia'.
        if (name == TagEditorViewModel.CreateLabel("#" + q.Prefix)) name = q.Prefix;
        else if (name is ['#', ..]) name = name[1..];
        var result = TagCompletion.Apply(_inlineText, q, name);
        _inlineQuery = null;
        InlineSuggestions = [];
        return result;
    }

#pragma warning disable CA1863 // messaggi d'errore rari, non vale una CompositeFormat
    private static string Format(string template, string arg) => string.Format(CultureInfo.CurrentCulture, template, arg);
#pragma warning restore CA1863

    private JournalEntryRowViewModel? EditingRow => Entries.FirstOrDefault(r => r.IsEditing);

    [RelayCommand]
    private async Task AddPhotoAsync(CancellationToken ct)
    {
        var photos = await ImportChosenPhotosAsync(ct).ConfigureAwait(true);
        // In modalita' diario una foto vale solo col riferimento nel testo (Save scarta le altre): senza cursore va in coda.
        if (IsDiaryMode && photos.Count > 0) ActiveText = AppendPhotoRefs(ActiveText, photos);
    }

    private static string AppendPhotoRefs(string? text, IEnumerable<ImportedPhoto> photos)
    {
        var sb = new System.Text.StringBuilder(text ?? string.Empty);
        if (sb.Length > 0 && sb[^1] != '\n') sb.Append('\n');
        foreach (var p in photos) sb.Append(PhotoRefs.Format(p.Id, p.Filename)).Append('\n');
        return sb.ToString();
    }

    private bool _isInserting;

    /// <summary>Sceglie i file, li importa e li aggiunge alla voce attiva (quella in modifica, altrimenti il composer).
    /// Torna le foto entrate; gli errori per file vanno in <see cref="ErrorMessage"/>.</summary>
    private async Task<List<ImportedPhoto>> ImportChosenPhotosAsync(CancellationToken ct)
    {
        var imported = new List<ImportedPhoto>();
        // Mai durante un salvataggio: le foto scelte nel frattempo non finirebbero nella transazione e il successo le svuoterebbe.
        if (IsSaving) return imported;
        // Durante una modifica le foto vanno alla voce che si sta modificando, non al composer.
        ObservableCollection<ImportedPhoto> target = EditingRow?.AddedPhotos ?? PendingPhotos;
        var errors = new List<string>();
        foreach (var path in await _dialog.PromptMultipleAsync(ImageFileFilter).ConfigureAwait(true))
        {
            try
            {
                var photo = await _photoImporter.ImportAsync(path, ct).ConfigureAwait(true);
                target.Add(photo);
                imported.Add(photo);
            }
            catch (PhotoImportException ex)
            {
                errors.Add(Format(ex.Reason switch
                {
                    PhotoImportFailure.Unsupported => Strings.Diario_QuickEntry_Error_PhotoUnsupported,
                    PhotoImportFailure.TooLarge => Strings.Diario_QuickEntry_Error_PhotoTooLarge,
                    PhotoImportFailure.TooManyPixels => Strings.Diario_QuickEntry_Error_PhotoTooManyPixels,
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
        return imported;
    }

    /// <summary>"Inserisci immagine…": sceglie e importa i file (stesso dialogo e stessi errori di <see cref="AddPhotoCommand"/>) e
    /// mette al cursore una riga di riferimento per foto. Torna il testo e il cursore nuovi (dopo i riferimenti, a inizio riga),
    /// o null se nessuna foto e' entrata.</summary>
    public async Task<(string Text, int Caret)?> InsertPhotosAsync(string text, int caret, CancellationToken ct)
    {
        List<ImportedPhoto> photos;
        _isInserting = true; // Save non parte mentre l'import e' in corso (come AddPhotoCommand.IsRunning)
        try { photos = await ImportChosenPhotosAsync(ct).ConfigureAwait(true); }
        finally { _isInserting = false; }
        if (photos.Count == 0) return null;
        text ??= string.Empty;
        caret = Math.Clamp(caret, 0, text.Length);
        var block = new System.Text.StringBuilder();
        if (caret > 0 && text[caret - 1] != '\n') block.Append('\n'); // il riferimento sta in una riga a se'
        foreach (var p in photos) block.Append(PhotoRefs.Format(p.Id, p.Filename)).Append('\n');
        return (text.Insert(caret, block.ToString()), caret + block.Length);
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
    private async Task BeginEditAsync(JournalEntryRowViewModel? row)
    {
        if (row is null) return;
        // Cambiare voce con modifiche aperte chiede prima di scartarle (in tutte e due le modalita').
        if (EditingRow is { } open && open != row && open.HasChanges)
        {
            var ok = await _confirm.ConfirmAsync(
                Strings.Diario_Editor_DiscardEdit_Title,
                Format(Strings.Diario_Editor_DiscardEdit_Message, open.TimeDisplay)).ConfigureAwait(true);
            if (!ok) return;
        }
        CancelEdit();
        row.BeginEdit(_knownTags);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        foreach (var r in Entries.Where(r => r.IsEditing)) r.CancelEdit();
    }

    /// <summary>Toglie dal testo i riferimenti a foto che non sono fra <paramref name="validIds"/> (ne' in attesa ne' allegate).</summary>
    private static string StripUnknownRefs(string text, IEnumerable<Guid> validIds)
    {
        var valid = validIds.ToHashSet();
        var sb = new System.Text.StringBuilder(text);
        foreach (var r in PhotoRefs.Find(text).Reverse().Where(r => !valid.Contains(r.Id)))
        {
            var end = r.Start + r.Length;
            var lineStart = r.Start == 0 ? 0 : text.LastIndexOf('\n', r.Start - 1) + 1;
            var nl = text.IndexOf('\n', end);
            var lineEnd = nl < 0 ? text.Length : nl;
            if (string.IsNullOrWhiteSpace(text[lineStart..r.Start]) && string.IsNullOrWhiteSpace(text[end..lineEnd]))
            {
                // Il riferimento era l'unico contenuto della riga: via la riga, non resta un buco.
                if (nl >= 0) sb.Remove(lineStart, nl + 1 - lineStart);
                else sb.Remove(Math.Max(lineStart - 1, 0), lineEnd - Math.Max(lineStart - 1, 0));
            }
            else sb.Remove(r.Start, r.Length);
        }
        return sb.ToString().Trim();
    }

    [RelayCommand]
    private async Task Save(CancellationToken ct)
    {
        // Salvare mentre una scelta di foto e' in corso perderebbe quelle che arrivano dopo lo snapshot.
        if (_habitNodeId == Guid.Empty || IsSaving || _isInserting || AddPhotoCommand.IsRunning) return;
        ErrorMessage = null;

        // Una modifica in riga in corso vince sul composer: e' l'azione che l'utente sta guardando.
        if (EditingRow is { } editing)
        {
            var editText = editing.EditDraft?.Trim() ?? string.Empty;
            var kept = editing.Photos.Select(p => p.Id).ToList();
            var added = editing.AddedPhotos.ToList();
            var removed = editing.RemovedPhotoIds.ToList();
            if (IsDiaryMode)
            {
                // Decisione 6: si tolgono solo le foto il cui riferimento c'era all'inizio della modifica e ora non c'e' piu';
                // quelle nate senza riferimento (nota veloce) restano. Entrano solo le nuove con il riferimento nel testo.
                var before = JournalEntryRowViewModel.ReferencedIds(editing.Text);
                var now = JournalEntryRowViewModel.ReferencedIds(editing.EditDraft);
                var dropped = kept.Where(id => before.Contains(id) && !now.Contains(id)).ToList();
                removed.AddRange(dropped);
                kept.RemoveAll(dropped.Contains);
                added = added.Where(p => now.Contains(p.Id)).ToList();
                editText = StripUnknownRefs(editText, [.. kept, .. added.Select(p => p.Id)]);
            }
            if (editText.Length == 0 && kept.Count + added.Count == 0)
            {
                // Stessa regola del composer piu' sotto: una voce non puo' restare senza testo ne' foto.
                ErrorMessage = Strings.Diario_QuickEntry_Error_Empty;
                return;
            }
            IsSaving = true;
            try
            {
                var edit = new DiaryEntryEdit(
                    editText, editing.EditTags?.Tags.ToList() ?? [], added, removed);
                await _diary.EditEntryAsync(editing.Id, edit, ct).ConfigureAwait(true);
                editing.Text = editText;
                editing.IsEditing = false;
                await RefreshKnownTagsAsync(ct).ConfigureAwait(true);
                await ReloadEntriesAsync(ct).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"JournalQuickEntryViewModel edit save failed: {ex}");
                ErrorMessage = Strings.Diario_QuickEntry_Error_SaveFailed; // la modifica resta aperta, con tutto quello che c'era
            }
            finally
            {
                IsSaving = false;
            }
            return;
        }

        var text = Draft?.Trim() ?? string.Empty;
        var photos = PendingPhotos.ToList();
        if (IsDiaryMode)
        {
            // Entrano solo le foto con il riferimento ancora nel testo; i riferimenti a id sconosciuti si tolgono.
            var now = JournalEntryRowViewModel.ReferencedIds(text);
            photos = photos.Where(p => now.Contains(p.Id)).ToList();
            text = StripUnknownRefs(text, photos.Select(p => p.Id));
        }
        if (text.Length == 0 && photos.Count == 0)
        {
            ErrorMessage = Strings.Diario_QuickEntry_Error_Empty;
            return;
        }

        IsSaving = true;
        try
        {
            var draft = new DiaryEntryDraft(text, PropertyTags.Tags.ToList(), photos);
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
            System.Diagnostics.Debug.WriteLine($"JournalQuickEntryViewModel save failed: {ex}");
            ErrorMessage = Strings.Diario_QuickEntry_Error_SaveFailed;
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
