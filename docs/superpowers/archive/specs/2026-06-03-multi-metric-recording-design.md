# Multi-metric recording (B11) — design

> **Data**: 2026-06-03 · **Branch**: `fase-4-polish` · **Tracker**: `docs/phase-4-issues.md` → B11
>
> Risolve B11: l'abitudine costruita col builder (Spec 1) non riflette la configurazione nell'abitudine "funzionale". Root cause: lo strato di registrazione (QuickAdd, M3.5) è **mono-metrica per tipo canonico**, mentre il builder produce abitudini **multi-metrica**. Questo spec generalizza la registrazione a multi-metrica kind-aware.

## 1. Problema

`CounterQuickAddViewModel` e `StateQuickAddViewModel` prendono `metrics.OrderBy(SortOrder).FirstOrDefault()` (una sola metrica) e salvano un singolo valore senza mostrare l'unità. Il builder, invece, può comporre abitudini con più metriche di tipi misti (es. Quantità+Scala → `Counter` con 2 `MetricDefinition`; oppure due scale → `State` con 2 metriche). Risultato: tutte le metriche oltre la prima vengono **scartate dalla UI di registrazione** (i dati sarebbero registrabili, è la UI a non offrirne l'input) e l'unità non è mai mostrata.

`ResistanceQuickAddViewModel` è **già multi-metrica** (`SeriesRowViewModel` = una `SeriesCellViewModel` per metrica × serie), ma le sue celle non conoscono unità né scale.

## 2. Decisioni di design (dal brainstorming)

- **Un'abitudine può registrare più aspetti in un solo gesto** (scelta utente "una cosa sola"): misura + scala + più misure insieme. La registrazione deve mostrare e salvare **tutti** i campi configurati. Questo sfida il principio #4 (composizione > opzioni) ma è la decisione esplicita dell'utente.
- **Scope = solo registrazione** (Opzione 1). Dettaglio/grafici/heatmap/cell-marker restano sulla **metrica primaria** (prima per `SortOrder`); la visualizzazione multi-metrica è un follow-up separato (Opzione 2).
- **Cella range-aware per le scale**: bottoni segmentati con etichette per scale piccole, **slider** per scale grandi (1-100). Le scale restano **intere** in `[MinValue..MaxValue]` (invariante schema §5). I "decimali" non sono scale ma misure `Decimal`.
- **Grammatica builder invariata**: le composizioni miste restano permesse; non si restringono i guardrail.

## 3. Architettura

Modello concettuale: **una registrazione = N righe, ogni riga = una cella per `MetricDefinition`**. La cella sceglie il controllo dal *kind* della metrica.

| Kind | Riconoscimento | Controllo |
|---|---|---|
| **Measured** | `Integer`/`Decimal`/`Seconds`, `ScaleTemplateId == null` | input numerico + **etichetta unità** (es. `ml`) |
| **Scale** | `ScaleTemplateId != null` | bottoni-etichetta (≤10 passi) **o** slider (>10 passi) |
| **Text** | `MetricType.Text` | textbox |

Mappatura tipo abitudine → superficie:
- **Counter** e **State** → **1 riga** di celle kind-aware (1+ metriche, qualsiasi mix). *Unifica le due QuickAdd mono-metrica._
- **Resistance** → **N righe** + "aggiungi serie" (come oggi); le celle diventano le stesse kind-aware (mostra unità; la scala non entra in serie — `CanEnableSeries` è già false con scala).
- **Action** (nessuna metrica → "fatto") e **Journal** (testo) → invariati.

```
"Allenamento" (Counter, 2 metriche)            "Umore" (State, 1 scala)
┌──────────────────────────────────┐          ┌──────────────────────────┐
│ Durata   [ 30   ] min            │          │ Umore  ( 1 2 3 [4] 5 )   │
│ Energia  ( 1 2 3 [4] 5 )         │          │        Scarso → Ottimo    │
└──────────────────────────────────┘          └──────────────────────────┘
```

## 4. Componenti

### 4.1 `MetricInputKind` (helper, WPF QuickAdd)
Funzione pura `MetricInputKind Resolve(MetricDefinition m)`:
- `m.ScaleTemplateId != null` → `Scale`
- `m.Type == Text` → `Text`
- altrimenti → `Measured`

### 4.2 `SeriesCellViewModel` (potenziato — primitivo condiviso)
- ctor: `(MetricDefinition metric, string? unitCode = null, ScaleInfo? scale = null)` dove `ScaleInfo = (int Min, int Max, IReadOnlyList<string> Labels)`.
- nuove proprietà: `Kind`, `UnitLabel` (= `unitCode`), `ScaleLabels`, `StepCount`, `UseSlider` (= `StepCount > 10`), `SelectedScaleIndex` (`int?`, default `null`).
- `StepCount` = `ScaleLabels.Count` se ci sono etichette, altrimenti `Max - Min + 1`. `SelectedScaleIndex` è **0-based** sui passi (0..`StepCount`-1).
- **Encoding valore-scala**: `BuildValue` per una scala scrive `ValueInteger = SelectedScaleIndex` — **stesso encoding 0-based della `StateQuickAddViewModel` di oggi**, per non regredire i dati esistenti né l'interpretazione di Dettaglio/heatmap/cell-marker. (La mappatura indice↔valore reale Min..Max è puramente di presentazione: l'etichetta o il numero mostrato accanto allo slider è `Min + index`.)
- `BuildValue(seriesIndex)`:
  - `Scale`: se `SelectedScaleIndex is null` → `null`; altrimenti `ValueInteger = SelectedScaleIndex`.
  - `Measured`/`Text`: come oggi (Integer/Decimal/Seconds/Text).
- Retro-compatibilità: una cella creata senza `unitCode`/`scale` si comporta come oggi.

### 4.3 `MeasuredQuickAddViewModel` (rinomina di `CounterQuickAddViewModel`; **sostituisce** `StateQuickAddViewModel`)
- dipendenze: `ICompletionService`, `IMetricDefinitionService`, `IReferenceDataQueryService`, `IClock`.
- tiene **1 `SeriesRowViewModel`** (`Row`) costruita su tutte le metriche ordinate.
- `LoadMetricsAsync`: carica metriche (`ListByHabitAsync`, ordinate per `SortOrder`); risolve `unitCodeById` (`ListUnitsAsync`) e `scaleById` (`ListScalesAsync` → Min/Max/labels, con override `metric.LabelsJson` se presente, specificity §5); costruisce le celle kind-aware.
- `SaveAsync`: raccoglie `cell.BuildValue(0)` per ogni cella; se 0 valori → `ErrorMessage = QuickAdd_Error_ValueRequired`; altrimenti `RecordAsync` (stessa `Completion` di oggi: timestamp/retroattivo via `ResolveCompletionTimestamp`, `Notes`).

### 4.4 `ResistanceQuickAddViewModel`
- `LoadMetricsAsync` risolve `unitCodeById` e passa `unitCode` a ogni cella (le celle mostrano l'unità). Nessuna scala in serie.

### 4.5 XAML
- `DataTemplate` di cella kind-aware (per `SeriesCellViewModel`): scelta del controllo per `Kind`/`UseSlider` via trigger — numero+unità / tempo / bottoni-scala / slider-scala / testo.
- Vista `MeasuredQuickAddViewModel`: una riga di celle + Salva (+ note + data).
- Vista Resistance: griglia righe (riusa il template cella).
- Ritiro `DataTemplate`/View di `StateQuickAddViewModel`.

### 4.6 Host (`HabitScreenViewModel`)
- `HabitType.Counter` **e** `HabitType.State` → `new MeasuredQuickAddViewModel(...)`.
- switch `LoadMetricsAsync`: caso `MeasuredQuickAddViewModel`.

## 5. Flusso dati

1. **Load**: HabitScreen crea `MeasuredQuickAddViewModel`, `LoadMetricsAsync` → metriche ordinate + risoluzione unità/scale → 1 riga di celle.
2. **Input**: l'utente valorizza ogni cella (numeri, etichetta/slider scala).
3. **Save**: itera celle → `BuildValue(0)` → lista `CompletionValue` → `RecordAsync`.

Resistance: identico ma N righe (`AddSeries`), `BuildValue(i)` per riga `i`.

## 6. Error handling

- Nessuna cella valorizzata → `QuickAdd_Error_ValueRequired` (pattern esistente di Resistance).
- Cella scala senza selezione → non contribuisce (come una misura vuota).
- Risoluzione ref-data fallita / metrica senza unità → cella senza etichetta unità (degrada, non rompe).

## 7. Testing (TDD)

- **`SeriesCellViewModel`**: `Resolve` kind (scala vs misura vs testo); `BuildValue` scala (`SelectedScaleIndex`→`ValueInteger` 0-based, null→null); `UseSlider` soglia (10 vs 11 passi); `UnitLabel` portato.
- **`MeasuredQuickAddViewModel`**: load costruisce N celle per N metriche; save registra N `CompletionValue`; cella scala registra l'indice scelto; unità risolta nelle celle misura; **due-scale** (State multi) registra entrambe; singolo counter/state retro-compatibile; ≥1-valore richiesto.
- **Host**: `Counter` e `State` instradano a `MeasuredQuickAddViewModel`.
- **Resistance**: invariato + celle mostrano unità.

## 8. Fuori scope (esplicito)

- Dettaglio/grafici/heatmap/cell-marker multi-metrica (**Opzione 2**, follow-up).
- Creazione di scale personalizzate (1-10, 1-100, etichette) = **R-builderCustomScale**.
- Slider su misura `Decimal` con bound min/max (richiede un concetto di bound sulle misure, oggi assente).
- Color/icon picker, feedback "salva come template" = voci tracker separate.
- Grammatica builder invariata.

## 9. Rischi / note

- **Ripple type-driven**: streak/schedule/heatmap restano legati al `HabitType` derivato (Counter per mix Quantità+Scala). Coerente: una `Completion` = un record, lo streak conta il giorno. Nessuna modifica a quei sistemi.
- **Churn**: ritiro di `StateQuickAddViewModel` + relativa View/template + test. La UX del selettore-scala con etichette è preservata dentro la cella-scala (nessuna regressione).
- **Slider**: soglia 10 passi è una scelta iniziale; rivedibile dopo verifica visiva.
