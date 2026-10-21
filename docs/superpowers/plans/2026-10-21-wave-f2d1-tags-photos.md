# Onda F2d-1 — Tag sulle voci e foto cifrate — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dopo F2d-1, su Avalonia il diario del cibo si usa ogni giorno: dalla fascia "Oggi" si apre la nota veloce, si
scrive con i tag `#` suggeriti e con la riga "Tag", si allegano foto ridimensionate e ripulite (D10), e nessuna foto esiste
più in chiaro sul disco: stanno nel database SQLCipher.

**Architecture:** Schema 2.19 aggiunge `note_tag` e tre colonne ad `attachment` (contenuto, miniatura, "mancante").
`IDiaryService` diventa l'unico punto che scrive voce + tag + foto in una transazione; un `IPhotoImporter` (SkiaSharp, in
Data) normalizza le foto al momento della scelta; un passo d'avvio importa nel BLOB i file di `attachments/`. La shell
Avalonia porta la colonna Diario con la nota veloce, la fascia "Oggi" del diario e la scelta D10 nella pagina "Dati e
backup". WPF continua solo a mostrare le foto.

**Tech Stack:** C# / .NET 10; EF Core 10 + SQLCipher; SkiaSharp 3.119.4 (già in `Directory.Packages.props`); Avalonia 12 +
`Avalonia.Headless.XUnit` (xunit.v3); `CommunityToolkit.Mvvm`.

**Spec:** [`docs/superpowers/specs/2026-10-21-wave-f2d-diary-design.md`](../specs/2026-10-21-wave-f2d-diary-design.md) §5
(F2d-1), §2-4 e §8. Forma del piano (D5): decisioni, interfacce, obblighi di test, trappole.

## Global Constraints

- **Branch**: `f2d1-tags-photos`, da `f2c-data-safety` (pila non mergiata). Mai commit su `main`; cambiare branch in una
  chiamata separata. Aggiornato un `.md` → commit e push subito; dopo ogni push si guarda la CI.
- **Ambiente Linux**: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"`. Build:
  `dotnet build Recordum.slnx -p:EnableWindowsTargeting=true --nologo`. **Mai `-p:UseAppHost=false`**.
- **Baseline**: si misura nel Task 1 Step 0 (Core, Data `Category!=WindowsOnly`, Presentation, UI `Category!=Performance`,
  Plugins, ExportEngine) e si scrive nel ledger; si controlla sempre il totale eseguito.
- **Schema 2.18 → 2.19** in una sola migrazione (Task 1): `dotnet ef migrations add NoteTagAttachmentBlobV219 --project
  src/Recordum.Data --startup-project src/Recordum.Data`; `#pragma warning disable CA1861` dopo `#nullable disable`; il
  bump di `schema_version` tocca 8 riferimenti nei test, nomi dei metodi compresi; mai editare a mano `*.Designer.cs` e
  `*ModelSnapshot.cs`.
- **Privacy** (spec §3): nessuna foto scritta in chiaro fuori da `TempDirectory` (meglio: mai su disco); il file scelto
  dall'utente non si modifica mai; GPS e altri metadati via con l'impostazione predefinita.
- **Il parametro muto** (CLAUDE.md §7): i parametri nuovi sono obbligatori; dove si conserva una firma vecchia, un test prova
  che il suo default fa la cosa giusta.
- **Tempo** solo da `IClock`; il giorno da `DayBoundary`.
- **WPF congelato**: solo il cambiamento minimo che i contratti condivisi impongono (foto mostrate dal BLOB).
- **Avalonia**: `InitializeComponent()`; `x:DataType` su ogni view e template; `[AvaloniaFact]`; `using Xunit;`; DB privato
  (`tests/Recordum.UI.Tests/Fixtures/PrivateDbHost.cs`); un `ContentControl` con contenuto nullo non deve costruire il
  template (CLAUDE.md §8, primo punto). Suite UI provata in `en_US` e `it_IT`.
- **Stringhe**: `Strings.resx` + `Strings.it.resx` + `Strings.Designer.cs` **solo via shell**; riusare le chiavi
  `Diario_QuickEntry_*` esistenti.
- **Linguaggio visivo**: icone Lucide; pulsanti di sola icona con `AutomationProperties.Name` e `ToolTip.Tip`; chiaro e scuro.
- **`TreatWarningsAsErrors`** globale.

## Decisioni prese scrivendo il piano

L'esecutore non le ri-discute; sono qui perché l'autore possa disfarle prima dell'esecuzione.

1. **Impostazione D10**: chiave `photos.import_mode` in `app_setting` (tabella chiave/valore: niente CHECK, un valore
   sconosciuto vale come il predefinito), enum `PhotoImportMode { ResizeAndClean, CleanOnly, KeepOriginal }`, assente =
   `ResizeAndClean`. La scelta sta in una sezione "Foto" della pagina "Dati e backup" (l'unica pagina di Impostazioni su
   Avalonia).
2. **Formati**: in ingresso JPEG, PNG, WebP (riconosciuti da `SKCodec.EncodedFormat`, non dall'estensione); file oltre
   **50 MB** rifiutati. In uscita PNG resta PNG, il resto diventa JPEG qualità 85. Lato lungo 2048 px, mai ingrandire.
   Miniatura: lato lungo 320 px, stesso formato del contenuto, generata sempre (anche con `KeepOriginal`, ed è pulita).
   `KeepOriginal` conserva i byte del file così come sono, ma il file si decodifica comunque (validazione e miniatura).
3. **La foto si importa quando la si sceglie**, non al salvataggio: l'errore su un file si vede subito, e la voce porta in
   memoria `ImportedPhoto` già normalizzate fino a "Salva" (nessun percorso trattenuto).
4. **Tag solo sulle voci del diario** (`IDiaryService`); `INoteService` e le note Veloce non li gestiscono in F2d-1 (il
   design diceva "gratis": non lo è, servirebbe una UI che non esiste).
5. **Modifica di una voce**: testo, riga Tag, foto aggiunte e foto tolte si applicano tutte a "Salva" in una transazione;
   "Annulla" le scarta. Togliere una foto in modifica non chiede conferma (Annulla è la rete).
6. **Gesto del diario**: `HabitScreenViewModel` tratta il diario come nota veloce quando il gesto è `QuickNote` **oppure**
   `IEditorAvailability.IsAvailable` è falso. Su Avalonia (`NullEditorAvailability`) ogni diario apre la nota veloce fino a
   F2d-2; su WPF senza WebView2 idem (era un ripiego peggiore).
7. **Import dei file esistenti** (passo d'avvio, idempotente, a ogni avvio): per ogni file `attachments/{noteId}/{idN}.{ext}`
   - riga trovata senza contenuto → contenuto = byte del file (non normalizzati), miniatura se decodificabile, commit,
     **poi** cancella il file;
   - riga trovata con contenuto → il file è un avanzo (crash fra commit e cancellazione): si cancella;
   - nome che è un Guid senza riga → voce già cancellata dall'utente (il vecchio `DeleteEntryAsync` lasciava i file): si
     cancella, con un log;
   - nome che non è un Guid, o file illeggibile → resta lì, con un log;
   - alla fine le righe ancora senza contenuto il cui file non esiste diventano `is_missing = 1`. Le sottocartelle vuote si
     tolgono; `attachments/` resta (la ricrea comunque `RecordumOptions`).
   Backup e restore non cambiano (spec §5.3, precisato).
8. **L'ora di una voce si mostra nel fuso locale** (`IClock.LocalTimeZone`): oggi la riga lega `CreatedAt` (UTC) a `HH:mm`.
9. **Export**: `ExportAttachmentRef` perde `AbsolutePath` e prende `byte[]? Content`; HTML e PDF incorporano da lì; il
   Markdown resta com'è (link relativo) fino alla cartella di F2d-2.

## Review Focus

1. **L'ora delle voci**: una voce salvata alle 13:42 ora italiana mostra 13:42, non 11:42 o 12:42 (decisione 8) → Task 6.
2. **Un salvataggio che fallisce** (DB occupato, eccezione del servizio) lascia testo, tag e foto in attesa dove l'utente li ha
   lasciati, con un messaggio → Task 6.
3. **Una foto del telefono** con orientamento EXIF 6 e coordinate GPS esce dritta (altezza > larghezza) e senza segmento
   EXIF → Task 3.
4. **Un file sbagliato** (HEIC, testo rinominato `.jpg`, file troncato, oltre 50 MB) dà un messaggio che nomina il file;
   le altre foto scelte nello stesso colpo entrano → Task 3 e Task 6.
5. **Un import interrotto** (crash dopo il commit, prima della cancellazione) si completa al giro dopo senza duplicare né
   perdere; un file illeggibile non fa mai cancellare la riga → Task 5.

---

### Task 1: Schema 2.19

**Files:**
- Create: `src/Recordum.Core/Models/NoteTag.cs`, `src/Recordum.Core/Enums/NoteTagSource.cs`,
  `src/Recordum.Core/Models/ImportedPhoto.cs` (solo il record, vedi Task 3),
  `src/Recordum.Data/Configurations/NoteTagConfiguration.cs`, la migrazione `NoteTagAttachmentBlobV219`
- Modify: `src/Recordum.Data/Models/Attachment.cs`, `src/Recordum.Data/Configurations/AttachmentConfiguration.cs`,
  `src/Recordum.Data/RecordumDbContext.cs`, i test che fissano `schema_version`
- Test: `tests/Recordum.Data.Tests/Schema/NoteTagSchemaTests.cs`

**Interfaces:**
- Produces:
  - `enum NoteTagSource { Inline, Property }`, colonna TEXT con `ck_note_tag_source` (`source IN ('Inline','Property')`).
  - `sealed class NoteTag { Guid NoteId; Guid TagId; NoteTagSource Source; }` → tabella `note_tag`, PK `(note_id, tag_id,
    source)`, FK verso `note` e verso `tag` **entrambe `Cascade`** esplicite, indice `ix_note_tag_tag_id`;
    `DbSet<NoteTag> NoteTags`. Niente navigazione su `Note`.
  - `Data.Models.Attachment`: `byte[]? Content` (`content BLOB`), `byte[]? Thumbnail` (`thumbnail BLOB`), `bool IsMissing`
    (`is_missing INTEGER` default 0). FK `fk_attachment_note_id` resta `Restrict`.
  - `schema_version` = `2.19`.

- [ ] **Step 0** — misurare le baseline e scriverle nel ledger.
- [ ] **Step 1 — test, rossi**: 1.1 cancellare (con `ExecuteDelete`) una nota con due `note_tag` lascia zero righe in
  `note_tag`; 1.2 cancellare un `tag` lascia zero righe sue in `note_tag` e la nota intatta; 1.3 `PurgeService` su un'abitudine
  con voci taggate: zero `note_tag` residue; 1.4 la stessa coppia nota/tag può stare due volte con `source` diverso, non due
  volte con lo stesso; 1.5 `source = 'Altro'` viola il CHECK; 1.6 un `attachment` con `content` da 1 MB si rilegge identico.
- [ ] **Step 2** — entità, configurazioni, migrazione, bump a 2.19; verdi; mutazione: togliere `Cascade` da una FK → 1.1 o
  1.2 rosso.
- [ ] **Step 3** — commit `feat(data): schema 2.19 — note_tag e foto nel database (F2d-1)`.

### Task 2: Tag sulle voci in `IDiaryService`

**Files:**
- Create: `src/Recordum.Core/Text/InlineTags.cs`, `src/Recordum.Core/Models/DiaryEntryDraft.cs` (i due record di
  scrittura e `EntryTag`), `src/Recordum.Data/Services/NoteTagWriter.cs`
- Modify: `src/Recordum.Core/Abstractions/Services/IDiaryService.cs`, `src/Recordum.Data/Services/DiaryService.cs`, i fake
  di `IDiaryService` nei test (il compilatore li elenca)
- Test: `tests/Recordum.Core.Tests/Text/InlineTagsTests.cs`, `tests/Recordum.Data.Tests/Services/DiaryServiceTagTests.cs`

**Interfaces:**
- Consumes: `NoteTag`, `NoteTagSource`, `ImportedPhoto` (Task 1).
- Produces:
  - `static class InlineTags { static IReadOnlyList<string> Parse(string markdown); }` — tag in ordine di prima comparsa,
    distinti senza maiuscole (vince la prima grafia).
  - `sealed record DiaryEntryDraft(string Text, IReadOnlyCollection<string> PropertyTags, IReadOnlyList<ImportedPhoto> Photos);`
  - `sealed record DiaryEntryEdit(string Text, IReadOnlyCollection<string>? PropertyTags, IReadOnlyList<ImportedPhoto>
    AddedPhotos, IReadOnlyCollection<Guid> RemovedPhotoIds);` — `PropertyTags` null = riga Tag non toccata.
  - `sealed record EntryTag(string Name, NoteTagSource Source);`
  - `IDiaryService`:
    - `ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, DiaryEntryDraft draft, CancellationToken ct = default);`
    - `ValueTask EditEntryAsync(Guid noteId, DiaryEntryEdit edit, CancellationToken ct = default);`
    - `ValueTask<IReadOnlyDictionary<Guid, IReadOnlyList<EntryTag>>> GetEntryTagsAsync(IReadOnlyCollection<Guid> noteIds,
      CancellationToken ct = default);` — ordinati per nome; le note senza tag non compaiono.
    - Le firme vecchie restano come **metodi di default dell'interfaccia** (precedente: `IEditorAvailability.RuntimeVersion`):
      `SaveEntryAsync(h, d, text)` → bozza con riga Tag e foto vuote; `EditEntryAsync(noteId, text)` → modifica con
      `PropertyTags = null` e nessuna foto.
  - `static class NoteTagWriter { static Task SyncAsync(RecordumDbContext db, Guid noteId, string text,
    IReadOnlyCollection<string>? propertyTags, CancellationToken ct); }` — lavora sul contesto del chiamante (stessa
    transazione), crea i tag mancanti, riscrive le righe `Inline` dal testo e, se `propertyTags` non è null, le `Property`.

**Decisioni e trappole:** sintassi (spec §5.1): `#` a inizio parola (inizio testo o dopo uno spazio bianco o una
punteggiatura di apertura), seguito da lettere Unicode, cifre, `_`, `-`; almeno un carattere non cifra; massimo 64
caratteri (oltre: non è un tag); `#` seguito da spazio è un titolo; dentro `` `codice` `` e blocchi ``` non conta; `#`
dentro una parola (`C#`, `url#frag`) non conta. Il confronto dei nomi in memoria è `OrdinalIgnoreCase` (più largo di
`NOCASE` di SQLite, quindi non genera mai un doppione sull'indice unico). Il nome salvato è senza `#`. Un nome di riga Tag
vuoto o oltre 64 caratteri si scarta nel servizio (la UI lo impedisce già). `EditEntryAsync` non aggiunge una versione a
`NoteHistory` se il testo non cambia, ma applica comunque tag e foto. Gli eventi sul bus restano quelli di oggi.

- [ ] **Step 1 — test, rossi**: `InlineTagsTests` — 2.1 `"Pasta #pranzo e #Casa"` → `["pranzo","Casa"]`; 2.2 `"# Titolo"`,
  `"#2"`, `"C#"`, `` "`#no`" ``, un blocco ``` con `#no` dentro → nessun tag; 2.3 `"#pranzo #Pranzo"` → `["pranzo"]`;
  2.4 `"#città-vecchia_2"` → `["città-vecchia_2"]`; 2.5 65 caratteri → nessun tag. `DiaryServiceTagTests` — 2.6 save con
  `#pranzo` nel testo e `casa` in riga Tag → `GetEntryTagsAsync` dà `pranzo/Inline` e `casa/Property`, e `ITagService.ListAsync`
  contiene entrambi; 2.7 un tag `Pranzo` già esistente non si duplica scrivendo `#pranzo`; 2.8 edit che toglie `#pranzo` dal
  testo → il tag sparisce dalla voce ma resta nell'elenco; 2.9 la firma vecchia `EditEntryAsync(id, text)` non tocca i tag
  `Property` (il parametro muto); 2.10 edit con testo identico e riga Tag diversa: tag aggiornati, nessuna versione nuova in
  `NoteHistory`; 2.11 con un interceptor di test che lancia su `SaveChanges`, non resta né la nota né il tag nuovo.
- [ ] **Step 2** — implementazione, verdi; mutazione: togliere la cancellazione delle righe `Inline` in `SyncAsync` → 2.8
  rosso.
- [ ] **Step 3** — commit `feat: tag sulle voci del diario, nel testo e nella riga Tag (F2d-1)`.

### Task 3: Import delle foto (D10)

**Files:**
- Create: `src/Recordum.Core/Enums/PhotoImportMode.cs`,
  `src/Recordum.Core/Abstractions/Services/IPhotoImporter.cs`, `src/Recordum.Core/Errors/PhotoImportException.cs`,
  `src/Recordum.Data/Services/SkiaPhotoImporter.cs`
- Modify: `src/Recordum.Data/Recordum.Data.csproj` (SkiaSharp + i nativi per RID, col pattern condizionato di
  `Recordum.Plugins.ExportEngine.csproj`), `src/Recordum.Presentation/Bootstrap/RecordumServiceCollectionExtensions.cs`,
  `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`
- Test: `tests/Recordum.Data.Tests/Services/SkiaPhotoImporterTests.cs`, fixture in `tests/Recordum.Data.Tests/Fixtures/Photos/`

**Interfaces:**
- Produces:
  - (creato nel Task 1) `sealed record ImportedPhoto(string Filename, string MimeType, byte[] Content, byte[] Thumbnail);` —
    `Filename` è il nome del file scelto (solo nome, senza cartella).
  - `enum PhotoImportMode { ResizeAndClean, CleanOnly, KeepOriginal }`; costante `PhotoImportSettings.Key = "photos.import_mode"`
    (in Core, accanto all'enum).
  - `interface IPhotoImporter { Task<ImportedPhoto> ImportAsync(string sourcePath, CancellationToken ct); byte[]?
    TryMakeThumbnail(byte[] content); }` — `ImportAsync` legge la modalità da `IAppSettingService` (`GetEnumAsync`);
    lavora fuori dal thread chiamante (`Task.Run`) perché decodifica.
  - `sealed class PhotoImportException(string filename, PhotoImportFailure reason) : Exception`, `enum PhotoImportFailure
    { Unsupported, TooLarge, Unreadable }`.

**Decisioni e trappole:** decisione 2. Orientamento: `SKCodec.EncodedOrigin` si applica ai pixel prima di ridimensionare e
codificare, in tutte e tre le modalità per contenuto e miniatura (con `KeepOriginal` solo la miniatura). La codifica
SkiaSharp non scrive EXIF: è così che si pulisce. Il file sorgente si apre in sola lettura e non si scrive niente su disco.
Fixture: una JPEG piccola con orientamento 6 e un tag GPS, creata una volta con `exiftool` (comando in un `README.md` della
cartella delle fixture), più un file di testo rinominato `.jpg` e una JPEG troncata; HEIC si simula con l'header `ftypheic`.

- [ ] **Step 1 — test, rossi**: 3.1 (Review Focus 3) JPEG 4000×3000 con orientamento 6 e GPS, modalità predefinita → JPEG
  con lato lungo 2048, **altezza > larghezza**, nessun segmento APP1 `Exif` nei byte; 3.2 `CleanOnly` → stesse dimensioni
  (ruotate), nessun EXIF; 3.3 `KeepOriginal` → `Content` identico al file, miniatura senza EXIF; 3.4 PNG piccolo → resta PNG,
  non ingrandito; 3.5 miniatura con lato lungo 320; 3.6 (Review Focus 4) HEIC → `Unsupported`, testo `.jpg` → `Unsupported`,
  JPEG troncata → `Unreadable`, file oltre 50 MB (sparso) → `TooLarge`, e ogni eccezione porta il nome del file; 3.7 il
  file sorgente ha lo stesso hash prima e dopo; 3.8 valore sconosciuto in `photos.import_mode` → comportamento predefinito.
- [ ] **Step 2** — implementazione, verdi su Linux (e in CI su Windows); registrazione DI in entrambe le shell + test della
  registrazione.
- [ ] **Step 3** — commit `feat(data): import delle foto ridimensionate e senza metadati (D10, F2d-1)`.

### Task 4: Foto nel database

**Files:**
- Modify: `src/Recordum.Core/IAttachmentService.cs`, `src/Recordum.Core/Models/Attachment.cs`,
  `src/Recordum.Data/Services/AttachmentService.cs`, `src/Recordum.Data/Services/DiaryService.cs`,
  `src/Recordum.Plugins.Contracts/Export/ExportModel.cs`, `src/Recordum.Data/Services/Queries/ExportQueryService.cs`,
  `src/Recordum.Plugins.ExportEngine/Formatters/HtmlExportFormatter.cs`, `PdfExportFormatter.cs`,
  `src/Recordum.Presentation/ViewModels/HabitScreen/Notes/AttachmentViewModel.cs`, `NotesViewModel.cs`, le view WPF che
  legano `FilePath` (`JournalQuickEntryView.xaml`, `NotesTabView.xaml`) con un convertitore byte → `BitmapImage` in WPF
- Test: `tests/Recordum.Data.Tests/Services/AttachmentServiceTests.cs` (adattati), `DiaryServicePhotoTests.cs`, i test
  dell'export che toccano le immagini

**Interfaces:**
- Consumes: `ImportedPhoto`, `IPhotoImporter` (Task 3); `DiaryEntryDraft`/`DiaryEntryEdit` (Task 2).
- Produces:
  - `IAttachmentService`: `Task<Attachment> AddAsync(Guid noteId, ImportedPhoto photo, CancellationToken ct)` (sostituisce
    la versione col percorso); `Task<byte[]?> ReadContentAsync(Guid attachmentId, CancellationToken ct)`;
    `Task<byte[]?> ReadThumbnailAsync(Guid attachmentId, CancellationToken ct)`; `DeleteAsync`, `ListByNoteAsync`,
    `ListAllAsync`, `ResolveRelativePath` restano; **`ResolveAbsolutePath` sparisce**. Le liste non caricano i BLOB
    (proiezione).
  - `Core.Models.Attachment` + `bool IsMissing`.
  - `DiaryService.SaveEntryAsync`/`EditEntryAsync` scrivono e tolgono le foto della bozza nella **stessa** `SaveChanges`
    della nota.
  - `ExportAttachmentRef`: via `AbsolutePath`, nuova proprietà `init` `byte[]? Content`.
  - `AttachmentViewModel(Attachment attachment, byte[]? thumbnail)`: `Thumbnail`, `IsMissing`; via `FilePath`.

**Decisioni e trappole:** decisione 9. `DeleteAsync` non tocca più il disco. Un `RemovedPhotoIds` che appartiene a un'altra
nota si ignora (mai cancellare foto altrui). `NotesViewModel` (colonna Note, solo WPF) importa con `IPhotoImporter` come la
nota veloce. Il convertitore WPF decodifica con `DecodePixelWidth` e `CacheOption.OnLoad`.

- [ ] **Step 1 — test, rossi**: 4.1 save di una bozza con due foto → due righe con `content` e `thumbnail`, nessun file nella
  cartella `attachments/`; 4.2 se la nota non si salva (eccezione forzata) non resta nessuna foto; 4.3 edit con una foto
  aggiunta e una tolta → elenco giusto, in una transazione; 4.4 `RemovedPhotoIds` di un'altra nota → ignorato; 4.5
  `DeleteEntryAsync` → zero righe `attachment` della nota; 4.6 `ListByNoteAsync` non carica `content` (la query generata non
  seleziona la colonna: asserzione sul SQL loggato o sul tipo proiettato); 4.7 export HTML e PDF di una voce con foto
  contengono l'immagine (byte presenti nell'output), senza file su disco.
- [ ] **Step 2** — implementazione; build verde su tutta la soluzione (il compilatore elenca i chiamanti di
  `ResolveAbsolutePath` e di `AddAsync`); suite WPF serializzata verde in CI.
- [ ] **Step 3** — commit `feat: le foto stanno nel database cifrato (F2d-1)`.

### Task 5: Import all'avvio dei file esistenti

**Files:**
- Create: `src/Recordum.Data/Maintenance/AttachmentFileImport.cs`
- Modify: `src/Recordum.Presentation/Bootstrap/CoreInitializer.cs` (dopo `RetroactiveNoonBackfill`),
  `RecordumServiceCollectionExtensions.cs`, `src/Recordum.WPF/Bootstrap/ApplicationInitializer.cs` (stessa chiamata, una
  riga), `ServiceRegistrar.cs`
- Test: `tests/Recordum.Data.Tests/Maintenance/AttachmentFileImportTests.cs`

**Interfaces:**
- Consumes: `IPhotoImporter.TryMakeThumbnail` (Task 3); colonne del Task 1.
- Produces: `sealed class AttachmentFileImport(IDbContextFactory<RecordumDbContext> dbFactory, IPhotoImporter photos,
  IBackupStoragePaths paths, ILogger<AttachmentFileImport> logger) { Task<AttachmentImportReport> RunAsync(CancellationToken
  ct); }`, `sealed record AttachmentImportReport(int Imported, int LeftoversDeleted, int OrphansDeleted, int Skipped, int
  MarkedMissing);` — registrata scoped, chiamata in uno scope proprio come il backfill.

**Decisioni e trappole:** decisione 7. Un file per volta: commit, poi cancellazione. Un'eccezione di I/O su un file lo
salta (conteggiato in `Skipped`), non ferma l'avvio; un'eccezione del database invece ferma l'avvio come una migrazione
fallita. Il log non scrive contenuti, solo id e conteggi.

- [ ] **Step 1 — test, rossi**: 5.1 riga senza contenuto + file → contenuto uguale ai byte del file, miniatura presente,
  file sparito; 5.2 (Review Focus 5) riga **con** contenuto + file uguale → file cancellato, contenuto invariato; 5.3 file con
  Guid senza riga → cancellato; 5.4 `appunti.txt` → resta; 5.5 file illeggibile (permessi 000 su Unix) → resta, riga non
  marcata mancante, riga mai cancellata; 5.6 riga senza contenuto e senza file → `is_missing = 1`; 5.7 secondo giro → report
  tutto a zero; 5.8 un file leggibile ma non decodificabile entra lo stesso, con `thumbnail` null.
- [ ] **Step 2** — implementazione, verdi; mutazione: cancellare il file prima del commit → un test con commit che fallisce
  (contesto che lancia su `SaveChanges`) trova il file sparito, rosso.
- [ ] **Step 3** — commit `feat(data): all'avvio le foto esistenti entrano nel database (F2d-1)`.

### Task 6: La nota veloce in Presentation

**Files:**
- Create: `src/Recordum.Presentation/ViewModels/HabitScreen/Journal/TagCompletion.cs`,
  `src/Recordum.Presentation/ViewModels/HabitScreen/Journal/TagEditorViewModel.cs` (riga Tag + suggerimenti),
  `src/Recordum.Presentation/ViewModels/Backup/PhotoImportSettingsViewModel.cs`
- Modify: `JournalQuickEntryViewModel.cs`, `JournalEntryRowViewModel.cs`, `HabitScreenViewModel.cs` (decisione 6; il
  costruttore prende `IPhotoImporter` e `ITagService` per il composer — il compilatore elenca i call-site dei test), `DataAndBackupPageViewModel.cs` (espone la sezione Foto)
- Test: `tests/Recordum.Presentation.Tests/...` accanto ai test esistenti del composer, `TagCompletionTests.cs`,
  `PhotoImportSettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `IDiaryService` (Task 2, 4), `IPhotoImporter` (Task 3), `ITagService.ListAsync`, `IAttachmentService.ReadThumbnailAsync`.
- Produces:
  - `static class TagCompletion { static TagQuery? Find(string text, int caret); static (string Text, int Caret)
    Apply(string text, TagQuery query, string tagName); }`, `readonly record struct TagQuery(int Start, string Prefix);` —
    `Find` torna la parola `#…` che finisce al cursore (stesse regole di `InlineTags`), `Apply` la sostituisce con
    `#tagName ` e mette il cursore dopo lo spazio. Riusato dall'editor di F2d-2.
  - `TagEditorViewModel`: `ObservableCollection<string> Tags`, `string Query`, `IReadOnlyList<string> Suggestions` (tag
    esistenti per prefisso, senza maiuscole, massimo 8, esclusi quelli già scelti; ultima voce "Crea «…»" se il nome non
    esiste), comandi `Add(string)`, `Remove(string)`; nome vuoto o oltre 64 caratteri rifiutato.
  - `JournalQuickEntryViewModel`: costruttore + `IPhotoImporter photos, ITagService tags`; `PendingPhotos` diventa
    `ObservableCollection<ImportedPhoto>`; `TagEditorViewModel PropertyTags`; `IReadOnlyList<string> InlineSuggestions` e
    `void UpdateInlineQuery(string text, int caret)` / `(string Text, int Caret) AcceptInlineSuggestion(string name)` per il
    menu `#` della view; `Save` usa `DiaryEntryDraft` / `DiaryEntryEdit`.
  - `JournalEntryRowViewModel(Note note, IReadOnlyList<EntryTag> tags, IClock clock)`: `TimeDisplay` (ora locale `HH:mm`),
    `Tags` (unione, per nome), in modifica `EditTags` (`TagEditorViewModel`), foto in attesa e foto tolte.
  - `PhotoImportSettingsViewModel(IAppSettingService settings)`: `PhotoImportMode Mode` (salvato al cambio),
    `bool ShowsGpsWarning` (vero solo con `KeepOriginal`), `Task LoadAsync()`.

**Decisioni e trappole:** decisioni 3, 5, 6, 8. La guardia anti-sovrapposizione delle reload (`_reloadGeneration`) resta
così com'è. Un `PhotoImportException` produce un messaggio per file (chiavi nuove `Diario_QuickEntry_Error_PhotoUnsupported`,
`…_PhotoTooLarge`, `…_PhotoUnreadable`, con il nome del file) e non ferma le altre foto della stessa scelta. `AsyncRelayCommand`
non consulta `CanExecute`: la guardia `IsSaving` sta anche dentro `Save`.

- [ ] **Step 1 — test, rossi**: 6.1 `TagCompletion.Find("Pasta #pra", 10)` → `(6,"pra")`; `Find("# Tit", 5)` → null;
  `Apply` → `"Pasta #pranzo "` con cursore 14; 6.2 suggerimenti: con `Pranzo` e `pranzo-fuori` esistenti, query `pra` →
  entrambi + nessun "Crea"; query `pranzo` → `Pranzo`, `pranzo-fuori`, niente "Crea" (esiste senza maiuscole); 6.3 save con
  testo, riga Tag e due foto → `IDiaryService` riceve una sola `DiaryEntryDraft` con tutto; 6.4 (Review Focus 2) il servizio
  lancia → `Draft`, `PropertyTags.Tags` e `PendingPhotos` intatti, `ErrorMessage` valorizzato; 6.5 (Review Focus 4) tre file
  scelti, il secondo HEIC → due foto in attesa, un messaggio col nome del secondo; 6.6 (Review Focus 1) voce creata alle
  11:42 UTC con fuso `Europe/Rome` d'estate → `TimeDisplay` `13:42`; 6.7 modifica: togli una foto, aggiungine una,
  Annulla → nessuna chiamata al servizio, la riga torna com'era; Salva → una `DiaryEntryEdit` con entrambe; 6.8 decisione 6:
  diario con gesto `Diary` e `IEditorAvailability.IsAvailable == false` → `QuickEntryForDiario` non null; con
  `IsAvailable == true` → null (il parametro muto: lo stesso diario risponde diverso); 6.9 `PhotoImportSettingsViewModel`
  carica `CleanOnly` dal setting, cambiarla salva, `ShowsGpsWarning` solo con `KeepOriginal`.
- [ ] **Step 2** — implementazione, verdi; mutazione su 6.4 (svuotare il draft prima della chiamata al servizio → rosso).
- [ ] **Step 3** — commit `feat: nota veloce con tag e foto in Presentation (F2d-1)`.

### Task 7: Le view Avalonia

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/Journal/JournalQuickEntryView.axaml(.cs)`,
  `src/Recordum.UI/Views/HabitScreen/Journal/TagEditorView.axaml(.cs)`,
  `src/Recordum.UI/Views/HabitScreen/Today/JournalTodayView.axaml(.cs)`, `src/Recordum.UI/Converters/BytesToBitmapConverter.cs`
- Modify: `src/Recordum.UI/Views/HabitScreen/HabitScreenView.axaml` (chip e colonna Diario per i diari),
  `src/Recordum.UI/Views/HabitScreen/Today/TodayContentTemplate.cs` (ramo diario), `src/Recordum.UI/Views/Settings/DataAndBackupPage.axaml`
  (sezione Foto)
- Test: `tests/Recordum.UI.Tests/HabitScreen/JournalQuickEntryViewTests.cs`, `JournalTodayViewTests.cs`,
  `tests/Recordum.UI.Tests/Settings/DataAndBackupPagePhotoTests.cs`

**Interfaces:**
- Consumes: tutto il Task 6; `JournalTodayContent` (esistente, comando `Primary`).
- Produces: nessuna API; view legate con `x:DataType`.

**Decisioni e trappole:** mockup approvato (spec §5.1): composer (TextBox multilinea, menu `#` in un `Popup` sotto il
cursore con frecce/Invio/Tab/Esc, riga Tag con chip e "+ tag", "Aggiungi foto…", miniature in attesa con ✕, Salva),
poi "Oggi" con le voci (ora, testo, chip — `Inline` e `Property` con due classi di stile —, miniature, Modifica, Elimina).
Nella TextBox il `#` non si evidenzia (arriva con l'editor di F2d-2). Il menu `#` chiama `UpdateInlineQuery` su testo e
`CaretIndex`; Invio con il menu aperto accetta il suggerimento e **non** va a capo. La colonna Diario si mostra con
`ShowDiario`; il suo `ContentControl` è legato a `QuickEntryForDiario` con un `DataTemplate` per tipo (mai
`ContentTemplate` statico). Miniatura mancante (`IsMissing` o null) → icona Lucide `image-off` con tooltip. Il convertitore
decodifica con `Bitmap.DecodeToWidth` a 160 px.

- [ ] **Step 1 — test, rossi** (headless, DB privato): 7.1 da Oggi di un diario il pulsante apre la colonna Diario con il
  composer; 7.2 scrivere `#pra` mostra il menu con `pranzo`; Invio → testo `#pranzo ` e nessun a capo; 7.3 Salva con una
  foto (fixture) → la voce compare in "Oggi" con miniatura e chip, ora nel fuso dell'orologio finto; 7.4 la colonna Diario non
  costruisce il composer per un'abitudine non diario (ricerca nell'intera view); 7.5 pagina Dati e backup: tre scelte,
  l'avviso GPS solo con "Tieni l'originale"; 7.6 PNG dello stato con voce + foto, in chiaro e in scuro, negli artefatti con
  un'asserzione strutturale accanto. Suite in `en_US` e `it_IT`.
- [ ] **Step 2** — implementazione, verdi.
- [ ] **Step 3** — commit `feat(ui): nota veloce del diario su Avalonia, con tag e foto (F2d-1)`.

### Task 8: Sul DB vero, review, chiusura

**Files:**
- Modify: `docs/phase-4-issues.md` (sezione "Onda F2d-1"), `docs/ROADMAP.md`, `docs/MILESTONES.md`

- [ ] **Step 1** — prova a mano su una copia della cartella dati dell'autore (`--data-dir`, mai l'originale): avvio → le foto
  esistenti entrano nel DB, `attachments/` senza file, le voci vecchie mostrano le foto; una voce nuova con tag e foto;
  backup e restore della copia; `ls -lR` della cartella: nessuna immagine.
- [ ] **Step 2** — review dell'intero branch (controllore, modello più capace) e CI verde su entrambi i job.
- [ ] **Step 3** — documenti: tracker, ROADMAP "Dove siamo", MILESTONES; nella ROADMAP la checklist a schermo per l'autore:
  nota veloce dal Food diary e da un diario normale; `#` con suggerimenti e "Crea"; riga Tag; foto dal telefono (dritta,
  ridimensionata), una HEIC rifiutata con messaggio; modifica con foto tolta e Annulla; le tre scelte D10 in Dati e backup;
  l'ora delle voci giusta; nessuna immagine leggibile nella cartella dati. Commit e push.
