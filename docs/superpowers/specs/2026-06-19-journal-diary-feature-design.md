# Recordum — Feature Journal/Diario (note-diario che guidano il completamento)

> **Data**: 2026-06-19 · **Stato**: approvato (brainstorm con l'autore) · **Modello**: B (categoria persistita, diario unificato in `Note`)
> **Scope**: parte della **v1.0 desktop** — "a 1.0 tutti i template e le loro feature devono funzionare" (decisione autore). Aggiorna l'asticella v1.0 dello spec `2026-06-19-roadmap-revision-design.md`.

## 1. Obiettivo

Per un'abitudine **journal**, scrivere una **nota-diario** è ciò che la completa. Le note (diario e veloci) sono categorizzate e ancorate a **giorno + orario**. La categoria è il fondamento per il futuro editor di testo ricco / personalizzazione template.

## 2. Modello confermato

- **Journal** = abitudine a sé stante (resta un `HabitType`, composabile come gli altri — Q9 opzione a). Ciò che la completa è una **voce di testo**, non un check/numero.
- **Una nota-diario ↔ un completamento (1:1)** *(scelta A confermata)*. Salvare una voce crea `Note` (contenuto+orario) **e** un `Completion` appaiato, insieme. Cancellare la voce cancella entrambi.
- **Giorno completo finché esiste ≥1 nota-diario** quel giorno: cancellando una nota in un giorno con due note, resta un completamento → il giorno **resta completo**.
- **Nota-veloce** = `Note(Category=Veloce)`, **nessun completamento**, non conta per lo streak (Q5).
- **Categoria Diario solo sui journal** per ora *(scelta B confermata)*; diario su abitudini arbitrarie via template = Future.

### Perché 1:1 nota↔completamento
Fa cadere il resto senza codice nuovo:
- "Quante note-diario quel giorno" = numero di completamenti del giorno = il numero che `ICompletionService.GetDailyMagnitudeAsync` **già** restituisce per Journal (`g.Count()`). Nessuna query nuova, calendario invariato.
- "Done finché ≥1 nota" = ≥1 completamento → `SchedulingAwareStreakAggregator` (conta l'esistenza del completamento via `GetHeatmapAsync`) **funziona invariato**.
- Regola di cancellazione dell'autore rispettata esattamente.

## 3. Modello dominio

`Note` (`src/Recordum.Core/Models/Note.cs`) — due campi nuovi:
- `Category` (enum `NoteCategory { Diario, Veloce }`, default `Veloce`).
- `CompletionId` (`Guid?`) — il completamento appaiato; valorizzato solo per le Diario, null per le Veloci.

`Note.CreatedAt` (già esistente, `DateTime`) porta l'**orario**: nessuna colonna tempo nuova. Per voci retroattive: `TargetDate` = giorno scelto; il completamento appaiato atterra su `TargetDate` (al time-of-day corrente) così calendario/streak finiscono nel giorno giusto; `Note.CreatedAt` = istante reale di scrittura (componente orario usata in lista).

`Completion.Notes` (legacy string) **non più usato** dal journal (il testo vive in `Note.BodyMd`); la colonna resta ma per i journal è null.

## 4. Schema — migrazione additiva v2.11 (`NoteCategoryV211`)

- `note.category TEXT NOT NULL DEFAULT 'Veloce'` + CHECK `IN ('Diario','Veloce')`.
- `note.completion_id TEXT NULL` (FK soft verso `completion.id`, **nessuna cascade EF** — l'accoppiamento è gestito in transazione dal servizio) + indice su `completion_id`.
- Bump `schema_version` AppSetting → `2.11`; `Down` reverte a `2.10` e droppa le due colonne.
- **Nessuna migrazione dei dati** dei vecchi `Completion.Notes`: per decisione autore (Q6) i dati esistenti si **cancellano** (in dev via `run --clean`) e si **ripopolano** dopo con il seeder. La migrazione è puramente additiva sullo schema.

## 5. `IDiaryService` — orchestratore dell'accoppiamento (Data layer)

Isola l'unica invariante delicata (note↔completion) in un punto testabile, un solo percorso di scrittura:
- `SaveEntryAsync(habitNodeId, day, text, ct)` → in **una transazione**: crea `Note(Category=Diario, TargetDate=day, BodyMd=text)` + `Completion(CompletedAt su day)` appaiato, scrive `note.completion_id`. Ritorna l'id nota.
- `DeleteEntryAsync(noteIdOrCompletionId, ct)` → cancella **nota + completamento appaiato** insieme, transazionale. Tutte le cancellazioni di voci-diario (colonna Note **e** riga Cronologia journal) passano da qui → **nessun orphan**, e non esiste un percorso che cancella il solo completamento ("non viceversa" rispettato).
- `EditEntryAsync(noteId, text, ct)` → aggiorna solo `BodyMd` (+ version history `NoteHistory` esistente); completamento intatto.
- Pubblica gli eventi esistenti (`CompletionRecorded`/`NoteCreated`/…`Deleted`) così Home/calendario/streak si rinfrescano come oggi.

## 6. Punti d'ingresso che cambiano (WPF)

- `JournalQuickAddViewModel` → usa `IDiaryService.SaveEntryAsync` invece di `Completions.RecordAsync` con `Completion.Notes`.
- Editor "nota del giorno" del journal (`DayNoteViewModel` nel contesto journal) → scrive una voce-diario via `IDiaryService`.
- **Cronologia** (`CompletionListViewModel`): per i journal mostra il testo della **nota appaiata** (join via `completion_id`), non `Completion.Notes`; il delete riga journal → `DiaryService.DeleteEntryAsync`.
- **+ da Home** su journal → apre l'editor voce-diario.
- **Nota veloce** (modulo generico su ogni abitudine, `NotesViewModel`/`DayNoteViewModel` fuori dal contesto journal) → crea `Note(Category=Veloce)`, nessun completamento.

## 7. Colonna Note (Feature A)

- Ogni nota mostra **chip categoria** (Diario / Veloce) + **giorno e orario** (`TargetDate` + `CreatedAt`).
- Toggle **Giorno / Periodo** come in Cronologia, **riusando** `EntriesPeriodRange` (Core) e il pattern di `HabitRecordsViewModel`.
- Diario appare **sia** in Cronologia **sia** in colonna Note (Q8); revisione UI non esclusa in futuro.

## 8. Impatti "gratis" (nessuna modifica funzionale)

Streak journal, numero sul calendario, distribuzione oraria, heatmap: alimentati dal completamento-per-nota, funzionano senza cambi (verificare con test di regressione).

## 9. Dati di collaudo

Aggiornare `DemoDataSeeder` e `--seed-habit` a creare voci-diario via `IDiaryService` per i journal (più qualche nota Veloce). Ripopolamento **dopo** l'implementazione (Q6), per validare end-to-end.

## 10. Testing

- **Core**: enum `NoteCategory`; nessuna logica pura nuova significativa (il resolver periodo è già testato).
- **Data**: `IDiaryService` — Save crea coppia appaiata + `completion_id`; Delete rimuove entrambi; Delete di una di due note tiene il giorno completo (resta un completamento); Edit non tocca il completamento; transazionalità (fallimento → rollback di entrambi); migrazione v2.11 (Up/Down, CHECK, bump versione).
- **WPF**: JournalQuickAdd usa DiaryService; Cronologia journal mostra testo nota appaiata; nota-veloce non crea completamento; colonna Note mostra categoria+orario e toggle periodo; regressione streak/calendario journal invariati.

## 11. Fuori scope (Future)

Categoria Diario su abitudini non-journal via template; editor di testo ricco; revisione UI della colonna Note; categorie custom dai template.

## 12. Rischi / note

- `Completion.Notes` resta nello schema (non rimosso) per non allargare il blast-radius; solo il journal smette di usarlo.
- L'accoppiamento vive **solo** in `IDiaryService`: ogni nuovo percorso di scrittura/cancellazione voci-diario DEVE passare da lì (non scrivere `Completion`/`Note` journal a mano altrove).
- Migrazione additiva in Fase 4 coerente con le precedenti (v2.8/2.9/2.10); lo schema è "finale" alla v1.0.
