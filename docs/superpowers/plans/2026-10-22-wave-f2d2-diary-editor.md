# Onda F2d-2 — Editor del diario della sera — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dopo F2d-2, su Avalonia il diario della sera si scrive in un editor Live Preview (titoli, grassetto, corsivo,
elenchi, citazioni, `#tag` evidenziati, immagini nel testo), il testo non si perde mai (bozza salvata a ogni modifica),
la Cronologia mostra i chip dei tag, e l'export Markdown esce come cartella che si apre in Obsidian.

**Architecture:** Nessuna migrazione. Il diario della sera riusa il ViewModel della nota veloce
(`JournalQuickEntryViewModel`) in una **modalità diario**: una sola superficie di scrittura (voce nuova o voce in
modifica), immagini come riferimenti `![nome](photo:<id>)` risolti da una classe di Core condivisa da editor, export e
Cronologia. La sonda `LivePreviewEditorControl` diventa il controllo `DiaryEditor`; la sonda `MarkdownEditorControl` e
`Markdown.Avalonia` escono. Il contratto dei plugin di export restituisce un documento più file accessori. Cancellare una
voce cancella già i suoi allegati (`DiaryService.DeleteEntryAsync`, F2d-1): nessun lavoro in questa onda.

**Tech Stack:** C# / .NET 10; EF Core 10 + SQLCipher; Avalonia 12 + `Avalonia.AvaloniaEdit` 12.0.0 +
`Avalonia.Headless.XUnit` (xunit.v3); `CommunityToolkit.Mvvm`; QuestPDF (ExportEngine).

**Spec:** [`docs/superpowers/specs/2026-10-21-wave-f2d-diary-design.md`](../specs/2026-10-21-wave-f2d-diary-design.md) §6
(F2d-2), §3 (anti-obiettivi), §8-9. Forma del piano (D5): decisioni, interfacce, obblighi di test, trappole. Aperti di
F2d-1 che questa onda chiude: `docs/phase-4-issues.md` F2d1-6 (modifica scartata in silenzio) e F2d1-7 (chip in Cronologia).

## Global Constraints

- **Branch**: `f2d2-editor`, da `f2d1-tags-photos` (pila non mergiata). Mai commit su `main`; cambiare branch in una
  chiamata separata. Aggiornato un `.md` → commit e push subito; dopo ogni push si guarda la CI.
- **Ambiente Linux**: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"`. Build:
  `dotnet build Recordum.slnx -p:EnableWindowsTargeting=true --nologo --no-incremental` (l'orologio di sistema è tornato
  indietro: la build incrementale salta progetti). **Mai `-p:UseAppHost=false`**.
- **Baseline** (fine di F2d-1, `f8bcf4d5`): Core 170, Data 646 + 1 skipped (`Category!=WindowsOnly`), Presentation 1067,
  UI 401 (`Category!=Performance`, più 3 di prestazioni), ExportEngine 39. Si rimisura nel Task 1 Step 0; si controlla
  sempre il totale eseguito.
- **Schema**: resta **2.19**. Nessuna migrazione (spec §8).
- **Il testo non si perde mai** (spec §3, §6): chiusura della tab o della finestra, crash, errore di scrittura taciuto.
- **Privacy** (spec §3): nessuna foto in chiaro su disco fuori dall'export voluto dall'utente; la bozza sta nel DB
  cifrato (`app_setting`), mai in un file.
- **Il parametro muto** (CLAUDE.md §7): parametri nuovi obbligatori; i record che prendono un campo nuovo lo prendono
  posizionale, e il compilatore elenca i chiamanti.
- **Tempo** solo da `IClock`; il giorno da `DayBoundary`.
- **WPF congelato**: compila e passa i suoi test; cambia solo dove un contratto condiviso lo impone (export, record).
  Il gesto `Diary` su WPF con WebView2 resta `JournalQuickAddViewModel` + `MarkdownEditorControl` WPF.
- **Avalonia**: `InitializeComponent()`; `x:DataType` su ogni view e template; `[AvaloniaFact]`; `using Xunit;`; DB privato
  (`tests/Recordum.UI.Tests/Fixtures/PrivateDbHost.cs`); mai `ContentTemplate` statico su contenuto che può essere null
  (CLAUDE.md §8). **Il primo `AvaloniaEdit` headless della sessione non fa il layout**: si prima con una vista diversa che
  contiene un editor, poi `ForceRenderTimerTick()` + `RunJobs()` (vedi `LivePreviewStructureTests`); un PNG senza
  asserzione strutturale non prova niente. Suite UI in `en_US` e `it_IT`.
- **Stringhe**: `Strings.resx` + `Strings.it.resx` + accessor in `Strings.Designer.cs` **solo via shell**; prefisso
  `Diario_Editor_*` per le chiavi nuove; riusare `Diario_QuickEntry_*` dove il senso è lo stesso.
- **Linguaggio visivo**: icone Lucide; pulsanti di sola icona con `AutomationProperties.Name` e `ToolTip.Tip`; chiaro e
  scuro; serif (Source Serif 4) per il testo del diario (CLAUDE.md §11).
- **`TreatWarningsAsErrors`** globale.

## Decisioni prese scrivendo il piano

L'esecutore non le ri-discute; sono qui perché l'autore possa disfarle prima dell'esecuzione.

1. **Un solo ViewModel per i due gesti.** Su Avalonia il diario apre sempre `JournalQuickEntryViewModel` (com'è oggi);
   prende `bool IsDiaryMode` = gesto diverso da `QuickNote`. In modalità nota veloce nulla cambia. Niente
   `JournalQuickAddViewModel` su Avalonia; `IEditorAvailability` resta com'è.
2. **Riferimento all'immagine**: `![nome-file](photo:<id in 32 cifre esadecimali>)`, una riga a sé, inserita al cursore da
   "Inserisci immagine…". `ImportedPhoto` prende un `Guid Id` assegnato all'import, che diventa l'id della riga
   `attachment`. L'editor mostra la **miniatura** (lato lungo 320 px, già nel DB) fuori dalla riga del cursore.
3. **Salvataggio esplicito + bozza automatica.** "Salva" crea o modifica la voce come oggi. In più, ogni modifica del
   testo o della riga Tag scrive una bozza in `app_setting`, chiave `diary.draft.<habitId 32 hex>`, valore JSON
   `{ "text", "tags", "editNoteId", "editText", "editTags" }`; stringa vuota = nessuna bozza. Scrittore **coalescente**:
   al più una scrittura in volo, e alla fine si riscrive se il valore è cambiato nel frattempo (nessun timer). All'apertura
   la bozza torna; la parte di modifica torna solo se la voce è fra quelle del giorno caricato. Un errore di scrittura della
   bozza si vede (`ErrorMessage`). Niente dialogo alla chiusura: non c'è niente da perdere. **Limite noto**: le foto
   inserite e non ancora salvate non sono nella bozza; dopo un crash il loro riferimento resta nel testo e l'editor mostra
   "foto non salvata"; al salvataggio i riferimenti a id che non sono né in attesa né allegati alla voce si tolgono.
4. **Modalità diario: una superficie sola.** La voce nuova e la voce in modifica si scrivono nello stesso editor:
   `Modifica` su una voce del giorno la porta nell'editor con una striscia "Stai modificando la voce delle HH:mm ·
   Annulla"; la bozza della voce nuova aspetta e torna dopo Salva o Annulla. Le voci del giorno si mostrano rese (lo stesso
   controllo in sola lettura), con Modifica ed Elimina. In nota veloce la modifica resta nella riga, come in F2d-1.
5. **Cambiare voce con modifiche aperte chiede** (`IConfirmationDialog`: "Scartare le modifiche alla voce delle HH:mm?"),
   in tutte e due le modalità (chiude F2d1-6).
6. **Foto in modalità diario**: al salvataggio entrano solo le foto in attesa il cui riferimento è ancora nel testo; in
   modifica, si tolgono solo le foto il cui riferimento c'era all'inizio della modifica e non c'è più (le foto di una voce
   nata come nota veloce, senza riferimento, restano).
7. **Export** (spec §6): `IExportFormatter.Write` restituisce `ExportOutput(byte[] Document, IReadOnlyList<ExportFile>
   Files)`; `ExportFile(string RelativePath, byte[] Content)`. Markdown con foto → cartella `<nome>/<nome>.md` +
   `images/<id 32 hex>.<ext>`, riferimenti relativi; se la cartella esiste già, `<nome> (2)`, `(3)`…; ogni voce è un titolo
   `#### yyyy-MM-dd HH:mm` seguito dal suo markdown **intero** (non più schiacciato su una riga). HTML: l'immagine al posto
   del riferimento (data URI). PDF: il riferimento si toglie dal testo, le foto restano in fondo come oggi. JSON e CSV: testo
   così com'è. `ExportAttachmentRef` prende `Guid Id` posizionale; `RelativePath` diventa `images/<id 32 hex>.<ext>`.
8. **Cronologia**: le righe del diario mostrano i chip dei tag (stile di F2d-1) e il riferimento a una foto come "[foto]".
9. **Editor, perimetro minimo** (spec §6): titoli 1-3, grassetto, corsivo (ci sono), elenchi puntati (`- `, `* `, `+ `,
   resi come "•") e numerati (`1. `, numero in tono attenuato), citazioni (`> ` sostituito da una barra, testo attenuato),
   `#tag` nel colore d'accento, immagini; Ctrl+B / Ctrl+I avvolgono la selezione (senza selezione inseriscono la coppia di
   marcatori con il cursore in mezzo); Invio su una voce d'elenco ne apre un'altra (numero +1), Invio su una voce vuota
   chiude l'elenco. Nessuna barra degli strumenti oltre "Inserisci immagine…".
10. **Pulizia delle sonde**: `LivePreviewEditorControl` (+ colorizer, generator, scanner) diventa `DiaryEditor`;
    `MarkdownEditorControl` di Avalonia e il pacchetto `Markdown.Avalonia` si tolgono se nient'altro li usa.

## Review Focus

1. **Crash mentre si scrive**: testo e riga Tag scritti, processo ucciso prima di Salva → alla riapertura tornano; se la
   scrittura della bozza fallisce, un messaggio lo dice → Task 4.
2. **Riferimenti e foto non allineati**: riferimento cancellato prima di Salva → la foto non entra; riferimento a un id
   sconosciuto → tolto al salvataggio; voce nata in nota veloce modificata in modalità diario → le sue foto restano → Task 3.
3. **Tastiera nell'editor**: Invio con il menu `#` aperto sceglie il suggerimento e non va a capo; Ctrl+B senza selezione;
   Invio su voce d'elenco vuota chiude l'elenco → Task 6 e Task 7.
4. **Export Markdown**: due foto con lo stesso nome file non si sovrascrivono; una foto senza contenuto (`is_missing`) non
   produce un file vuoto né un link rotto; cartella già esistente → `(2)` → Task 2.
5. **Rileggere i giorni prima**: scegliendo ieri nel calendario la colonna Diario mostra le voci di ieri rese con le
   immagini, e una voce nuova scritta lì va su ieri → Task 7.

---

### Task 1: Riferimenti alle foto in Core, id alle foto importate, riassunti per la Cronologia

**Files:**
- Create: `src/Recordum.Core/Text/PhotoRefs.cs`, `tests/Recordum.Core.Tests/Text/PhotoRefsTests.cs`
- Modify: `src/Recordum.Core/Models/ImportedPhoto.cs`, `src/Recordum.Data/Services/SkiaPhotoImporter.cs`,
  `src/Recordum.Data/Services/AttachmentService.cs` (`ToEntity` usa `photo.Id`),
  `src/Recordum.Core/Abstractions/Services/IDiaryService.cs`, `src/Recordum.Data/Services/DiaryService.cs`,
  `src/Recordum.Presentation/ViewModels/HabitScreen/CompletionListViewModel.cs` (solo per compilare: usa il nuovo metodo),
  fake dei test (`FakeDiaryService`)
- Test: `tests/Recordum.Data.Tests/Services/DiaryServiceTagTests.cs` (o accanto), test dell'importer

**Interfaces:**
- Produces:
  - `public static class PhotoRefs` in `Recordum.Core.Text`:
    - `string Format(Guid id, string filename)` → `![filename](photo:<id:N>)` (le `]` e i ritorni a capo del nome si
      tolgono);
    - `IReadOnlyList<PhotoRef> Find(string markdown)` con `readonly record struct PhotoRef(int Start, int Length, Guid Id)`;
      ignora il codice inline e a blocchi come `InlineTags`;
    - `string Replace(string markdown, Func<PhotoRef, string> replacement)`.
  - `ImportedPhoto(Guid Id, string Filename, string MimeType, byte[] Content, byte[] Thumbnail)` — `Id` primo,
    posizionale; `SkiaPhotoImporter` lo assegna con `Guid.NewGuid()`.
  - `IDiaryService.GetEntrySummariesByCompletionsAsync(IReadOnlyCollection<Guid> completionIds, CancellationToken ct)` →
    `IReadOnlyDictionary<Guid, DiaryEntrySummary>`, con `record DiaryEntrySummary(Guid NoteId, string Body,
    IReadOnlyList<EntryTag> Tags)` in `Recordum.Core.Models`; **sostituisce** `GetEntryBodiesByCompletionsAsync` (una
    query per i corpi, una per i tag; niente N+1).

- [ ] **Step 0** — rimisurare la baseline (vedi Global Constraints) e scriverla nel ledger.
- [ ] **Step 1 — test, rossi**: 1.1 `Format(id, "pasta.jpg")` → `![pasta.jpg](photo:<id:N>)`; `Find` su quel testo → un
  `PhotoRef` con lo stesso id, `Start`/`Length` che coprono tutta la sintassi; 1.2 `Find` ignora `` `![](photo:…)` `` e un
  blocco ``` ``` ```; id non valido (`photo:xyz`) → nessun risultato; 1.3 `Replace` con due riferimenti → entrambi
  sostituiti, il resto intatto; 1.4 `ImportedPhoto` importata due volte dallo stesso file → id diversi; salvata con
  `SaveEntryAsync` → la riga `attachment` ha `Id == photo.Id`; 1.5 riassunti: due voci, una con tag di riga e inline →
  dizionario per completion con corpo e tag ordinati per nome; completion senza nota → assente.
- [ ] **Step 2** — implementazione, verdi; mutazione su 1.4 (`ToEntity` torna a `Guid.NewGuid()` → rosso).
- [ ] **Step 3** — commit `feat: riferimenti alle foto nel testo e riassunti delle voci (F2d-2)`.

### Task 2: Export con le immagini

**Files:**
- Create: `src/Recordum.Plugins.Contracts/Export/ExportOutput.cs`
- Modify: `IExportFormatter.cs`, `ExportFormatterBase.cs`, i cinque formatter in
  `src/Recordum.Plugins.ExportEngine/Formatters/`, `src/Recordum.Plugins.Contracts/Export/ExportModel.cs`
  (`ExportAttachmentRef`), `src/Recordum.Data/Services/Queries/ExportQueryService.cs`,
  `src/Recordum.Presentation/ViewModels/Export/ExportDialogViewModel.cs`, `src/Recordum.WPF/Services/WpfSaveFileDialog.cs`
  (crea la cartella madre), fake dei test
- Test: `tests/Recordum.Plugins.ExportEngine.Tests/Formatters/*`, `tests/Recordum.Presentation.Tests/...ExportDialog...`

**Interfaces:**
- Consumes: `PhotoRefs` (Task 1).
- Produces: `sealed record ExportOutput(byte[] Document, IReadOnlyList<ExportFile> Files)` con
  `static ExportOutput Single(byte[] document)`; `sealed record ExportFile(string RelativePath, byte[] Content)`;
  `IExportFormatter.Write(ExportModel) → ExportOutput`; `ExportAttachmentRef(Guid Id, string Filename, string MimeType,
  string RelativePath)` (+ `Content` com'è). Contracts resta senza IO: chi scrive i file è l'host.

**Decisioni e trappole:** decisione 7. L'host (`ExportDialogViewModel`) con `Files` vuoto scrive il documento nel percorso
scelto come oggi; con `Files` non vuoto, dal percorso scelto `dir/nome.md` ricava la cartella `dir/nome/` (o `nome (2)`…
se esiste), ci scrive `nome.md` e ogni file al suo `RelativePath`, tutto via `ISaveFileDialog.WriteAsync` (un `RelativePath`
con `..` o assoluto si rifiuta). Le foto dell'export sono in chiaro per scelta dell'utente: è l'unica uscita prevista.

- [ ] **Step 1 — test, rossi**: 2.1 Markdown, nota con `![a.jpg](photo:<id>)` e contenuto → `Files` contiene
  `images/<id:N>.jpg` con quei byte, il documento contiene `![a.jpg](images/<id:N>.jpg)`; 2.2 (Review Focus 4) due foto
  di nome `IMG_0001.jpg` → due file distinti; foto senza contenuto → nessun file e nel testo il riferimento diventa
  `[a.jpg — missing]`; 2.3 Markdown, voce con titoli ed elenco → il suo markdown esce intero sotto `#### <data ora>`;
  2.4 HTML → `<img src="data:image/jpeg;base64,…">` dove stava il riferimento, nessun `photo:` nel documento; 2.5 PDF → il
  testo non contiene `photo:`; 2.6 CSV/JSON → `Files` vuoto; 2.7 host: con `Files` → scritti `dir/nome/nome.md` e
  `dir/nome/images/…`; cartella esistente → `dir/nome (2)/…`; `RelativePath` `../x` → nessuna scrittura, errore.
- [ ] **Step 2** — implementazione, verdi.
- [ ] **Step 3** — commit `feat(export): Markdown come cartella con le immagini, riferimenti risolti (F2d-2)`.

### Task 3: Modalità diario nel ViewModel

**Files:**
- Modify: `src/Recordum.Presentation/ViewModels/HabitScreen/Journal/JournalQuickEntryViewModel.cs`,
  `JournalEntryRowViewModel.cs`, `src/Recordum.Presentation/ViewModels/HabitScreen/HabitScreenViewModel.cs` (passa la
  modalità; mostra il chip Diario per i diari)
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/JournalQuickEntryViewModelTests.cs` (o un file
  `JournalDiaryModeTests.cs` accanto)

**Interfaces:**
- Consumes: `PhotoRefs`, `ImportedPhoto.Id` (Task 1).
- Produces (Task 7 le lega):
  - `bool IsDiaryMode { get; }` (fissato al caricamento: `LoadAsync(HabitNode, DayBoundary, bool isDiaryMode, ct)`, parametro
    obbligatorio);
  - `string ActiveText { get; set; }` — in modalità diario il testo della voce in modifica se c'è, altrimenti `Draft`;
    `TagEditorViewModel ActiveTags` idem; notificano quando cambia la voce in modifica;
  - `JournalEntryRowViewModel? EditingEntry` e `string? EditingBanner` ("Stai modificando la voce delle {0}");
  - `Task<(string Text, int Caret)?> InsertPhotosAsync(string text, int caret, CancellationToken ct)` — sceglie i file
    (stesso dialogo e stessi messaggi d'errore di `AddPhoto`), importa, aggiunge alla voce attiva, inserisce al cursore
    una riga `PhotoRefs.Format` per foto; null se nessuna foto è entrata;
  - `IReadOnlyDictionary<Guid, byte[]?> PhotoThumbnails` — miniature delle foto in attesa e di quelle delle voci del
    giorno (null = mancante), per l'editor;
  - `JournalEntryRowViewModel.UnreferencedPhotos` — le foto della voce il cui riferimento non è nel testo (la striscia di
    miniature mostra solo queste in modalità diario).

**Decisioni e trappole:** decisioni 1, 4, 5, 6. `BeginEdit` con un'altra modifica che ha cambiamenti chiede conferma; "No"
lascia tutto com'era. Al salvataggio in modalità diario: `DiaryEntryDraft.Photos` = in attesa con riferimento nel testo;
riferimenti a id né in attesa né allegati alla voce → tolti dal testo salvato. La voce vuota resta rifiutata con le
regole di F2d-1 (contano le foto che restano).

- [ ] **Step 1 — test, rossi**: 3.1 `InsertPhotosAsync("Cena\n", 5)` con una foto → testo `Cena\n![x.jpg](photo:<id>)\n`,
  cursore dopo il riferimento, una foto in attesa; 3.2 (Review Focus 2) riferimento cancellato dal testo → Salva manda
  `Photos` vuoto; riferimento a un id sconosciuto → il testo salvato non lo contiene; 3.3 voce con una foto senza
  riferimento (nata in nota veloce), modificata in modalità diario cambiando solo il testo → `RemovedPhotoIds` vuoto;
  voce con riferimento cancellato in modifica → quella foto in `RemovedPhotoIds`; 3.4 `BeginEdit` → `ActiveText` è il
  testo della voce, `Draft` intatto; Annulla → `ActiveText == Draft` di prima; 3.5 modifica con cambiamenti aperta,
  `BeginEdit` su un'altra voce, conferma "No" → la prima resta in modifica con i suoi cambiamenti; "Sì" → si apre la
  seconda; vale anche in nota veloce (il parametro muto: `IsDiaryMode` falso, stessa domanda); 3.6 `HabitScreenViewModel`:
  diario con gesto `Diary` → composer con `IsDiaryMode` vero; gesto `QuickNote` → falso.
- [ ] **Step 2** — implementazione, verdi; mutazione su 3.3 (togliere ogni foto senza riferimento → rosso).
- [ ] **Step 3** — commit `feat: modalità diario del composer, immagini nel testo (F2d-2)`.

### Task 4: La bozza non si perde

**Files:**
- Modify: `JournalQuickEntryViewModel.cs` (il costruttore prende `IAppSettingService`; il compilatore elenca i chiamanti),
  `HabitScreenViewModel.cs`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/JournalDraftTests.cs`; un test d'integrazione in
  `tests/Recordum.Data.Tests` solo se il fake non basta a provare il giro completo

**Interfaces:**
- Consumes: `IAppSettingService.GetAsync/SetAsync` (esistente).
- Produces: `Task FlushDraftAsync()` (attende la scrittura in volo; per i test — la chiusura della tab non attende niente:
  la finestra di perdita è una sola scrittura di pochi ms).

**Decisioni e trappole:** decisione 3. Si scrive al cambio di `Draft`, della riga Tag della voce nuova e, in modalità
diario, del testo e dei tag della voce in modifica; mai durante `LoadAsync` (non riscrivere la bozza appena letta). Dopo
un Salva riuscito la parte salvata si azzera. JSON con `System.Text.Json`; un valore illeggibile vale "nessuna bozza".

- [ ] **Step 1 — test, rossi**: 4.1 (Review Focus 1) scrivi testo e un tag, `FlushDraftAsync`, nuovo composer sullo
  stesso fake di `IAppSettingService` → `Draft` e tag tornano; 4.2 modifica aperta su una voce del giorno, nuovo composer
  → la voce torna in modifica con il suo testo; la voce non è più nel giorno → la parte di modifica si scarta, la bozza
  nuova resta; 4.3 dieci modifiche mentre la prima scrittura è bloccata (fake con `TaskCompletionSource`) → al massimo una
  scrittura in volo e l'ultima scritta è l'ultimo testo; 4.4 `SetAsync` lancia → `ErrorMessage` valorizzato, il testo
  resta; 4.5 Salva riuscito → la chiave vale stringa vuota; 4.6 `LoadAsync` non chiama `SetAsync`.
- [ ] **Step 2** — implementazione, verdi; mutazione su 4.3 (scrittura senza coalescenza fuori ordine → rosso).
- [ ] **Step 3** — commit `feat: la bozza del diario si salva a ogni modifica e torna all'apertura (F2d-2)`.

### Task 5: Tag e foto nella Cronologia

**Files:**
- Modify: `CompletionListViewModel.cs`, `CompletionRowViewModel.cs`,
  `src/Recordum.UI/Views/HabitScreen/Records/CompletionListView.axaml`
- Test: `tests/Recordum.Presentation.Tests/...CompletionList...`, `tests/Recordum.UI.Tests/HabitScreen/...`

**Interfaces:**
- Consumes: `GetEntrySummariesByCompletionsAsync` (Task 1), `TagChip` (F2d-1), `PhotoRefs.Replace`.
- Produces: `CompletionRowViewModel.TagChips` (`IReadOnlyList<TagChip>`, vuota fuori dai diari).

- [ ] **Step 1 — test, rossi**: 5.1 riga di diario con tag di riga e inline → due chip con le due classi; 5.2 corpo con un
  riferimento → il testo mostrato contiene "[foto]" ("[photo]" in inglese) e non `photo:`; 5.3 Avalonia: la riga mostra i
  chip (asserzione strutturale), in `en_US` e `it_IT`.
- [ ] **Step 2** — implementazione, verdi.
- [ ] **Step 3** — commit `feat: chip dei tag e foto nella Cronologia del diario (F2d-2)`.

### Task 6: Il controllo `DiaryEditor`

**Files:**
- Create (per rinomina dalla sonda): `src/Recordum.UI/Controls/DiaryEditor/DiaryEditor.axaml(.cs)`,
  `DiaryLineScanner.cs`, `DiaryColorizer.cs`, `DiaryElementGenerator.cs`
- Delete: `src/Recordum.UI/Controls/LivePreviewEditorControl.*`, `MarkdownLineScanner.cs`,
  `MarkdownLivePreviewColorizer.cs`, `MarkdownLivePreviewMarkerGenerator.cs`, `MarkdownEditorControl.axaml(.cs)` (Avalonia),
  il riferimento a `Markdown.Avalonia` in `Recordum.UI.csproj` e `Directory.Packages.props` se nient'altro lo usa
- Modify: `tests/Recordum.UI.Tests/ControlGalleries.cs`, `LivePreviewStructureTests.cs` → `DiaryEditorStructureTests.cs`
- Test: `tests/Recordum.UI.Tests/Controls/DiaryEditorStructureTests.cs`, `DiaryEditorKeyboardTests.cs`,
  `tests/Recordum.UI.Tests/Controls/DiaryLineScannerTests.cs`

**Interfaces:**
- Consumes: `PhotoRefs` (Task 1), `InlineTags` (F2d-1).
- Produces: `DiaryEditor : UserControl` con
  - `string Markdown` (two-way), `bool IsReadOnly` (sola lettura: nessuna riga del cursore, tutto reso),
    `IReadOnlyDictionary<Guid, byte[]?>? Photos` (miniature; un id assente o null → segnaposto "foto non salvata" /
    "foto mancante");
  - `int CaretOffset { get; set; }`, `Rect? CaretBounds()` (rettangolo del cursore nelle coordinate del controllo, per il
    popup `#`), `void ReplaceText(string text, int caret)`;
  - evento `TextOrCaretChanged`; con un `KeyDown` intercettabile dall'host **prima** dell'editor (per frecce/Invio/Tab/Esc
    del menu `#`).

**Decisioni e trappole:** decisione 9. Lo scanner resta la fonte unica per stile e marcatori nascosti; nascondere = un
elemento con testo vuoto e `documentLength` pari al marcatore (offset del documento intatti); l'immagine = un
`InlineObjectElement` con un `Image` (bitmap decodificata una volta per id e tenuta in cache nel controllo). Il `#` di un
titolo (`# `) non è un tag. Font serif per il testo. Un'immagine con larghezza massima pari a quella della riga.

- [ ] **Step 1 — test, rossi** (scanner, puri): 6.1 `- uno`, `* uno`, `+ uno` → marcatore elenco di 2; `12. dodici` →
  numerato; `> citazione` → marcatore di 2; `#pranzo` → span tag; `# Titolo` → titolo, nessun tag; riga con
  `![a](photo:<id>)` → un'immagine con quell'id. (Strutturali, headless, con priming) 6.2 fuori dalla riga del cursore la
  riga visuale di `> citazione` è più corta di 2 caratteri, sulla riga del cursore no; 6.3 con `Photos` che contiene l'id,
  la riga dell'immagine contiene un `Image` di larghezza > 0; id assente → segnaposto testuale; 6.4 `IsReadOnly` → nessuna
  riga resta grezza; 6.5 (Review Focus 3) Ctrl+B con "ciao" selezionato → `**ciao**`; senza selezione → `****` con il
  cursore a 2; Ctrl+I analogo con `*`; Invio dopo `- uno` → nuova riga `- `; dopo `3. tre` → `4. `; Invio su `- ` vuoto →
  la riga si svuota e l'elenco finisce. PNG chiaro/scuro negli artefatti con asserzione accanto.
- [ ] **Step 2** — implementazione, verdi; build senza `Markdown.Avalonia` se rimosso.
- [ ] **Step 3** — commit `feat(ui): DiaryEditor — elenchi, citazioni, tag, immagini, scorciatoie (F2d-2)`.

### Task 7: La colonna Diario con l'editor

**Files:**
- Modify: `src/Recordum.UI/Views/HabitScreen/Journal/JournalQuickEntryView.axaml(.cs)` (ramo modalità diario),
  `src/Recordum.UI/Views/HabitScreen/HabitScreenView.axaml` (chip Diario visibile per i diari: disfa il Ruling 5 di F2d-1)
- Create (se serve a non duplicare): `src/Recordum.UI/Views/HabitScreen/Journal/TagMenuController.cs` — la logica del
  popup `#` (posizione sotto il cursore, frecce/Invio/Tab/Esc, chiusura fuori fuoco tranne col puntatore sul menu) oggi nel
  code-behind della nota veloce, riusata da `TextBox` e `DiaryEditor`
- Test: `tests/Recordum.UI.Tests/HabitScreen/JournalDiaryEditorViewTests.cs`

**Interfaces:**
- Consumes: Task 3 (`IsDiaryMode`, `ActiveText`, `ActiveTags`, `InsertPhotosAsync`, `PhotoThumbnails`, `EditingBanner`,
  `UnreferencedPhotos`), Task 4, Task 6.
- Produces: nessuna API.

**Decisioni e trappole:** decisione 4. Modalità diario: in alto la striscia di modifica (solo se `EditingEntry`), poi
`DiaryEditor` legato a `ActiveText`, riga Tag su `ActiveTags`, "Inserisci immagine…" (applica il risultato di
`InsertPhotosAsync` con `ReplaceText`), Salva, errore; sotto, le voci del giorno: ora, `DiaryEditor` in sola lettura,
chip, miniature di `UnreferencedPhotos`, Modifica, Elimina. Modalità nota veloce: la view di F2d-1 invariata. Le due
modalità sono due `DataTemplate` scelti per contenuto, mai entrambe costruite. L'altezza di un `DiaryEditor` in sola
lettura dentro una lista si adatta al testo (misurare: AvaloniaEdit dentro uno `StackPanel` può non farlo).

- [ ] **Step 1 — test, rossi** (headless, DB privato): 7.1 diario con gesto `Diary`: da Oggi si apre la colonna Diario
  con un `DiaryEditor` e nessuna `TextBox` del composer; gesto `QuickNote` → il contrario; 7.2 scrivere `#pra` nell'editor
  → menu con `pranzo`; Invio → `#pranzo ` e nessun a capo (Review Focus 3); 7.3 "Inserisci immagine…" con una fixture,
  Salva → la voce compare sotto, resa, con un `Image`; nessuna miniatura duplicata nella striscia; 7.4 Modifica su quella
  voce → striscia "Stai modificando…" e testo nell'editor; Annulla → torna la bozza di prima; 7.5 (Review Focus 5) ieri
  scelto nel calendario → le voci di ieri; una voce nuova salvata lì ha la completion su ieri; 7.6 chip Diario visibile per
  un diario, assente per un'azione; 7.7 chiudere la colonna non costruisce l'editor (ricerca nell'intera view); 7.8 PNG
  della colonna con una voce resa, chiaro e scuro, asserzione accanto. Suite in `en_US` e `it_IT`.
- [ ] **Step 2** — implementazione, verdi.
- [ ] **Step 3** — commit `feat(ui): editor del diario della sera nella colonna Diario (F2d-2)`.

### Task 8: Review, documenti, checklist

**Files:**
- Modify: `docs/phase-4-issues.md` (sezione "Onda F2d-2"; F2d1-6 e F2d1-7 chiusi), `docs/ROADMAP.md`, `docs/MILESTONES.md`

- [ ] **Step 1** — review dell'intero branch (controllore, modello più capace) e CI verde su entrambi i job.
- [ ] **Step 2** — documenti: tracker, ROADMAP "Dove siamo", MILESTONES; nella ROADMAP la checklist a schermo per
  l'autore: diario della sera con titoli, elenchi (Invio che continua e chiude), citazione, Ctrl+B/I, `#` con il mouse e
  con la tastiera, un'immagine inserita e vista nel testo; Salva e rilettura dalla Cronologia (chip, "[foto]"); modifica
  di una voce e Annulla; chiudere la finestra a metà frase e riaprire (la bozza torna); `kill -9` a metà frase e
  riaprire; export Markdown aperto in Obsidian con le immagini; export HTML con l'immagine al suo posto. Commit e push.
