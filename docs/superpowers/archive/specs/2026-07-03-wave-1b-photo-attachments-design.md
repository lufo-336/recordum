# Onda 1b — Foto allegate alle note (design)

**Data**: 2026-07-03 · **Stato**: approvato dall'autore (brainstorm 2026-07-03)
**Contesto**: piano v1.0 (`2026-07-03-v1-redefinition-plan-design.md`, Onda 1b). Capability generale che abilita **U7 (food diary)** — la cui UX dedicata resta in Onda 2 — e chiude l'exit criterion **E5**: foto allegabile a una nota veloce, visibile in app, inclusa in backup/restore, presente negli export.

**Fatto verificato in esplorazione (2026-07-03) — NESSUNA MIGRAZIONE SERVE**: la tabella `attachment` esiste dallo schema iniziale — `Attachment { Id, CompletionId?, NoteId?, FilePath (max 500), MimeType (max 100), Caption? (max 500), CreatedAt }`, FK `Restrict` verso Note e Completion (`src/Recordum.Core/Models/Attachment.cs`, `AttachmentConfiguration`). Il `PurgeService` cancella già le **righe** attachment (ma non i file); `BackupService` copia già `IBackupStoragePaths.AttachmentsDirectory` nel `.recbak` e `RestoreService` la ripristina. In 1b si usa **solo il ramo NoteId**.

## Decisioni dell'autore (2026-07-03)

1. **File in chiaro** nella cartella dati (no cifratura per-file, no BLOB nel DB). Trade-off accettato e da documentare: il DB è SQLCipher ma le foto su disco no; il `.recbak` le cifra comunque; la cifratura per-file è un'eventuale opzione 1.x.
2. **Acquisizione**: selettore file **+** incolla dagli appunti (Ctrl+V) **+** drag & drop — tutti e tre in v1.0.
3. **Export**: foto incorporate in **PDF** e **HTML**; **Markdown** con link relativi + copia dei file accanto al `.md`; JSON/CSV solo caption+filename.

## 1. Storage e processing

- Percorso: `%LocalAppData%\Recordum\attachments\{guid}.jpg` (da `IBackupStoragePaths.AttachmentsDirectory`); `Attachment.FilePath` memorizza il **path relativo** `attachments/{guid}.jpg` (portabile nel restore su un'altra macchina). Nome file = GUID: nessuna informazione trapelata dal filename.
- **Ri-codifica all'import, sempre**: lato lungo max **2048px**, JPEG qualità **~85**, orientamento EXIF applicato, **metadati EXIF rimossi** (incluse coordinate GPS — le foto dei pasti le contengono). Normalizza qualunque input decodificabile da WPF (JPEG/PNG/BMP/GIF frame singolo); `MimeType` risultante sempre `image/jpeg`.
- Processing in **`IImageImporter`** (servizio Recordum.WPF, `BitmapDecoder` + `JpegBitmapEncoder`, zero dipendenze nuove): input stream/bytes → output `byte[]` JPEG pronto. Il layer Data non tocca API di imaging (in Fase 5 MAUI avrà il proprio importer con lo stesso contratto concettuale).

## 2. Service layer

Nuovo **`IAttachmentService`** (interfaccia in `Recordum.Core/Abstractions/Services`, impl in `Recordum.Data/Services`, pattern sibling: `IDbContextFactory` + `IClock`):

- `Task<Guid> CreateForNoteAsync(Guid noteId, byte[] jpegBytes, string? caption, CancellationToken ct)` — scrive il file (crea la cartella se manca) poi la riga; se la scrittura della riga fallisce, cancella il file (niente orfani da create).
- `Task<IReadOnlyList<Attachment>> ListByNoteAsync(Guid noteId, CancellationToken ct)`.
- `Task DeleteAsync(Guid attachmentId, CancellationToken ct)` — ordine **file → riga**; file mancante = non fatale (log e prosegui).
- `Task UpdateCaptionAsync(Guid attachmentId, string? caption, CancellationToken ct)` — per la caption editabile del viewer.
- Risoluzione path assoluto: `Path.Combine(paths root, FilePath)` — esporre un helper (`ResolveAbsolutePath(Attachment)`) usato da UI ed export.

**Lifecycle coerente**:
- **Eliminazione nota**: FK `Restrict` → il percorso di delete della nota (in `NoteService`) elimina prima gli attachment (file+righe) della nota. Verificare il sito esatto in fase di piano.
- **PurgeService**: oggi `ExecuteDelete` delle righe attachment → i **file** resterebbero orfani. Fix: raccogliere i `FilePath` del sottoalbero **prima** dei delete e cancellare i file (ordine spec schema: file → tabelle figlie → HabitNode). Vale per entrambi i rami (per-nodo e job >30gg).
- **Backup/restore**: nessun lavoro — già coperti. Test di round-trip richiesto.

## 3. UI (tab Note)

- **Composer nota veloce** (`NotesTabView`): bottone allega (icona Lucide `Paperclip` o `Image`, apre `OpenFileDialog` con filtro immagini, multiselezione ammessa), **Ctrl+V** quando gli appunti contengono un'immagine (`Clipboard.ContainsImage`), **drag & drop** di file immagine sulla superficie della nota. Le foto si possono allegare anche a una nota esistente (stesso menu/bottone sulla riga).
- **Riga nota**: striscia orizzontale di **thumbnail** sotto il corpo (`Image` con `DecodePixelWidth` ≈ 120 e caching async — nessun file thumbnail separato).
- **Viewer**: click sul thumbnail → finestra chromed dell'app (stile `Recordum.ChromedWindow`) con immagine adattata, caption editabile, bottone elimina con conferma (`IConfirmationDialog`).
- Stringhe nuove EN-first + IT (convenzione 3 file, hook Designer.cs → shell).

## 4. Export

- `ExportNoteRow` (Plugins.Contracts) guadagna `Attachments: IReadOnlyList<ExportAttachmentRef>` (init-property default `[]`); `ExportAttachmentRef(string FileName, string AbsolutePath, string? Caption)`. `ExportQueryService` la popola risolvendo i path assoluti.
- **PDF** (QuestPDF): immagine sotto la nota, larghezza max pagina, da `File.ReadAllBytes(AbsolutePath)` (file mancante → salta con nota "missing image").
- **HTML**: `<img src="data:image/jpeg;base64,...">` (autocontenuto, come oggi).
- **Markdown**: `![{caption}](attachments/{FileName})`; il **flusso di salvataggio export** (lato WPF, dove il file viene scritto) copia i file referenziati in `attachments/` accanto al `.md`. Senza foto: singolo file identico a oggi. (Il contratto `IExportFormatter.Write → byte[]` non cambia: la copia dei file è responsabilità dell'orchestratore del salvataggio.)
- **JSON**: array `attachments` per nota con `file_name` e `caption`. **CSV**: invariato (le note non sono nel CSV).

## 5. Test

- **Data**: `CreateForNoteAsync` scrive file+riga (path relativo corretto); `DeleteAsync` rimuove file+riga; create con DB failure non lascia file; purge di un habit con note fotografate → righe E file spariti; delete nota → attachment prima (FK Restrict rispettata); backup→restore round-trip con un allegato (file identico al ripristino).
- **WPF**: `IImageImporter` (ridimensiona >2048px, non ingrandisce le piccole, EXIF orientation applicata, metadati assenti nell'output); VM composer (allega da bytes, rimuovi, lista thumbnails); paste/drag gating (`ContainsImage`).
- **ExportEngine**: MD emette link relativi; HTML contiene `data:image/jpeg;base64`; PDF con immagine non lancia e produce bytes; JSON contiene `attachments`.
- Verifica visiva finale con l'autore: allega da picker/incolla/drag, thumbnail, viewer, elimina, backup+restore, export PDF/HTML/MD riletti.

## 6. Fuori scope (esplicito)

Allegati alle **Completion** (colonna già in schema, nessuna UI ora); cifratura per-file (eventuale 1.x); fotocamera (Fase 5 mobile); gallerie/riordino/più di una striscia; editing immagine (crop/ruota); HEIC e formati non decodificabili da WPF (messaggio d'errore localizzato).
