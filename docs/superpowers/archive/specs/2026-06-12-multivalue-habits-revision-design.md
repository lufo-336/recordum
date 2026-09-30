# Revisione abitudini multivalore (R-multiValue) — Design

> **Fase 4 / Polish-UX.** Brainstorming 2026-06-12. Companion visivo: `.superpowers/brainstorm/2009-1781260245/`. Emenda la grammatica della spec builder `2026-06-02-modular-habit-builder-design.md` §4 (che resta autoritativa per bozza/lancio/template); il modello dominio resta governato da `CLAUDE.md` §5/§6.

## 1. Problema

Tre incoerenze emerse dall'uso reale (tracker `docs/phase-4-issues.md` §R-multiValue):

1. **Il mattone "conteggio" tradisce la promessa del contatore.** Un counter dovrebbe rispondere a "quante volte" con un gesto rapido (+1); oggi invece il quick-add (`MeasuredQuickAddViewModel`) chiede di *digitare* un numero — indistinguibile da una quantità senza unità.
2. **"Serie attiva → resistance"** (grammatica §4.2 #2) fa collidere due semantiche opposte: "flessioni 3×" (fare serie di esercizi) e "niente fumo" (astinenza) condividono `HabitType.Resistance`. Effetto collaterale grave: lo streak engine tratta le flessioni con semantica da astinenza (completare = ricaduta).
3. **Il seeder** (`DemoDataSeeder.SeedHabitAsync`) popola solo la prima metrica: backfill di una habit multi-metrica lascia le altre vuote.

In più: il "numero di serie" pre-impostato nel builder non ha riscontro reale (le serie si aggiungono mentre si fanno), e manca un modo per cronometrare un campo tempo direttamente in registrazione.

## 2. Decisioni (lockate col brainstorm)

| # | Decisione |
|---|---|
| D1 | **La riga-serie È il check**: registrare una riga = serie eseguita. Niente metrica "contatore di serie"; il conteggio deriva dalle righe. |
| D2 | **Counter = occorrenze a tap**: counter nudo → bottone +1; con campi → riga/righe di celle; le occorrenze sono le serie. |
| D3 | **Serie = proprietà del Counter**, non più un tipo: la grammatica non produce più `resistance` dal toggle serie. Resistance torna SOLO astinenza. Migrazione dati leggera. |
| D4 | **Mattone contatore = tap, quantità = digitato** con unità *opzionale*. Il vecchio "conteggio" digitabile è assorbito da quantità. |
| D5 | **Niente numero di serie pre-impostato**: il toggle Serie e `SeriesCount` spariscono dal builder; "+ Aggiungi serie" è sempre disponibile sui counter con campi. (Un eventuale target di serie sarà un Goal, fuori scope.) |
| D6 | **Serie valida = ≥1 campo compilato**; riga vuota ignorata al salvataggio; campo vuoto in riga valida = valore *mancante*, statisticamente visibile. |
| D7 | **Input durata = campo mascherato** `mm:ss` auto-scalante (scelta A del companion). |
| D8 | **⏱ crono-cella** sul campo tempo, riusando `active_timer` (recovery incluso). Per durate di *attività*; l'astinenza pluriennale ("da quanto non fumo") si serve con la streak Resistance, non con un crono aperto. |
| D9 | **Niente `MetricType.Boolean`**: l'abitudine "fatto e basta" è già `Action`; il check per-serie è implicito (D1). |

## 3. Grammatica dei mattoni (sostituisce spec builder §4.1–§4.3)

### 3.1 Mattoni

- **Contatore** — dichiarazione "conta occorrenze". **Non crea `MetricDefinition`**: nel draft è un flag (`CountsOccurrences`). Max uno; ridondante (e rimosso/disabilitato) quando ci sono campi o astinenza: lì il conteggio è implicito nelle occorrenze.
- **Quantità** — campo numerico digitato. Unità **opzionale**: senza unità → `MetricType.Integer` ("Ripetizioni", "Pagine"); con unità → `MetricType.Decimal` + `UnitOfMeasureId` ("Acqua" in L).
- **Tempo** — durata → `MetricType.Seconds`. Input mascherato (D7) + ⏱ (D8).
- **Scala** — invariato (`Integer` + `ScaleTemplateId`/`LabelsJson`).
- **Testo libero** — invariato (definente del journal).
- **Astinenza** — *nuovo* mattone-base esclusivo → `resistance`. Necessario perché la Resistance dal builder nasceva solo dal toggle Serie (che sparisce). Può coesistere con quantità/tempo/scala (metriche dell'episodio di tentazione/ricaduta); non con testo né contatore.
- ~~Serie~~ — eliminata dalla palette (D5).

### 3.2 Risoluzione del tipo — per priorità

| # | Condizione sui mattoni | `habit_type` |
|---|---|---|
| 1 | C'è **testo libero** | **journal** |
| 2 | C'è **astinenza** | **resistance** |
| 3 | Solo campo/i **scala**, nessun quantità/tempo/contatore | **state** |
| 4 | **Contatore** oppure ≥1 campo **quantità/tempo** | **counter** |
| 5 | Nessun mattone | **action** |

### 3.3 Guardrail

| Mattone | + testo | + astinenza | + contatore | + quantità/tempo | + scala |
|---|:---:|:---:|:---:|:---:|:---:|
| **testo** | — | ✗ | ✗ | tempo¹ ✓ | ✗ |
| **astinenza** | ✗ | — | ✗ | ✓ | ✓ |
| **contatore** | ✗ | ✗ | — | ✗² | ✗² |
| **quantità/tempo** | tempo¹ | ✓ | ✗² | ✓ | ✓ |
| **scala** | ✗ | ✓ | ✗² | ✓ | ✓ |

¹ Rilassamento journal+tempo invariato (spec builder §4.3).
² Il contatore è il mattone del counter *nudo*: con qualsiasi campo il conteggio è implicito nelle occorrenze, quindi la palette lo disabilita (tooltip esplicativo) e viceversa.

I modi di registrazione (manuale/cronometro/countdown) restano come in spec builder; il ⏱ crono-cella (D8) è indipendente dal modo.

## 4. Registrazione (quick-add Counter nelle tre forme)

Riferimento visivo: mockup `counter-quickadd.html` nel companion.

1. **Counter nudo** (contatore, zero metriche): bottone **+1** prominente + conteggio del giorno. Ogni tap registra subito una `Completion` **senza valori**; ⌫ annulla l'ultima occorrenza di oggi (delete dell'ultima completion odierna). Conteggio giorno = numero di occorrenze.
2. **Counter con campi, uso semplice**: una riga di celle (come oggi) + **Registra** → una `Completion` con `SeriesIndex=0`.
3. **Counter con campi, a serie**: la stessa superficie ha sempre **"+ Aggiungi serie"** (e ✕ per riga): più righe → una `Completion` con le righe *valide* come `SeriesIndex` 0..n-1. Indicatore per riga: **✓** appena ≥1 campo è compilato, **○** se vuota (ignorata). Un campo lasciato vuoto in una riga valida **non** produce `CompletionValue` → "mancante" per le statistiche (D6). Il pulsante Registra riporta il numero di serie valide ("Registra 2 serie").

**Occorrenze del giorno** (counter) = Σ sulle completion del giorno di `max(1, numero di SeriesIndex distinti)`. Counter nudo: ogni completion vale 1.

**Quick action +1 da Home** (`CounterQuickActionHandler`): counter nudo → completion senza valori (oggi scrive `ValueInteger=1` sulla prima metrica e fallisce senza metriche); counter con campi → naviga alla Cronologia della habit (un +1 cieco lascerebbe campi sistematicamente vuoti).

**Resistance** torna astinenza pura: il quick-add registra l'episodio (ricaduta/tentazione) con le eventuali metriche dell'episodio su una riga; la UI multi-serie (`AddSeries`/`RemoveSeries`) trasloca al Counter. Rifinitura inclusa: **streak Resistance mostrata in formato tempo-trascorso** ("2a 41g") nelle superfici dove oggi è un numero di giorni.

### 4.1 Cella tempo: mask + ⏱

- Input mascherato `mm:ss`, auto-promozione a `h:mm:ss` (digitando o via crono). Display di valori esistenti auto-scalante: `45:12` → `1:32:05` → `3g 14:22`.
- **⏱ start/stop**: start crea l'`ActiveTimer` (mode `Stopwatch`) per la habit → corre persistito, sopravvive a crash/riavvio, compare nella `TimerStatusBar`; stop riempie la cella (modificabile). Vincolo ereditato: **un crono attivo per habit** (UNIQUE su `habit_node_id`) — coerente con "una serie alla volta". Recovery: dopo un riavvio lo stop dalla status bar propone la registrazione con il tempo nella metrica tempo (riusa il percorso di recovery M3.5).

## 5. Dati, migrazione, compatibilità

- **Nessun cambio di schema.** "Serie" non è uno stato persistito (non lo era nemmeno prima: viveva solo nel tipo derivato); il multi-riga è sempre disponibile sui counter con campi.
- **Migrazione dati v2.10** (`CounterReclassV210`, migration EF di soli UPDATE — nessun DDL): `habit_type='resistance'` con **≥1 metrica misurata** (`Integer`/`Decimal`/`Seconds` senza `ScaleTemplateId`) → `'counter'`. Le Resistance d'astinenza (zero metriche misurate, es. "Niente fumo" del seeder demo) restano intatte. Bump `AppSetting.schema_version` → `2.10`. Verificare il SQL generato prima dell'update (convenzione §8 CLAUDE.md).
- **Template payload**: i payload esistenti con campi natura `count` si istanziano come quantità-senza-unità (`Integer`) — compat conservata; `series_count` nei payload vecchi viene ignorato. I nuovi payload serializzano il mattone contatore come `counts_occurrences: true` (nessun campo). `FieldNature.Count` resta nell'enum per il parsing dei payload storici; la palette non lo offre più come campo.
- **`MetricType` invariato** (D9). `HabitConfig`/`HabitConfigHistory` non toccati.

## 6. Seeder

`DemoDataSeeder.SeedHabitAsync` rifatto per essere **multi-metrica e natura-aware**:

- Carica *tutte* le `MetricDefinition` della habit e genera un valore per natura: quantità `Integer` con progressione realistica, quantità `Decimal` plausibile per l'unità, `Seconds` in range attività (20–120s per serie), scala pesata (riusa `WeightedMood`).
- Counter con campi: **1–4 serie/giorno** (righe `SeriesIndex`), con **~10% di campi lasciati vuoti** in righe valide (testa il "mancante" D6 e dà realismo).
- Counter nudo: N occorrenze/giorno (N completions senza valori).
- Routing per il *nuovo* significato dei tipi (resistance = episodi rari, non valori giornalieri).

## 7. Impatti / consumatori

| Area | Impatto |
|---|---|
| `HabitCompositionRules` (Core) | Nuova risoluzione §3.2 + guardrail §3.3; `HabitComposition` perde `SeriesEnabled`, guadagna `CountsOccurrences` e `AbstinenceEnabled`. |
| `HabitBuilderViewModel` + palette/preview | Mattoni nuovi (contatore-tap, astinenza), via toggle/`SeriesCount`; anteprima rende le tre forme §4. |
| Quick-add | `ResistanceQuickAddViewModel` (multi-riga) diventa il quick-add del **Counter con campi**; counter nudo → nuova vista +1; Resistance → registrazione episodio su riga singola; `MeasuredQuickAddViewModel` resta per State. Selettori (`QuickAddTemplateSelector`, factory in `HabitScreenViewModel`) aggiornati. |
| `CounterQuickActionHandler` | Completion senza valori per counter nudo; navigazione per counter con campi. |
| Cella tempo | Nuovo controllo mask + ⏱ in `SeriesCellViewModel`/template XAML; integrazione `IActiveTimerService`. |
| Aggregazioni | Magnitudine giornaliera del counter = occorrenze (serie valide), non somma della prima metrica: aggiornare il query service usato da calendario/heatmap/day-summary (`GetDailyMagnitudeAsync` e affini) e il day-summary formatter. |
| Streak | Nessun cambio motore; le ex-"flessioni" migrate a counter escono dalla semantica astinenza (fix implicito). Display streak Resistance in tempo-trascorso. |
| Stringhe | `.resx` English-first per mattoni nuovi, +1, indicatori serie, ⏱, formati durata. |

## 8. Error handling / validazione

- Registra con **zero righe valide** → errore esistente `QuickAdd_Error_ValueRequired`.
- Mask durata: input non parsabile → la cella si marca invalida (bordo errore) e non produce valore; la riga resta valida se *un altro* campo è compilato — il campo invalido blocca solo se stesso.
- ⏱ con crono già attivo sulla stessa habit → il bottone è in stato "stop" ovunque (un solo crono); start su habit con crono attivo da status bar → disabilitato con tooltip.
- Migrazione: UPDATE idempotente (re-run senza effetto); rollback transazionale come ogni migration.
- +1 e ⌫ sono operazioni su singola completion: nessun rischio di perdita oltre l'occorrenza toccata.

## 9. Testing

- **Grammatica**: ogni riga di §3.2 + guardrail §3.3 (incluso contatore↔campi mutuamente esclusivi, astinenza+metriche, rilassamento journal+tempo invariato).
- **Quick-add Counter**: tre forme; serie valide/ignorate; valore mancante non produce `CompletionValue`; "Registra N serie"; +1/⌫; occorrenze del giorno (formula §4).
- **Crono-cella**: start/stop riempie la cella; persistenza/recovery via `active_timer`; vincolo un-crono-per-habit.
- **Mask durata**: parsing/format round-trip (`mm:ss`, `h:mm:ss`, valori lunghi), input invalido.
- **Migrazione v2.10**: resistance-con-metriche → counter; "Niente fumo" (zero metriche misurate) intatto; idempotenza; bump versione.
- **Seeder**: tutte le metriche popolate per natura; multi-serie; ~10% mancanti; idempotenza invariata.
- **Aggregazioni**: magnitudine counter = occorrenze su dataset misto (nude + multi-serie).

## 10. Fuori scope

- **Grafici + dati + correlazioni** (filone grande, brainstorm dedicato successivo — anticipa parte della Fase 9).
- **Goal "N serie target"** (l'erede legittimo del vecchio `SeriesCount`).
- **B6b template custom** e catalogo template ufficiali.
- Session mode / segmenti (invariati, Spec 2 builder).
