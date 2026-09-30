# Astinenza nel motore streak — design

> **Data**: 2026-06-14 · **Branch**: `fase-4-polish` · **Tipo**: fix di comportamento su componente core (streak engine).
>
> **Fonti autoritative**: `Recordum_ProductSpec_v2.docx` (tipi habit), spec R-multiValue `2026-06-12-multivalue-habits-revision-design.md` (Resistance = astinenza pura; "completare = ricaduta"; display streak in tempo-trascorso). Tracker: `docs/phase-4-issues.md` (B-abstinence).

## 1. Problema

Il tipo `Resistance` modella l'astinenza ("niente fumo"): si registra un completamento **solo quando si ricade**, e lo streak rappresenta il **tempo trascorso dall'ultima ricaduta**. Questa semantica è documentata in `CompletionService` (commento "Resistance records abstinence lapses"; magnitudine = occorrenze → la ricaduta accende il giorno; completion nude ammesse) e nel display (`StreakSummaryViewModel` formatta lo streak Resistance come tempo trascorso, es. "2a 41g").

Ma **il motore streak non ha alcuna logica per Resistance**. `SchedulingAwareStreakAggregator` è completion-positivo: un giorno "conta" verso lo streak solo se ha un completamento, e un giorno schedulato senza completamento è un "miss" che spezza lo streak. Per l'astinenza questo è rovesciato:

- i giorni **puliti** (nessun completamento) diventano miss → spezzano lo streak;
- i giorni di **ricaduta** (completamento) contano in positivo verso lo streak.

Risultato: il "tempo dall'ultima ricaduta" mostrato dalla UI è calcolato su uno streak invertito → privo di senso. Nessun payload del template "Stop smoking" lo aggiusta, perché lo streak cresce solo sui giorni con completamento e nessuna config fa crescere lo streak senza input giornaliero. Il repo contiene inoltre tre modelli contraddittori di astinenza (template a zero metriche; demo che fa check-in ~70% giorni; seed-habit che tratta i completamenti come ricadute rare).

**Decisione**: implementare la semantica d'astinenza nel motore (non aggirare via template/seed).

## 2. Modello (deciso)

- **Streak corrente** = giorni puliti consecutivi fino a oggi. Mai ricaduto → conta dalla creazione dell'habit. Una ricaduta (qualunque completamento quel giorno) chiude la run pulita; la nuova run riparte dal giorno dopo.
- **Streak corrente a precisione fine**: il numero "grande" corrente è calcolato all'ora/minuto dall'**istante** dell'ultima ricaduta (`Completion.CompletedAtMs`), es. "2a 41g 6h". I run **storici** restano a giorni.
- **Dimensioni di config**: Resistance usa un percorso self-contained che **ignora** `schedule_type`, `streak_mode`, `streak_recovery`. Le **pause** restano onorate (un periodo in pausa è congelato: non conta come pulito né come ricaduta).

## 3. Componenti

### 3.1 Algoritmo d'astinenza (`SchedulingAwareStreakAggregator.GetHistoryAsync`)

Ramo dedicato in testa al metodo: se `habitNode.HabitType == HabitType.Resistance`, esegui la computazione d'astinenza e ritorna (salta tutto il loop completion-positivo).

Input:
- `habitCreatedDate` = `DateOnly.FromDateTime(habitNode.CreatedAt)`.
- **Giorni di ricaduta**: da `completions.GetHeatmapAsync(habitId, fromDate, toDate)` → un giorno è "ricaduta" se `Count >= 1`. (La distinzione retroactive non si applica all'astinenza: ogni completamento è una ricaduta.)
- **Pause**: `pauses.ListByHabitAsync(habitId)`.

Computazione (a giorni, su `[fromDate..toDate]` clampato a `>= habitCreatedDate`):
- Itera giorno per giorno costruendo **run pulite** = sequenze massimali di giorni consecutivi che non sono né ricaduta né in pausa.
- Un giorno in **pausa** non apre/chiude/estende una run e non è una ricaduta: è trasparente (la run prosegue oltre la pausa).
- Un giorno di **ricaduta** chiude la run corrente (se aperta) con `StreakBreakReason.Lapse`; la run successiva potrà aprirsi dal primo giorno pulito seguente.
- La run che arriva fino a `today` è marcata `IsCurrent = true`.
- `StreakRun.Length` = numero di giorni puliti nella run (esclusi i giorni in pausa).

Output: `IReadOnlyList<StreakRun>` (stesso tipo del percorso generico), così `StreakSummaryViewModel`, charts (StreakTimeline) ed export consumano run pulite coerenti per le Resistance.

Nuovo valore enum `StreakBreakReason.Lapse` (accanto a `Missed`, `Manual`).

### 3.2 Streak corrente a precisione fine (display)

`StreakSummaryViewModel.LoadAsync`, ramo `node.HabitType == Resistance`:
- **Istante "pulito da"**: l'ultima ricaduta = `completions.ListByHabitAsync(habitId, take: 1)` (più recente; verificare che l'ordinamento sia newest-first, altrimenti ordinare per `CompletedAtMs` desc). Se esiste → `cleanSince = DateTimeOffset.FromUnixTimeMilliseconds(lastLapse.CompletedAtMs)`. Se non esiste → `cleanSince = habitNode.CreatedAt`.
- **Pause**: sottrai dalla finestra `[cleanSince, now]` i giorni interi coperti da pause (`StreakPause` è a granularità giorno).
- `elapsed = now - cleanSince - pausedDays`; `CurrentDisplay = DurationFormat.FormatElapsed(elapsed)`.

### 3.3 `DurationFormat.FormatElapsed(TimeSpan)`

Nuovo formatter (accanto a `FormatDays`) per il tempo trascorso, a cascata di unità con le due più significative:
- `>= 1 anno` → "{a}a {g}g" (es. "2a 41g")
- `>= 1 giorno` → "{g}g {h}h" (es. "41g 6h")
- `>= 1 ora` → "{h}h {m}m" (es. "6h 12m")
- `>= 1 minuto` → "{m}m"
- altrimenti → "appena ora"

Stringhe localizzate (EN+IT) per le unità/"appena ora" via `.resx` (English-first).

### 3.4 Display card "Run 90g" → "Ricadute 90g"

Per Resistance, la card che oggi mostra `RunsLast90Days` mostra invece il **numero di giorni di ricaduta negli ultimi 90 giorni** (conteggio dei giorni con `Count >= 1` nella finestra). Etichetta dedicata localizzata ("Ricadute (90g)" / "Lapses (90d)"). Per gli altri tipi resta invariata ("Run (90g)"). `LongestLength`/`LongestStartedOn` per Resistance = la run pulita più lunga (già fornita da §3.1).

### 3.5 Editor

`HabitEditorViewModel`: per le habit `Resistance` nascondi/disabilita le sezioni **Scheduling**, **Streak (mode)** e **Recovery** (ignorate dal motore). Restano visibili Metriche (metriche dell'episodio: quantità/tempo/scala), Notifiche, Tag, AI, ecc. Seguire il pattern di visibilità sezione già esistente (applicabilità per tipo).

### 3.6 Seed / template

- **Template seed "Stop smoking": nessuna modifica.** Produce già una `Resistance`; è il motore a renderlo funzionante. Nessuna migrazione di seed (niente cambi a `HabitTemplateSeed.HasData`).
- **Demo `--seed-demo` "Niente fumo"** (`DemoDataSeeder`, codice runtime): allinea al modello ricadute. Oggi registra "Voglie resistite" ~70% dei giorni (check-in errato). Nuovo: niente check-in giornaliero; semina **poche ricadute sparse** nella finestra (~100g) — es. 2–3 ricadute, l'ultima ~3 settimane fa — così la verifica mostra uno streak pulito lungo (settimane) + qualche marker di ricaduta sul calendario, e "Più lungo"/"Ricadute 90g" sensati. La metrica "Voglie resistite" può restare come metrica opzionale dell'episodio (registrata solo nei giorni di ricaduta) oppure essere rimossa; scelta in fase di piano, irrilevante per il modello.

## 4. Testing

- **Aggregator (astinenza)**, `Recordum.Data.Tests`:
  - mai ricaduto → una run pulita corrente da creazione a oggi, `IsCurrent`.
  - ricaduta singola → due run; quella corrente parte dal giorno dopo la ricaduta; `BreakReason.Lapse`.
  - ricadute consecutive → nessuna run pulita tra esse.
  - ricaduta oggi → la run corrente pulita termina ieri; lunghezza 0 della finestra odierna.
  - pausa che congela → i giorni in pausa non spezzano la run né contano come puliti.
  - più lunga run pulita = `LongestLength` atteso.
- **`DurationFormat.FormatElapsed`**, `Recordum.WPF.Tests`: anni+giorni / giorni+ore / ore+minuti / minuti / "appena ora"; culture-independent.
- **`StreakSummaryViewModel` (Resistance)**, `Recordum.WPF.Tests`: `CurrentDisplay` da istante ultima ricaduta (con `FakeClock`); `Longest` = run pulita più lunga; "Ricadute 90g" = conteggio giorni ricaduta in finestra.
- Nessuna modifica schema → nessun test di migrazione.

## 5. Fuori scope (YAGNI)

- Goal per Resistance.
- **Sottrazione delle pause dal display fine corrente** (descopato in implementazione, 2026-06-14): l'elapsed "grande" è `now − ultima ricaduta` senza sottrarre i giorni in pausa. I run a giorni dell'aggregator (Più lungo / timeline) **sì** escludono le pause → per una Resistance *con pause* il numero corrente e i run storici possono divergere. Caso raro (astinenza + pausa); implementarlo richiede iniettare `IStreakPauseService` in `StreakSummaryViewModel` (cascata su `DetailViewModel`). Da fare se serve la precisione su quel caso.
- **Localizzazione di `DurationFormat`**: `"appena ora"` e le abbreviazioni `a/g/h/m` restano hardcoded (coerenti con `FormatDays` preesistente). Localizzare l'intera classe è un refactor separato.
- Localizzazione dei nomi display dei template seed.
- Riscrittura del percorso generico dell'aggregator per gli altri tipi.

## 6. Impatto / rischi

- `GetHistoryAsync` cambia output per le sole Resistance: i consumer (display, StreakTimeline chart, export streak) ne beneficiano (run corrette) senza modifiche di firma.
- Nessun cambio di schema, nessuna migrazione. Cambia solo: enum `StreakBreakReason` (+`Lapse`), un branch nell'aggregator, un formatter, rami display/editor, e il demo seeder (codice).
- Il template "Stop smoking" resta invariato: la verifica conferma che ora funziona via motore.
