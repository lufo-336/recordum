# Modalità Calendario — una lente sulle abitudini che registrano *quale* voce

**Data**: 2026-08-19
**Stato**: spec approvata, **non pianificata**. Candidata al riordino dell'**Onda 5** dopo il dogfooding (E21).
**Brainstorm**: sessione con l'autore del 2026-08-19 (layout scelto a mockup, companion visuale).

---

## 1. Cosa si sta costruendo

Un terzo workspace, accanto a Home e Grafici, che presenta certe abitudini come **calendari**: una griglia di giorni navigabile, dove ogni giorno elenca le **voci** registrate — "18 agosto: Elden Ring 2h10, Hades II 45m" — e dove le colonne a destra mostrano le voci del giorno e il catalogo dei titoli.

Più calendari (Videogiochi, Film, Libri), raggruppabili, con una vista aggregata che li unisce.

**L'idea in una riga**: non è un tipo di abitudine nuovo, è un modo diverso di guardare abitudini che già si possono comporre — più un mattone di modello che oggi manca, il catalogo di voci.

## 2. Le sei decisioni dell'autore

Fissate nel brainstorm, sono i vincoli che il piano non può rinegoziare da solo:

1. **Un calendario è un'abitudine**, vista con un'altra lente. Non un oggetto parallelo. Eredita registrazione, note, export, backup, cifratura, sync, streak.
2. **Una voce del catalogo ha identità propria** (non è testo libero ripetuto): così si rinomina una volta sola e si può censire un arretrato.
3. **I campi di una voce di giornata sono quelli dell'abitudine**, composti nel builder, con uno eletto *soggetto*. Non una struttura fissa titolo+ore+note.
4. **L'aggregato mostra le voci vere**, colorate per calendario (colore = identificatore, come in Home).
5. **Layout B**: albero dei calendari a sinistra · griglia dei giorni al centro · colonne a destra.
6. **La voce ha tre stati fissi**: `Todo` / `InProgress` / `Done`. Fissi e non configurabili — è la barriera contro il diventare un mini-database.

E una decisione di percorso: **l'elenco dei calendari parte derivato** (passo 1), con l'obiettivo dichiarato di arrivare alla marcatura esplicita (passo 2) e poi alla lente universale (passo 3). Vedi §8.

## 3. Modello dei dati

### 3.1 Niente di nuovo per il calendario in sé

| concetto d'interfaccia | cosa è già |
|---|---|
| un calendario | `HabitNode`, habit counter con `repeatable = true` |
| una voce di giornata | `Completion` (retroattiva se il giorno è passato) |
| ore, voto, altri campi | `CompletionValue` sulle `MetricDefinition` dell'abitudine |
| la nota di una voce | `Completion.Notes` |
| il colore del calendario | `HabitNode.Color` |
| gruppo di calendari, vista aggregata | nodo gruppo esistente + albero `parent_id` |

### 3.2 Il mattone nuovo: `catalog_item`

Tabella additiva.

| colonna | tipo | note |
|---|---|---|
| `id` | TEXT (GUID) PK | |
| `habit_node_id` | TEXT FK → `habit_node`, **CASCADE** | il calendario proprietario |
| `name` | TEXT NOT NULL | |
| `status` | TEXT NOT NULL DEFAULT `'Todo'` | CHECK `IN ('Todo','InProgress','Done')` |
| `created_at` / `updated_at` | TEXT ISO 8601 | convenzione standard |
| `updated_at_ms` | INTEGER Unix ms | LWW per la sync |

- **Indice UNIQUE** su `(habit_node_id, name)` **case-insensitive** (`COLLATE NOCASE`): due voci che differiscono per maiuscole sono la stessa voce.
- Indice su `(habit_node_id, status)` per il filtro della colonna Lista.
- È figlia di un `HabitNode` come `MetricDefinition` ⇒ **niente `created_by_plugin_id`** (eredita dal parent, CLAUDE.md §8).
- **Catalogo per-calendario.** Nessuna condivisione fra calendari in questa tappa.

### 3.3 Il legame con le registrazioni

Il campo-soggetto è una `MetricDefinition` di **tipo nuovo `Item`**; `CompletionValue` guadagna:

```
value_catalog_item_id TEXT NULL   -- FK → catalog_item(id), ON DELETE RESTRICT
```

Il catalogo di una metrica `Item` **non ha bisogno di una FK propria**: è l'insieme delle `catalog_item` dello stesso `habit_node_id` della metrica. Un legame in meno da tenere coerente.

`ON DELETE RESTRICT` è deliberato: una voce usata in N registrazioni non sparisce in silenzio portandosi via il significato dei dati. L'interfaccia dice quante registrazioni la usano e propone di rinominarla.

### 3.4 Il costo dichiarato: la ricostruzione di `metric_definition`

Aggiungere `Item` a `MetricType` significa toccare il CHECK su `metric_definition.type`, e SQLite un CHECK non lo altera: EF genera una **ricostruzione della tabella** (crea, copia, droppa, rinomina). È l'unico punto non puramente additivo, contro l'invariante "schema finale dalla Fase 3, nessuna migrazione invasiva".

**Alternativa considerata e respinta**: riusare `MetricType.Text` e far dipendere il comportamento a picker dall'esistenza di un catalogo sul nodo. Costa zero in migrazione ma introduce una regola implicita che non vive nel modello — contro "esplicito > implicito" (CLAUDE.md §11). Si paga una volta la ricostruzione, non ogni volta che qualcuno legge il codice.

**Vincolo operativo**: la migrazione girerà su un DB con i dati veri dell'autore (dogfooding già avviato). Vale la disciplina E21 — provata su una **copia** del DB reale prima dell'originale, `.recbak` manuale prima di lanciare la build.

## 4. Schermate e interazioni (layout B)

Terza icona nella ribbon (`WorkspaceRibbon.xaml` ha già il pattern: bordo accento a sinistra + sfondo elevato quando attivo). Scorciatoia `Ctrl+6` (`Ctrl+4/5` sono Grafici e Backup, non si rinumerano).

### 4.1 Zona 1 — l'albero dei calendari

L'albero delle abitudini **filtrato ai calendari**, coi gruppi che li contengono. **"Tutti"** in cima è la radice.

- Selezionare un calendario → mostra quello.
- Selezionare un gruppo ("Intrattenimento") → mostra **l'unione dei suoi discendenti**. La vista aggregata non è un oggetto speciale: è un gruppo selezionato. Ne consegue che più aggregati diversi sono gratis.
- Larghezza persistita e collassabile, come la sidebar Home.

### 4.2 Zona 2 — la griglia dei giorni

Mese, navigazione ‹ ›, oggi bordato d'accento, giorno selezionato evidenziato, giorni futuri non selezionabili.

Ogni cella elenca le voci del giorno con la barretta del colore del calendario; oltre 3 voci compare `+N` e le si vede tutte nella colonna. Un clic seleziona il giorno; il `+` della cella apre il composer già puntato su quella data.

**Riuso parziale, verificato nel codice**: `CalendarNavigatorViewModel` fornisce navigazione mese/anno, griglia 6×7, evidenziazione di oggi e blocco dei futuri — riusabili. La **cella** no: oggi un giorno è un `CalendarDayMarker(Date, TintStep, Number, NoteCount, TintColorHex)`, cioè una tinta e due numeri; non sa rappresentare un elenco di voci. Serve un modello di cella nuovo. Chi pianifica non stimi questa parte come "riuso".

**Confine giornaliero**: la cella di appartenenza di una registrazione deve seguire la cascata `reset_time`, non la mezzanotte civile. Oggi **non è così in nessuna parte dell'app** → dipendenza esterna, vedi §7 e finding **R9**.

### 4.3 Zona 3 — le colonne

Le **stesse** colonne toggle della Home: stesso sash, stessa memoria per-abitudine, stesso sistema. Non un secondo insieme (vincolo esplicito dell'autore — è ciò che tiene il calendario una lente invece di un'app dentro l'app).

- **Inseriti** — le voci del giorno selezionato: soggetto, campi, nota. Modifica in riga, elimina con conferma (`IConfirmationDialog`), `+ aggiungi`.
- **Lista** — il catalogo: nome, stato, quante volte, ultima volta. Filtro per stato; crea / rinomina / cambia stato; "registra oggi" da una voce.
- Restano disponibili **Cronologia, Dettaglio, Note, Configura**: un calendario è un'abitudine, e le sue colonne non spariscono perché lo si guarda da un'altra parte.

**Su un gruppo** la colonna Lista mostra l'unione dei cataloghi dei figli, raggruppata per calendario e in sola lettura: si crea e si rinomina dentro il calendario che possiede la voce.

### 4.4 Registrare

Il composer è il **quick-add esistente**, col campo `Item` reso come picker con ricerca. Scrivere un nome non in catalogo offre **"crea «Silksong»"** sul posto: il catalogo si riempie usandolo. Lo stato `Todo` serve solo a chi *vuole* segnarsi qualcosa in anticipo.

### 4.5 In Home

Il calendario resta un'abitudine normale, con la sua riga e la sua striscia per-tipo. Nessuna sparizione, nessuna doppia identità.

## 5. Confini (non-goals dichiarati)

- **Niente attributi sulle voci** oltre nome e stato. Un voto è un campo dell'*abitudine* (il builder lo compone già) e vale per tutte le registrazioni.
- **Niente catalogo condiviso** fra calendari, niente "importa da".
- **Niente unione di due voci duplicate.** Rinominare copre il refuso; due voci entrambe usate si sistemano a mano. Lacuna dichiarata.
- **Niente trascinamento** di una voce da un giorno all'altro: la data si corregge dalla riga, come le altre registrazioni retroattive.
- **Niente streak o notifiche speciali** per i calendari.
- **Niente passo 2 e passo 3** (§8) — ma la cella si scrive già senza presupporli.

## 6. Test che la spec pretende

- Unicità **case-insensitive** del nome dentro un catalogo.
- `ON DELETE RESTRICT`: cancellare una voce usata fallisce, e l'interfaccia dice quante registrazioni la usano.
- **Rinomina che si propaga** a tutte le registrazioni (è il motivo per cui la voce ha identità).
- Aggregato di gruppo = unione dei discendenti; un calendario spostato in un altro gruppo cambia aggregato.
- **Una cella-giorno che rende un'abitudine senza campo-soggetto** — è il test che tiene aperta la strada al passo 3.
- **Parità builder** (principio 8): un campo `Item` deve essere componibile nel builder. Se un template ufficiale userà un calendario, `BuilderTemplateParityTests` lo pretenderà da sé; il principio lo pretende comunque.
- Migrazione: ricostruzione di `metric_definition` che **preserva** righe, FK e indici, verificata su un DB popolato.

## 7. Dipendenze esterne alla spec

**R9 — il bucketing per `reset_time`** (tracker). `IResetTimeResolver` non ha un solo chiamante in `src/`: calendario e heatmap bucketizzano con `DateOnly.FromDateTime(c.CompletedAt)` (`CompletionService.cs:204`), gli evaluator di streak non lo nominano. `docs/MILESTONES.md:152` lo dichiara rimandato a una milestone mai arrivata.

Questa spec **assume R9 risolto**. Se non lo fosse, un calendario con reset alle 04:00 metterebbe la partita delle 02:30 nella cella sbagliata — cioè proprio nel caso d'uso che motiva la feature. R9 non si chiude qui: tocca streak, heatmap, calendario ed export, e ha il suo posto nell'Onda 2.9.

## 8. La progressione 1 → 2 → 3

| passo | cosa compare nell'elenco a sinistra | come si ottiene |
|---|---|---|
| **1** (questa spec) | le abitudini con un campo-soggetto `Item` | regola derivata, nessuna manopola |
| **2** (futuro) | + quelle marcate a mano | override additivo in `ui_prefs_json`, come il gesto journal di U7 |
| **3** (futuro) | tutte le abitudini, la lente diventa universale | l'elenco smette di essere un cancello e diventa un filtro |

**La condizione che rende gratis i passi 2 e 3**: la cella-giorno non deve mai *presupporre* l'esistenza di un campo-soggetto. Un giorno senza voci nominate mostra il nome dell'abitudine. Scritta così, il passo 3 è una riga di filtro; scritta assumendo il catalogo, è un rifacimento.

## 9. Collocazione nella roadmap

Fuori dal freeze v1.0: **niente esecuzione prima del riordino dell'Onda 5** post-dogfooding (mappa `2026-07-03-v1-redefinition-plan-design.md` §1-bis, punto 1). Il piano di implementazione si scrive a ridosso dell'esecuzione, per metodo (§9, punto 1, della mappa).

Il dogfooding è anche il giudice naturale di questa spec: se dopo settimane d'uso basta un counter ripetibile con un campo Testo, la lente non serve. Se invece ci si ritrova a cercare "cosa ho guardato a marzo", serve.
