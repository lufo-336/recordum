# Spec — Cronologia versioni nota + àncora data stabile

> **Data**: 2026-06-05 · **Branch**: `fase-4-polish` · **Stato**: approvato (brainstorming)
> **Origine**: feedback utente durante la verifica del fix B1 (editor note). Due richieste collegate:
> 1. la nota deve restare ancorata al suo giorno (la modifica non deve "spostarla" alla data odierna);
> 2. una cronologia delle modifiche, accessibile via click destro → "Cronologia", con lista delle versioni e ripristino.

---

## 1. Contesto e problema

Il modello `Note` (`Recordum.Core/Models/Note.cs`) ha già tre campi temporali:

- `CreatedAt` (DateTime) — immutabile, momento di creazione.
- `TargetDate` (DateOnly?) — giorno logico di calendario a cui la nota appartiene (il pallino sul calendario / "Nota del giorno"). Null = nota non ancorata a un giorno.
- `UpdatedAtMs` (long) — ultima modifica, per il last-write-wins sync. Popolato dal `TimestampInterceptor`.

**Sintomo "la data cambia alla modifica"**: non è un bug dei dati. `CreatedAt` e `TargetDate` non vengono toccati da una modifica del corpo. Le cause percepite sono due, entrambe di presentazione:

- `NoteService.ListByHabitAsync` ordina per `UpdatedAtMs DESC` → dopo un edit la nota **salta in cima** alla lista.
- `NoteRowViewModel.UpdatedAtDisplay` mostra in evidenza l'ultima modifica → letta come "la data della nota è diventata oggi".

**Versioning assente**: non esiste alcuna cronologia delle modifiche di una nota. Il progetto ha però un pattern collaudato per il versioning (`HabitConfigHistory`, snapshot ad ogni cambio config) da rispecchiare.

---

## 2. Decisioni (dal brainstorming)

| Tema | Decisione |
|---|---|
| Storage versioni | **Tabella dedicata `note_history`** (Approccio A), mirror di `HabitConfigHistory`. No JSON-column, no meccanismo generico. |
| Àncora data | **Mostra entrambe**: àncora stabile = `TargetDate ?? data(CreatedAt)` (usata per display **e** ordinamento); "modificata il …" come info secondaria muted. |
| Nota senza giorno (`TargetDate` null) | L'àncora mostra la **data di creazione** (niente più etichetta "Generale" come primario; ogni nota mostra una data). |
| Quando si crea una versione | **Ad ogni salvataggio in cui il corpo cambia** (mirror `HabitConfigHistory`). No collasso temporale, no snapshot manuali. |
| Apertura di una versione | **Anteprima in sola lettura + Ripristina**. Il ripristino è **non-distruttivo** (crea a sua volta una nuova versione). |
| UI cronologia | **Pannello inline** nella colonna destra della tab Note (commuta con l'editor), con "‹ Indietro". |
| Anteprima | `MarkdownEditorControl` **read-only**, binding **OneWay** (rendering markdown fedele, nessuna propagazione di ritorno). |

---

## 3. Schema — `note_history` (v2.9)

Migrazione **additiva** `NoteHistoryV29` (coerente con la catena v2.4→v2.8).

| Colonna | Tipo SQLite | Note |
|---|---|---|
| `id` | TEXT (GUID) PK | |
| `note_id` | TEXT (GUID) | FK → `note.id`, **cascade delete** (dichiarata esplicita in Fluent API) |
| `body_md` | TEXT | snapshot del corpo a quella versione |
| `created_at` | TEXT ISO 8601 | momento di cattura della versione; ordinamento + display |

- Indice `ix_note_history_note_id_created_at` su `(note_id, created_at DESC)`.
- `schema_version` (AppSetting) bumpato a `2.9`.
- Le righe di `note_history` sono **immutabili** → nessun `updated_at` / `updated_at_ms`.
- Naming snake_case via `EFCore.NamingConventions`; cascade + indice **espliciti** in Fluent API (mai default EF).

**Cosa/quando si cattura**: uno snapshot del corpo viene scritto, nella stessa `SaveChangesAsync` del salvataggio della nota (atomico), ogni volta che il corpo è persistito e cambiato:

- `CreateAsync`: se la nota nasce con `BodyMd` non-vuoto (es. "Nota del giorno") → versione 1.
- `UpdateAsync`: se `BodyMd` dopo `mutate` differisce da prima → nuova versione.

Invariante: la riga `note_history` più recente per una nota coincide sempre col `BodyMd` corrente della nota. La cronologia è la timeline completa dei contenuti salvati; la più recente = "corrente".

Nessun pruning automatico delle versioni (YAGNI; retention come eventuale refinement futuro, fuori scope).

---

## 4. Livello servizio (`INoteService` / `NoteService`)

**Helper privato (riusato):**
```
AppendVersionAsync(db, noteId, bodyMd, nowIso)  // Add su note_history
```
Chiamato dentro la transazione del salvataggio:
- `CreateAsync`: dopo `db.Notes.Add(draft)`, se `draft.BodyMd` non-vuoto → append.
- `UpdateAsync`: cattura `oldBody = n.BodyMd` **prima** di `mutate(n)`; dopo il mutate, se `n.BodyMd != oldBody` → append. (Confronto sul solo corpo: cambiare solo `TargetDate` non genera versione.)

**Nuovi metodi pubblici:**
- `ListVersionsAsync(Guid noteId, ct)` → `IReadOnlyList<NoteVersion>` ordinati per `created_at` DESC.
  - `NoteVersion` = DTO `record (Guid Id, string BodyMd, DateTime CreatedAt)` in `Core/Abstractions/Services/Dtos`.
- `RestoreVersionAsync(Guid noteId, Guid versionId, ct)` → legge `body_md` della versione, poi `UpdateAsync(noteId, n => n.BodyMd = body)`. Essendo un `UpdateAsync`, il ripristino crea automaticamente una nuova versione (non-distruttivo, nessuna logica speciale). No-op silenzioso se la versione non appartiene alla nota.

**Ordinamento lista note** (`ListByHabitAsync`): da `UpdatedAtMs DESC` a **àncora stabile** `TargetDate ?? DateOnly.FromDateTime(CreatedAt)` discendente. Le note per-habit sono poche → fetch + `OrderByDescending` lato client (evita di tradurre il coalescing `DateOnly` in SQLite). L'edit non sposta più la nota in cima.

**Eventi:** riuso `NoteUpdatedPayload` esistente (già pubblicato da `UpdateAsync`). Il ripristino passa per `UpdateAsync` → la tab Note si aggiorna come per qualsiasi modifica. Nessun nuovo evento.

---

## 5. Date & display (parte 1)

`NoteRowViewModel`:
- **`AnchorDateDisplay`** (primario): `TargetDate` se valorizzato, altrimenti `DateOnly.FromDateTime(CreatedAt)`; formato `"d MMM yyyy"`, `CurrentCulture`. È l'àncora per cui si ordina (§4) e che identifica la nota. Sostituisce l'attuale `DateBadge` (che mostrava "Generale" senza data).
- **`ModifiedAtDisplay`** (secondario, muted): l'attuale `UpdatedAtDisplay` riformulato come **"modificata il …"** (label localizzata `Notes_ModifiedAt`, EN+IT). Mostrato solo se l'ultima modifica differisce dalla creazione (altrimenti ridondante per una nota mai modificata).

Layout riga (`NotesTabView`): àncora in evidenza, "modificata il …" sotto in muted, poi il sommario (prima riga del corpo). Nessun cambiamento strutturale alla lista, solo binding/label.

"Nota del giorno" (Abitudine): nessun cambiamento di display (è già legata al giorno selezionato). Le sue modifiche ora alimentano la cronologia (§4), il che è coerente.

---

## 6. UI — cronologia inline

**Accesso:** `ContextMenu` sulla riga nota (click destro) con voce **"Cronologia"** (`Notes_History`, EN+IT) → `ShowHistoryCommand(NoteRowViewModel)` su `NotesViewModel`.

**Commutazione colonna destra:** flag `IsShowingHistory` su `NotesViewModel`; due stati mutuamente esclusivi nella colonna destra della tab Note:
- **Editor** (normale): `MarkdownEditorControl` + Salva (come ora).
- **Cronologia**: pannello versioni (sotto).

```
‹ Indietro
┌────────────┬───────────────────────────┐
│ 5 giu 14:57│  (anteprima sola lettura  │
│  ▸questa…  │   della versione scelta)  │
│ 5 giu 14:30│                           │
│   prova…   │                           │
│ 4 giu 09:12│   [ Ripristina ]          │
│   prima…   │                           │
└────────────┴───────────────────────────┘
```

**Sotto-VM `NoteHistoryViewModel`** (una istanza, ricaricata su `ShowHistory`):
- `Versions` : `ObservableCollection<NoteVersionRowViewModel>` (timestamp `"d MMM HH:mm"` + sommario prima-riga), da `ListVersionsAsync`, ordine recente→vecchio. La più recente può essere etichettata "corrente".
- `SelectedVersion` → `PreviewMarkdown` (sola lettura) nel pannello di destra.
- `RestoreCommand` (abilitato se una versione è selezionata) → `RestoreVersionAsync(noteId, versionId)` → torna a `IsShowingHistory=false` con il contenuto ripristinato caricato. Ripristinare la più recente è un no-op innocuo.
- `BackCommand` (‹ Indietro) → `IsShowingHistory=false`.

**Anteprima:** `MarkdownEditorControl` con `IsReadOnly=True` e `Markdown="{Binding PreviewMarkdown}"` **OneWay** (nessuna propagazione di ritorno → nessun rischio del feedback-loop risolto in B1).

**Refresh:** dopo Ripristina, l'evento `NoteUpdated` fa il refresh della lista; l'ordinamento per àncora resta stabile (il ripristino non cambia `TargetDate`/`CreatedAt`).

---

## 7. Test

**Data:**
- `CreateAsync` corpo non-vuoto → 1 versione; corpo vuoto → 0 versioni.
- `UpdateAsync` che cambia il corpo → +1 versione; che cambia solo `TargetDate` → 0 versioni.
- `ListVersionsAsync` → ordine `created_at` DESC; la più recente == `BodyMd` corrente.
- `RestoreVersionAsync` → `BodyMd` torna alla versione scelta **e** viene aggiunta una nuova versione; il contenuto pre-ripristino resta in cronologia.
- Cascade: delete nota → delete righe `note_history`.
- Migrazione `NoteHistoryV29`: su DB v2.8 → tabella + indice presenti, `schema_version` = `2.9`.

**WPF:**
- `ShowHistoryCommand` → `IsShowingHistory=true`, `Versions` popolate.
- `RestoreCommand` → chiama `RestoreVersionAsync`, torna a `IsShowingHistory=false`, editor mostra il contenuto ripristinato.
- `AnchorDateDisplay`: `TargetDate` valorizzato → quel giorno; null → `CreatedAt`.
- Ordinamento `ListByHabitAsync` per àncora (culture-independent).

**Verifica visiva utente** (skill `run`): click destro → Cronologia → seleziona versione → anteprima → Ripristina; conferma che le note non saltano più e che àncora + "modificata il" sono corrette.

---

## 8. Fuori scope (YAGNI)

- Pruning/retention automatica delle versioni.
- Diff visivo tra versioni.
- Cronologia accessibile dalla "Nota del giorno" (Abitudine) — la consultazione vive solo nella tab Note; le modifiche da lì comunque versionano.
- Una voce separata "Dettaglio" distinta da "Cronologia" (una sola voce: "Cronologia").
