# Onda 1a — Chart funzionanti + export (design)

**Data**: 2026-07-03 · **Stato**: approvato dall'autore (brainstorm 2026-07-03)
**Contesto**: primo blocco esecutivo del piano v1.0 (`2026-07-03-v1-redefinition-plan-design.md`, Onda 1a). Chiude i findings **F0-1, F0-2, F0-3, F0-4, F0-5, F0-7, F0-9** dell'Onda 0 (tracker `docs/phase-4-issues.md`, sezione "Onda 0"). Exit criteria coperti: **E1** (chart Dettaglio per i 5 tipi), **E2** (charts custom end-to-end), **E3-parziale** (export corretto).

**Root cause verificati in esplorazione (2026-07-03)** — l'implementazione parte da qui, non da ipotesi:

| Finding | Root cause (file:riga) |
|---|---|
| F0-1 | `ChartsView.xaml:55-66`: riga serie con solo TextBox etichetta — nessun picker habit/metrica → `HabitNodeId=Guid.Empty` → FK failure → `DbUpdateException` non gestita in `ChartBuilderViewModel.SaveAsync` → crash. Backend (query→modello→renderer) completo e sano. |
| F0-2 | (a) `SchedulingAwareStreakAggregator.cs:57-58`: `cfg is null → Array.Empty` — habit senza riga config = streak morto; (b) `:117-119`: con recovery≠Retroactive conta solo completions live (`Count - RetroactiveCount`) — design M3.8 corretto, ma il seeder marca TUTTO `IsRetroactive=true` → demo a zero. |
| F0-3 | `TrendChartControl.xaml.cs:16-17`: `Rebuild()` solo su `DataContextChanged`/`Loaded`, mai al riempimento asincrono di `vm.Series` → race, mostra "Dati insufficienti" con dati presenti. |
| F0-4 | `HourlyDistributionQueryService.cs:19`: filtro `!IsRetroactive` (design corretto — l'ora di un retro non è significativa) + seeder tutto-retro + nessun empty-state nella sezione. |
| F0-5 | `ExportQueryService.BuildHabitAsync` legge solo completions: mai reso period-aware dopo U9 → export resistance vuoto/fuorviante. |
| F0-7 | `ExportQueryService.cs:74`: nome metrica hardcoded `null` in `ExportCompletionRow`. |
| F0-9 | `BackupService.cs:25`: `CurrentSchemaVersion = "2.7"` costante mai aggiornata → manifest sempre etichettato 2.7 (DB reale 2.12). |

---

## 1. Builder chart con anteprima viva (F0-1)

**Decisione autore**: builder con anteprima renderizzata live (coerente col principio "anteprima viva" della v1.0), non form minimale né wizard.

### Layout
Due colonne nel pannello builder di `ChartsView.xaml`: **form a sinistra** (Nome, Tipo, Periodo, righe serie, Salva), **anteprima a destra** — la view prodotta dal renderer esistente, rigenerata a ogni modifica con **debounce ~300ms** (pattern `DebouncedColorRecorder` come riferimento).

### Riga serie (`ChartSeriesRowViewModel`)
- **Picker abitudine**: ComboBox delle habit attive (da `IHabitTreeQueryService.GetHomeTreeAsync`, solo `NodeType.Habit`), item con colore+nome. Obbligatorio.
- **Picker metrica**: popolato al cambio habit con `IMetricDefinitionService.ListByHabitAsync`; prima voce **"Conteggio"** (= `MetricDefinitionId=null` → il backend usa già il ramo trend/conteggio); default: prima metrica se esiste, altrimenti Conteggio.
- **Etichetta**: auto-compilata `"{Habit}"` o `"{Habit} — {Metrica}"` al cambio dei picker; l'utente può sovrascriverla (flag "toccata a mano" per non ri-sovrascrivere).
- **Colore**: default = colore dell'habit scelta; modificabile con il `ColorSwatchPicker` esistente.

### Anteprima (refactor `IChartQueryService`)
Estrarre overload **`BuildModelAsync(Chart chart, IReadOnlyList<ChartSeries> series, bool isDark, ct)`** che lavora su oggetti in memoria; l'attuale `BuildModelAsync(chartId, …)` carica dal DB e delega all'overload. Il builder costruisce `Chart`+`ChartSeries` dallo stato corrente (righe senza habit **escluse** dall'anteprima), chiama l'overload e passa il modello a `IChartRenderer.CreateView` (stesso lookup di `ChartsWorkspaceViewModel.RenderAsync` — estrarre helper condiviso). Anteprima vuota → messaggio localizzato "Scegli un'abitudine per l'anteprima".

### Validazione + errori (mai più crash)
- `SaveCommand.CanExecute`: nome non vuoto **e** ≥1 serie con habit selezionata. Le righe senza habit vengono scartate al salvataggio.
- `SaveAsync` in try/catch: qualunque eccezione → banner errore localizzato nel builder (`ErrorMessage` osservabile, stile `SetUnexpectedError` esistente), niente propagazione all'`AsyncRelayCommand`.

### Minimo estetico (il grosso in Onda 3)
Empty-state della lista chart (icona line + "Nessun grafico"), "✕" della riga serie → icona Lucide coerente, spaziature/underline da linguaggio. Niente restyling completo qui.

## 2. Pipeline del Dettaglio (F0-2/3/4)

- **F0-3**: `TrendChartControl` si sottoscrive a `vm.Series.CollectionChanged` e `vm.PropertyChanged`(`MaxValue`) e ri-invoca `Rebuild()` (con unsubscribe sul vecchio VM al `DataContextChanged`). **Stesso audit su `HourlyHistogram`** e ogni altro controllo del Dettaglio che disegna da collezioni async (probabile stesso pattern).
- **F0-2a**: `SchedulingAwareStreakAggregator`: `cfg is null` → non più `Array.Empty` ma valutazione con **config di default** (ScheduleType Daily, StreakMode Daily, Recovery None) — un habit senza riga config non ha più streak permanentemente a zero. Il fallback è un dettaglio del motore: nessuna scrittura sul DB.
- **F0-2b (verifica in piano)**: confermare che builder "Nuova abitudine" ed editor creino sempre la riga `HabitConfig` alla creazione; se no, fixare lì (la causa (a) resta come rete di sicurezza per dati legacy).
- **F0-4**: il filtro `!IsRetroactive` **resta**. Si aggiunge l'empty-state localizzato sotto "Ora del giorno" ("Nessuna registrazione dal vivo in questo periodo" EN+IT) quando i bucket sono tutti zero.
- **Comportamento retro invariato**: le completions retroattive continuano a non contare per streak (salvo recovery=Retroactive) né per l'orario — è la semantica di M3.8, documentata; non si cambia qui.

## 3. Seeder demo credibile (parte di F0-2/F0-4)

`DemoDataSeeder`: le completions seedate diventano **live** (`IsRetroactive=false`) tranne ~10% lasciate retro per realismo; **ogni habit seedata riceve la sua riga config** (oggi solo 4 su 9 ce l'hanno). Gli orari realistici esistono già. Risultato atteso a demo: streak/andamento/orario popolati. (Il marker `dev.demo_seeded` resta; i dati già seedati sulla VM si rigenerano con reset + `--seed-demo`.)

## 4. Export (F0-5/7)

- **F0-5 — decisione autore: un periodo per riga.** `ExportHabitModel` guadagna `Periods: IReadOnlyList<ExportPeriodRow>` (`StartedAt`, `EndedAt` nullable, `Duration`, `IsOpen`). Per habit **Resistance**, `BuildHabitAsync` legge da `IAbstinenceService.ListPeriodsAsync` (niente completions); i **5 formatter** (PDF/HTML/MD/JSON/CSV) rendono la tabella periodi al posto della tabella completions (CSV: `habit,started,ended,duration,outcome`); la **heatmap export** colora i giorni puliti dai periodi (stessa logica del fix U9.1 in `YearHeatmapViewModel`); `total_completions` **mantiene il nome** (stabilità del formato JSON) ma per resistance vale = numero tentativi (periodi); il significato va annotato in `docs/plugin-api.md` se il formato export vi è documentato. `current/longest` già corretti (ramo periodi dell'aggregator).
- **F0-7**: `BuildHabitAsync` risolve **una volta per habit** il nome della metrica primaria (`IMetricDefinitionService`, stessa selezione `OrderBy(SortOrder).First` usata per il valore) e lo scrive nelle righe al posto del `null`.

## 5. Stamp schema backup (F0-9)

`BackupService.CreateAsync` legge `schema_version` dall'AppSetting (stesso approccio di `RestoreService.GetCurrentSchemaVersionAsync`); la costante resta solo come fallback per DB senza riga. Test: backup creato su DB 2.12 → manifest `SchemaVersion == "2.12"`; il valore NON va ri-hardcodato — deve seguire lo schema reale.

## 6. Test e verifica

TDD: ogni fix parte dal test che riproduce il sintomo. Minimo:
- Aggregator: habit senza config + completions live → streak > 0 (oggi 0).
- `TrendChartControl`: Series riempita dopo il load → il contenuto appare (test VM-level sul rebuild reattivo, o estrazione della condizione in un helper testabile).
- Builder: CanSave (nome+serie valida), righe senza habit scartate, `SaveAsync` con service che lancia → `ErrorMessage` valorizzato e niente eccezione; anteprima: overload `BuildModelAsync` in-memory produce serie corrette senza toccare il DB.
- Export: resistance → righe periodo (aperto senza fine), heatmap non vuota, counter → `metric` = "Pagine".
- Backup: manifest = schema_version dell'AppSetting.
- Suite complete Data+WPF verdi.

**Verifica finale a schermo (autore)**: E1 — Dettaglio dei 5 tipi con streak/andamento/orario popolati su demo ri-seedata; E2 — crea chart custom multi-serie con anteprima → salva → riapri; export Lettura (metric valorizzato) e Niente fumo (tabella periodi) riletti da file.

## 7. Fuori scope (esplicito)

Restyling completo schermata Charts (→ Onda 3); multi-metrica per serie e assi custom oltre l'esistente; foto/allegati (→ Onda 1b); lingua/EN default (→ Onda 4); qualsiasi cambiamento alla semantica retroactive di M3.8.
