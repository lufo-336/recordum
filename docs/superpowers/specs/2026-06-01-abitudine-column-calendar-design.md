# Recordum — Colonna "Abitudine" + calendario + note unificate + timer Session ibrido

- **Data**: 2026-06-01
- **Stato**: design approvato in brainstorming, in attesa di review dello spec
- **Fase**: 4 (Polish/UX)
- **Branch**: `fase-4-polish`
- **Issue di riferimento**: report utente "Recordum problemi 2" (calendario, colonna Abitudine, note per-giorno, timer a tappe)

---

## 1. Problema

Dal test della build M3.10b (report "Recordum problemi 2"):

- Manca **una colonna dove "TUTTO è visualizzabile"** per una habit: oggi le viste (Cronologia/Dettaglio/Note) sono tab separate e nessuna è giorno-centrica.
- **Selezionare un giorno** per registrare/correggere è scomodo: il calendario attuale (heatmap GitHub-style) è un riquadro piccolo non navigabile (niente scroll mesi/anni, niente giorno della settimana, nessun modo evidente di scegliere un giorno e agire su di esso).
- Il menù-tendina di selezione data è **chiaro su chiaro** (illeggibile) e disgiunto dal calendario.
- Le **note** sono per-habit e non per-giorno: non si può "vedere sul calendario dove ho messo una nota" né annotare uno specifico giorno.
- Il **timer a tappe** (Session, segmenti Work/Rest) esiste nel modello ma è poco scopribile e non supporta durate per-segmento né intervalli ripetuti.

Questo spec copre **quattro pilastri**. Altre sezioni di Configura (metriche, scale, goal, scheduling, notifiche) sono rinviate a un brainstorm dedicato.

## 2. Decisione (overview)

1. **Modello a colonne** — il workspace di una habit diventa `★ Abitudine · Dettaglio · Note · ⚙ Configura`. "Cronologia" sparisce come colonna: la **marcatura** entra in Abitudine, la parte **analitica** resta in Dettaglio. (Rifinisce lo spec nav-model parcheggiato.)
2. **Colonna Abitudine** — calendario navigabile riusabile (`CalendarNavigator`) in cima + **pannello del giorno** sotto, con input di marcatura per tipo di habit + nota del giorno.
3. **Note unificate** — `Note` guadagna un'ancora-data opzionale: stesse note esposte come "note del giorno" in Abitudine e come timeline completa nella colonna Note.
4. **Timer Session ibrido** — durata **opzionale** per segmento (vuota = avanzamento manuale/lap; impostata = countdown auto-avanzante) + ripeti ciclo ×N.

Tutte le modifiche schema sono **additive** → bump a **v2.8** in una singola migration.

## 3. Relazione con gli spec esistenti

- **`2026-05-29-navigation-model-design.md`** (nav-model "A evoluto", parcheggiato): resta valido per le **meccaniche** (chip toggle, ✕/Esc/clic-a-vuoto, persistenza `ui.open_columns`, rebind al cambio selezione, gruppi scopribili). **Questo spec lo aggiorna** sostituendo il set colonne di §3.4: `Abitudine` rimpiazza `Cronologia`; le altre tre (`Dettaglio`, `Note`, `Configura`) restano. Default all'apertura di una habit = **Abitudine**.
- **`2026-05-29-editor-sections-ui-design.md`** (B6c, sezioni editor): le View di sezione si riusano identiche dentro la colonna Configura. Questo spec **aggiunge** alla sezione Timer i campi del timer ibrido e introduce il picker colore-nota (vedi §8).

## 4. Pilastro 1 — Modello a colonne

- Workspace soggetto = chip-bar toggle → colonne affiancate (come da nav-model spec). Set per una **habit**: `★ Abitudine · Dettaglio · Note · ⚙ Configura`.
- Chip toggle: clic apre la colonna, riclic la chiude; ✕ sull'header, **Esc** chiude l'ultima, **clic a vuoto** nell'albero deseleziona → chiude tutte.
- Apertura di una habit da zero colonne → si apre **Abitudine** di default. Il set aperto è persistito (`ui.open_columns`, nav-model spec) e si **riassocia** al cambio selezione.
- La colonna **Note** resta come scorciatoia diretta al journal completo (decisione utente: "se l'utente vuole saltare lì, può").

## 5. Pilastro 2 — `CalendarNavigator` (componente riusabile)

Widget WPF a sé (`Views/Common/CalendarNavigator.xaml` + `CalendarNavigatorViewModel`), progettato **riusabile** perché la futura colonna **Gruppo** lo riuserà (vedi §10).

**Struttura visiva (dall'alto):**
1. **Barra di salto**: `‹` mese precedente · **tendina mese/anno** (tematizzata, leggibile — risolve il "chiaro su chiaro") · `›` mese successivo. La tendina permette di saltare a mese/anno arbitrari; le frecce scorrono di un mese.
2. **Intestazioni giorni settimana** (L M M G V S D, dipendenti da cultura/`FirstDayOfWeek`).
3. **Griglia mese**: celle con il numero del giorno; giorni fuori-mese attenuati; **oggi** cerchiato (outline accent); **giorno selezionato** pieno (colore habit); **marcatori** (pallini) sotto al numero.
4. **Legenda**: completato · nota · oggi · selezionato.

**Marcatori (pallini):**
- 🟢 **completato** → colore = **`HabitNode.Color`** (zero config extra).
- 🟡 **nota** → colore = **`HabitConfig.NoteMarkerColor`** (configurabile in Configura; default ambra se null).

**Contratto del componente (per il riuso):**
- Input: `DisplayMonth` (anno+mese visualizzato), `SelectedDate`, `IReadOnlyDictionary<DateOnly, DayMarker>` dove `DayMarker { bool Completed; bool HasNote; }`, `CompletedColor`, `NoteColor`.
- Output/eventi: `SelectedDateChanged(DateOnly)`, `DisplayMonthChanged(int year, int month)` (il consumer carica i marcatori del mese).
- Il componente **non** conosce habit/gruppi: riceve solo marcatori e colori. Abitudine e (in futuro) Gruppo gli forniscono i dati.
- Giorni **futuri**: visualizzabili ma non selezionabili-per-marcatura (il pannello-giorno disabilita la marcatura, vedi §6).

## 6. Pilastro 2b — Pannello del giorno (in Abitudine)

Sotto il calendario, il pannello mostra il **giorno selezionato** ("Lunedì 1 Giugno 2026") e gli input di marcatura **specifici per tipo di habit**:

| HabitType | Input nel pannello-giorno |
|---|---|
| Action | check ✓ "fatto / non fatto" |
| Counter | stepper quantità (per metrica; metriche multiple → più righe) + timer se `HasIntegratedTimer` |
| Resistance | "ho resistito / ho ceduto" + timer |
| State | selettore scala (labels da `MetricDefinition`/`ScaleTemplate`) |
| Journal | solo nota del giorno (nessun valore) |

- Sotto agli input: **nota del giorno** (la stessa entità `Note` ancorata a quel giorno — vedi §7). Editor breve inline; per il markdown ricco c'è la colonna Note.
- Bottone **"✓ Segna <giorno>"**.
- **Semantica data**: giorno = oggi → `Completion` normale; giorno **passato** → `IsRetroactive=true` (infra già esistente, M3.5); giorno **futuro** → marcatura disabilitata.
- La marcatura riusa l'infrastruttura esistente: `ICompletionService.RecordAsync`, `IActiveTimerService` per il timer. Multi-serie e segmenti seguono la semantica M3.5 invariata.

## 7. Pilastro 3 — Note unificate

**Decisione**: un unico sistema di note. `Note` guadagna un'**ancora-data opzionale**.

- **Modello**: `Note.TargetDate` (`DateOnly?`, nullable). `null` = nota libera (journal non ancorato, comportamento attuale); valorizzata = nota di quel giorno.
- **Abitudine** mostra/crea le note con `TargetDate == giornoSelezionato`. **Colonna Note** mostra la timeline completa (tutte le note dell'habit, ancorate e non).
- Scrivere una nota in Abitudine la rende visibile nella colonna Note e viceversa: **stesse note, due accessi** (decisione utente).
- Il marcatore 🟡 sul calendario compare per i giorni con almeno una `Note` con `TargetDate` = quel giorno.
- `Note` mantiene la sua peculiarità schema (solo `UpdatedAtMs`, niente `UpdatedAt` — intenzionale, invariato). `TargetDate` è TEXT ISO date (`YYYY-MM-DD`).

> Nota: il campo testo `Completion.Notes` resta com'è (nota volatile sul singolo completamento, usata dal flusso quick-add M3.5). La nota "ricca/persistente del giorno" è l'entità `Note` ancorata. Non si fondono i due: `Completion.Notes` è un di più del completamento, `Note.TargetDate` è la nota del giorno mostrata sul calendario.

## 8. Pilastro 4 — Timer Session ibrido

**Modello attuale**: `SegmentDefinition { Name, Color, SortOrder, IsRest }` — segmenti solo nominali; `TimerMode=Session` avanza manualmente (`NextSegmentAsync`) e registra il tempo reale (`CompletionSegment`).

**Aggiunte (ibrido manuale + preimpostato):**
- **Schema**: `SegmentDefinition.DurationSeconds` (`int?`, nullable). `HabitConfig.SessionRepeatCount` (`int?`, nullable, default 1).
- **Semantica**:
  - Segmento con `DurationSeconds == null` → tappa **manuale** (l'utente avanza, registra il tempo reale) — comportamento attuale.
  - Segmento con `DurationSeconds` impostato → **countdown**: a fine durata avanza da solo al segmento successivo + segnale acustico.
  - `SessionRepeatCount` → il ciclo di segmenti si ripete N volte (caso HIIT). Default 1 = nessuna ripetizione.
- **Motore timer**: estendere la macchina a stati Session. Oggi `NextSegmentAsync` è solo manuale e cappa al numero di segmenti; va aggiunto:
  - tick/countdown per i segmenti con durata, con auto-avanzamento;
  - loop del ciclo per `SessionRepeatCount` (segment_index attraversa `n_segmenti × repeat`);
  - segnale acustico al cambio segmento (configurabile/silenziabile — dettaglio in fase di piano).
  - Recovery post-crash invariato nella forma (`active_timer` con `segment_index`), tenendo conto del ciclo ripetuto.
- **UI (sezione Timer di Configura)**: per ogni riga segmento aggiungere **campo durata** (placeholder "manuale" se vuoto) + esporre il **colore segmento** (già nel modello, non in UI) + campo **"Ripeti ciclo ×N"**.

## 9. Configura — aggiunte di questo spec

- **Picker colore marcatore nota**: nuovo controllo nella sezione appropriata (header L1 vicino al colore habit, oppure sezione dedicata — deciso in piano), bound a `HabitConfig.NoteMarkerColor`.
- **Timer ibrido**: campi di §8 nella sezione Timer (riusa `TimerSectionView`/`SegmentRowViewModel`).
- Il **colore habit** esistente (`HabitNode.Color`) ora pilota anche il pallino "completato" sul calendario — nessun nuovo controllo, solo nuova conseguenza.

## 10. Schema v2.8 — delta additivo

Singola migration `AbitudineCalendarV28`, tutte aggiunte (nessun drop/alter distruttivo):

| Tabella | Colonna | Tipo | Note |
|---|---|---|---|
| `note` | `target_date` | TEXT ISO date NULL | nota del giorno; NULL = nota libera |
| `habit_config` | `note_marker_color` | TEXT NULL | colore pallino nota; NULL = default |
| `habit_config` | `session_repeat_count` | INTEGER NULL | ripeti ciclo Session; NULL/1 = no loop |
| `segment_definition` | `duration_seconds` | INTEGER NULL | NULL = segmento manuale |

- Bump `AppSetting schema_version` → **2.8**.
- **Versioning HabitConfigHistory** (`HabitConfigHistoryColumns`): `SessionRepeatCount` → lista **`Versioned`** (è un setting comportamentale); `NoteMarkerColor` → lista **`Excluded`** (cosmetico, come `QuickComplete`). `SegmentDefinition` non è versionata in `HabitConfigHistory` (tabella separata).
- Cascade/indici dichiarati esplicitamente in Fluent API (nessun default EF). `note.target_date` indicizzato per il lookup "note del mese" (es. indice composito `(habit_node_id, target_date)`).
- Nessun nuovo `AppSetting` necessario (conteggio resta 18). Verificare il SQL generato contro la doc schema prima dell'update.

## 11. Architettura — mappatura sul codice

- **`CalendarNavigator`**: nuovo UserControl in `Views/Common/` + VM. Indipendente da habit. Consumato da `AbitudineColumnView`.
- **Colonna Abitudine**: nuova View+VM (`AbitudineColumnView` / `AbitudineColumnViewModel`) che (a) fornisce i marcatori del mese al calendario (query completamenti + note con `TargetDate` nel range), (b) renderizza il pannello-giorno per tipo riusando i controlli di marcatura M3.5 (quick-add, stepper, scala, timer), (c) media le note del giorno via `INoteService`.
- **Workspace**: come da nav-model spec, l'host colonne (`HabitScreen`/`MainWindow`) sostituisce la chip `Cronologia` con `Abitudine` nell'enum `HabitColumn { Abitudine, Dettaglio, Note, Configura }` e nel `ToggleColumn`.
- **Note**: `INoteService` esteso con query per `TargetDate` (giorno + range-mese). `IExportQueryService`/aggregatori non toccati (le note non entrano nelle metriche).
- **Query marcatori**: servizio/metodo che per `(habitNodeId, year, month)` ritorna `Dictionary<DateOnly, DayMarker>` combinando giorni con completamenti e giorni con note. Vive in `Recordum.Data` (come gli altri query service), interfaccia in `Recordum.Core`.
- **Timer**: estensione `IActiveTimerService` / macchina a stati Session per countdown+loop (vedi §8).
- **Chrome**: calendario, chip, header colonna usano i brush di tema (`Brush.Surface/Border/Accent`) già in `BaseStyles.xaml`. La tendina mese/anno usa lo stile ComboBox flat già introdotto (Pass 2/3) — risolve esplicitamente il "chiaro su chiaro".

## 12. Riuso per i Gruppi (rinviato)

`CalendarNavigator` nasce riusabile: la futura **colonna Gruppo** (cronologia aggregata delle attività figlie) gli passerà marcatori aggregati invece dei marcatori di una singola habit. **Design della colonna Gruppo rinviato** a un giro dedicato (quali marcatori aggregati, come si combinano i colori dei figli, cosa mostra il pannello-giorno di un gruppo). Qui ci limitiamo a non incatenare il calendario alla singola habit.

## 13. Fuori scope / bug prerequisiti (tracciati a parte)

In `docs/phase-4-issues.md`, **non** progettati qui (sono difetti, non design):
- **Configura non applica le modifiche** (autosave/persistenza editor): **prerequisito** — senza, il picker colore-nota e i campi timer non si vedono salvare. Verificare se è regressione del wiring B6c o autosave rotto.
- Creazione habit vuota senza prompt di configurazione + cronologia che si apre da sola.
- Grafici Dettaglio vuoti con pochi dati (investigare: dato sparso vs bug aggregazione).
- Rifiniture R aperte (picker scala, layout scala State, "+" State).

Fuori scope di design (rinviati): colonna Gruppo (§12), altre sezioni di Configura (metriche/scale/goal/scheduling/notifiche), confronto affiancato di due habit.

## 14. Testing

- **`CalendarNavigatorViewModel`**: navigazione mese (frecce + salto tendina), selezione giorno emette evento, marcatori resi con i colori passati, giorni futuri non selezionabili-per-marcatura, `FirstDayOfWeek` rispettato (culture-independent).
- **`AbitudineColumnViewModel`**: marcatori del mese = unione completamenti+note; marcatura giorno passato → `IsRetroactive=true`; marcatura oggi → normale; pannello-giorno cambia per tipo (Action/Counter/Resistance/State/Journal).
- **Note unificate**: nota creata in Abitudine con `TargetDate` compare nella query timeline della colonna Note; round-trip `TargetDate` persistito; marcatore 🟡 per giorni con nota.
- **Timer ibrido**: segmento senza durata = manuale (comportamento attuale invariato); segmento con durata = auto-avanza a fine countdown; `SessionRepeatCount` ripete il ciclo; recovery post-crash con ciclo ripetuto.
- **Schema/versioning**: migration additiva applica v2.8; `SessionRepeatCount` versionato in HabitConfigHistory, `NoteMarkerColor` escluso (test su `HabitConfigHistoryColumns`).
- **Verifica visiva** (skill `run`): calendario navigabile e leggibile (tendina scura), pallini con colore habit/nota, pannello-giorno per tipo, timer ibrido in Configura.

## 15. Domande aperte / da decidere in fase di piano

- Posizione esatta del picker colore-nota in Configura (header L1 vs sezione).
- Segnale acustico del timer ibrido: suono di default, silenziabile, file? (dettaglio implementativo).
- Editor della nota del giorno in Abitudine: TextBox semplice vs mini-markdown (il ricco resta nella colonna Note).
- Multi-completamento nello stesso giorno: il pannello-giorno mostra/aggrega più completamenti o ne edita uno? (seguire semantica M3.5; precisare in piano).
- Indice ottimale per la query "note del mese" (`(habit_node_id, target_date)` vs solo `target_date`).
