# Calendar Cell Markers — Design

> **Data**: 2026-06-02 · **Fase**: 4 (Polish/UX) · **Branch**: `fase-4-polish`
> **Tipo**: rifinitura di una superficie esistente (marcatori del calendario nella colonna **Abitudine**). Nessuna nuova tabella, nessuna migrazione.

## 1. Scopo

Ridisegnare la **cella-giorno** del `CalendarNavigator` (tab Abitudine) perché comunichi, a colpo d'occhio e in modo universale:

1. **se** l'attività è stata svolta quel giorno (fatto / non fatto);
2. **quanto** (intensità) e **quante volte** (conteggio), risolvendo il caso "3 serie di flessioni" che oggi conta come 1;
3. **quante note** sono ancorate a quel giorno.

Sostituisce gli attuali due pallini (completato = colore habit, nota = ambra fissa) che si sovrappongono al numero del giorno e non distinguono il "quanto".

Il design emerge dal brainstorming 2026-06-02 (companion visivo): l'utente ha approvato l'ibrido **tint (heat) + numero**, con la nota come **angolino colorato per-abitudine**.

## 2. Decisioni bloccate (dal brainstorming)

- **Sfondo tinto (heat)** nel colore dell'abitudine = "fatto + intensità", a colpo d'occhio. Sostituisce il pallino-completato.
- **Conteggio come NUMERO** (non pallini), mostrato **solo quando porta informazione** (vedi §4) → i giorni "fatto una volta" restano puliti (solo tint).
- **Nota = angolino** in alto a destra, nel **colore scelto per l'abitudine** (`habit_config.note_marker_color`, già in schema v2.8). Numero note dentro la cella **solo se > 1**.
- **Striscia dettaglio** sotto il calendario: numeri esatti del giorno selezionato (fatto / quante volte / quante note + unità).
- **Tint = intensità relativa** (magnitudine ÷ massimo del mese visibile). *Niente* normalizzazione su goal: il modello non ha un goal per-giorno (`GoalPeriodType ∈ {Fixed, Rolling, Open}`). Vedi §9 (idee future).
- **Niente modalità configurabili dall'utente**: una sola regola automatica per tipo. Composizione > opzioni (CLAUDE.md §11).

## 3. Modello dati — nessuna migrazione

Tutto calcolabile dai dati esistenti. Si aggiungono **solo query**.

`CompletionValue.SeriesIndex` distingue le serie; `CompletionSegment.SegmentIndex` i segmenti di sessione; `Note.TargetDate` ancora le note al giorno (v2.8). `habit_config.note_marker_color` esiste (v2.8).

### 3.1 Nuova query — magnitudine giornaliera type-aware

`ICompletionService.GetDailyMagnitudeAsync(Guid habitNodeId, int year, int month, CancellationToken ct)`
→ `IReadOnlyList<DailyMagnitude>` dove `DailyMagnitude(DateOnly Date, decimal Magnitude)`.

La query carica il `HabitType` del nodo e calcola `Magnitude` per giorno secondo la tabella §4. Restituisce solo i giorni con `Magnitude > 0` (assenza ⇒ non fatto). Filtra per `CompletedAtMs` nel mese richiesto (stessa convenzione di `GetHeatmapAsync`).

### 3.2 Nuova query — conteggio note giornaliero

`INoteService.ListNoteCountsInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct)`
→ `IReadOnlyList<DayNoteCount>` dove `DayNoteCount(DateOnly Date, int Count)` (GROUP BY `target_date`, solo conteggi > 0).

Affianca l'esistente `ListNoteDaysInMonthAsync` (che resta per altri usi); il VM passa a quella nuova per avere il conteggio.

## 4. Magnitudine per tipo (regola unica)

`Magnitude` = il singolo numero rappresentativo del giorno. Il VM decide come renderizzarlo (§5.2).

| Tipo | `Magnitude` del giorno | Numero in cella |
|---|---|---|
| **action** | n° completamenti (di solito 1) | nascosto; "n×" solo se > 1 |
| **counter** | **somma** della metrica primaria del giorno (es. 100 pagine) | sempre, la quantità (`100`) |
| **resistance** | n° **serie** = somma su tutti i completamenti del giorno del conteggio di `SeriesIndex` distinti | se > 1 (il "3" richiesto) |
| **state** | **valore** sulla scala del giorno (se più voci, l'ultima per `CompletedAtMs`) | sempre, il valore (`4`) |
| **journal** | n° voci (di solito 1) | nascosto; "n×" solo se > 1 |

> **Session non è un `HabitType`**: è un counter con `timer_mode='session'`. In questa iterazione un counter-sessione è trattato come **counter** (somma metrica primaria). Il conteggio di sessioni/segmenti come magnitudine è un refinement futuro (richiederebbe leggere `timer_mode` nella query).

Note (separate dalla magnitudine): angolino sempre presente se ≥ 1 nota; numero dentro l'angolino solo se > 1.

## 5. Presentazione

### 5.1 `CalendarNavigator` resta riusabile e "muto"

Il controllo NON conosce il tipo di abitudine. Riceve celle già pronte. Il calcolo type-aware (tint step, stringa numero, normalizzazione) avviene nel **host** (`HabitScreenViewModel`), così il calendario resterà riusabile per la futura colonna Gruppo.

Il `CalendarMarkerProvider` cambia firma di ritorno: invece di `CalendarMonthMarkers(Completed, Notes)` (insiemi booleani) restituisce per-giorno una struttura **presentation-ready**:

```
record CalendarDayMarker(DateOnly Date, int TintStep, string? Number, int NoteCount);
record CalendarMonthMarkers(IReadOnlyList<CalendarDayMarker> Days);
```

`CalendarDayCellViewModel` perde `Completed`/`HasNote` booleani e guadagna:
- `int TintStep` (0–4; 0 = non fatto = nessun tint)
- `string? Number` (già formattato/localizzato; `null`/vuoto = niente numero)
- `int NoteCount` (0 = nessun angolino; > 1 = mostra conteggio)
- `string TintColorHex` (colore abitudine, propagato dal VM padre come oggi `CompletedColorHex`)

### 5.2 Calcolo nel host (`HabitScreenViewModel.LoadMonthMarkersAsync`)

1. Fetch `GetDailyMagnitudeAsync` + `ListNoteCountsInMonthAsync`.
2. **TintStep** (4 step):
   - `action`/`journal`: `Magnitude > 0 → step 4` (binario: fatto = pieno).
   - `counter`/`resistance`/`state`: `monthMax = max(Magnitude)` sul mese; `step = ceil(Magnitude / monthMax * 4)`, con `Magnitude > 0 → step ≥ 1`. (Per `state` il tint assoluto su `scaleMax` è un refinement futuro: richiederebbe risolvere la scala nel VM; il relativo è consistente e non necessita fetch aggiuntivi.)
3. **Number** (cultura corrente, vedi §4): nascosto per action/journal salvo `> 1` (allora `"{n}×"`); per resistance/session solo se `> 1`; per counter sempre la quantità; per state sempre il valore.
4. **NoteCount**: dal conteggio note.

### 5.3 Rendering cella (`CalendarNavigator.xaml`)

- **Sfondo**: `Background` = colore abitudine miscelato con la surface secondo `TintStep` (nuovo converter, §5.4). `TintStep 0` ⇒ trasparente.
- **Numero giorno**: in alto a sinistra, discreto (`Brush.Text`, dimmed se fuori mese).
- **Numero attività**: un **solo** slot, centrale-basso, allineato al centro (vale per tutti i tipi: quantità counter, valore state, conteggio resistance/session/action). Mai due posizioni. Colore ad alto contrasto sul tint (vedi token §8).
- **Angolino nota**: triangolo in alto a destra, `Fill = NoteColorHex` (già propagato). Conteggio (`NoteCount > 1`) sovrapposto, font piccolo, colore contrastante.
- **Oggi**: bordo accent (come ora). **Selezionato**: outline accent + (futuro `Brush.OnAccent`, vedi follow-up R-onAccent).
- **Futuro**: cella disabilitata (come ora).

### 5.4 Converter tint

Nuovo `IValueConverter` multi-input (o `IMultiValueConverter`) `HabitColorTintConverter`: input `(string hex, int step)` → `SolidColorBrush` = `hex` ad alpha crescente per step (es. step 1..4 → alpha 0.22 / 0.40 / 0.62 / 0.85) su `Brush.Surface`. Step 0 → `Transparent`. Riutilizza la logica di blending dell'esistente `IntensityStepToBrush` se compatibile; altrimenti nuovo converter dedicato (l'esistente usa una ramp fissa, non il colore abitudine).

### 5.5 Striscia dettaglio del giorno selezionato

Riga di riepilogo del giorno selezionato, **sopra** l'editor della nota (`DayNoteView`, già sotto il calendario). Mostra: stato (`✓ Fatto` / `—`), magnitudine con etichetta (es. `3 serie`, `100 pagine`, `Umore 4/5`), conteggio note. Localizzata.

Implementazione: estendere `DayNoteViewModel` con proprietà di riepilogo (`SummaryText`) popolate in `LoadDayAsync`, oppure un piccolo `DaySummaryViewModel` dedicato accanto. Preferenza: dentro `DayNoteViewModel` per riusare il caricamento già legato al giorno selezionato (un solo round-trip).

### 5.6 Picker colore-nota in Configura

Aggiungere alla sezione **Notifications/Notes** (o una micro-sezione "Aspetto") dell'editor Configura un **color picker** legato a `note_marker_color`. Salva via `IHabitConfigService.UpsertAsync` (già il percorso delle sezioni). Default `#e0b341` (ambra) se mai impostato. Validazione hex (`#RRGGBB`, maxLen 7, già vincolo schema). Alla modifica, il calendario riflette il nuovo colore (il VM ricarica `NoteColorHex` su evento config / al rientro nella tab).

> **Nota di scope**: il picker colore-nota era originariamente accorpato a Piano 3 (timer Session). Si sposta qui perché riguarda direttamente il marcatore del calendario. **Piano 3 resta solo il timer Session ibrido.**

## 6. Componenti e responsabilità

| Unità | Cosa fa | Dipende da |
|---|---|---|
| `ICompletionService.GetDailyMagnitudeAsync` (Data) | magnitudine type-aware per mese | DbContext, HabitType |
| `INoteService.ListNoteCountsInMonthAsync` (Data) | conteggi note per giorno | DbContext |
| `HabitScreenViewModel.LoadMonthMarkersAsync` (host) | compone magnitudine+note, calcola TintStep/Number/NoteCount | le due query, config (scaleMax, color) |
| `CalendarDayMarker` / `CalendarMonthMarkers` (WPF VM) | DTO presentation-ready | — |
| `CalendarNavigatorViewModel` + cella | rendering muto delle celle | i DTO |
| `HabitColorTintConverter` (WPF) | colore→brush per step | — |
| `DayNoteViewModel` (esteso) | riepilogo + editor nota del giorno | query del giorno |
| Sezione Configura (note color picker) | sceglie `note_marker_color` | `IHabitConfigService` |

## 7. Quick fix adiacenti (stessa tab, segnalati dall'utente)

Inclusi in questo lavoro perché toccano la stessa superficie e sono piccoli:

- **A — Range anni del navigatore troppo stretto.** `CalendarNavigatorViewModel.SelectableYears = Enumerable.Range(_today.Year - 5, 8)` → solo 2021..2028. Allargare a ~15 anni indietro + 1 avanti (`_today.Year - 15`, count 17). I pulsanti ‹ › restano comunque illimitati.
- **B — DatePicker ridondante nei QuickAdd.** Ogni template QuickAdd (Action/Counter/Resistance/Journal/State in `HabitScreen.xaml`) ha un `<DatePicker SelectedDate="{Binding CompletionDate}"/>` il cui popup è ancora chiaro-su-chiaro **ed** è ridondante: il calendario principale già pilota `CompletionDate` via `OnCalendarDateSelected`. Sostituire il `DatePicker` con un'etichetta **read-only** "Registrando per: \<data\>" (conferma esplicita del giorno, principio esplicito > implicito), così si rimuove il popup rotto senza perdere il contesto.

## 8. Theming / i18n

- Tutte le nuove stringhe (etichetta "Registrando per:", riepilogo giorno, label picker colore) in `.resx` EN + IT.
- Colore numero sul tint: usare un token ad alto contrasto. Allinearsi al follow-up **R-onAccent** (introdurre `Brush.OnAccent`) invece di `White` hardcoded.
- Angolino nota e tint rispettano dark/light via i brush di tema (lo sfondo blend parte da `Brush.Surface`).

## 9. Idee parcheggiate (future, fuori scope)

- **Figura geometrica guidata dal goal** (idea utente 2026-06-02): la cella mostra una forma che si riempie verso il goal (es. triangolo a 2/3 con 2 serie su 3; cerchio oltre N). Bloccata da tre nodi: (1) abitudini senza goal → serve fallback; (2) goal `Fixed/Rolling/Open` non mappano su una singola cella-giorno (progresso per-periodo, non per-giorno); (3) cambiare *tipo* di forma in base al numero rischia il "candy" vietato dal design brief. Da riprendere con un design dedicato se/quando si introduce un concetto di "target giornaliero".
- **Tint guidato dal goal**: conseguenza diretta del punto sopra; per ora il tint è intensità relativa.

## 10. Testing

- **Data** (`GetDailyMagnitudeAsync`): un test per tipo — action (count check-in), counter (somma metrica), resistance (somma `SeriesIndex` distinti su più completamenti = "3 serie in 1 completamento → 3"), state (ultimo valore), session (n° sessioni). Più: giorni vuoti esclusi; confine mese (fuso, `CompletedAtMs`).
- **Data** (`ListNoteCountsInMonthAsync`): conteggio per giorno, più note stesso giorno, mesi diversi.
- **Host VM** (`LoadMonthMarkersAsync`): TintStep per tipo (binario action; relativo counter; assoluto state via scaleMax); regole Number (nascosto/`n×`/quantità/valore); NoteCount → angolino/conteggio. `monthMax` con un solo giorno fatto. Niente regressione su selezione giorno → `CompletionDate`.
- **Quick fix A**: `SelectableYears` copre l'anno richiesto < 2021.
- Niente test su XAML (verifica visiva utente).

## 11. Fuori scope

- Modalità di riempimento configurabili dall'utente (esplicitamente scartate).
- Qualsiasi cosa guidata dal goal (§9).
- Refactor della colonna a chip / nav-model (parcheggiato altrove).
- Timer Session ibrido (Piano 3, separato).
