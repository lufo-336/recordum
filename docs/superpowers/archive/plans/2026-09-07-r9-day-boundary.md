# R9 — Il giorno di appartenenza: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un solo punto decide a quale giorno appartiene un istante — ora locale + cascata `reset_time` — e streak, heatmap, calendario, andamento, grafici, export e la fascia "Oggi" lo chiamano tutti, invece di derivare ciascuno la propria data UTC.

**Architecture:** Un valore puro `DayBoundary` in Core (`DayOf` / `Today` / `RangeOf`, sincroni, senza I/O) prodotto da `IResetTimeResolver`, che oggi cammina già la cascata habit → gruppi antenati → `AppSetting daily_reset_time` → `00:00` e non ha un solo chiamante. Ogni operazione risolve **una volta** in cima e poi chiama la funzione pura dentro i cicli: i servizi Data lo risolvono da sé (le firme di `ICompletionService` & co. **non cambiano**, quindi i ~20 call site restano intatti), i VM WPF lo **ricevono come parametro** da chi carica la schermata (`HabitScreenViewModel`, `HomeViewModel`), che è l'unico punto WPF a iniettare il resolver. Nessun cambio di schema: si continua a salvare istanti, il giorno resta un'interpretazione.

**Tech Stack:** C#/.NET 10, EF Core + SQLite (SQLCipher), WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/archive/specs/2026-08-19-day-boundary-design.md` — **leggerla prima di ogni task**. Findings a tracker: R9 (+ R10, che ne è un campione) in `docs/phase-4-issues.md`.

## Global Constraints

- **Branch**: creare `r9-day-boundary` **da `wave-2-visual-pass`** (dove vivono spec e piano), in una chiamata separata dal primo commit — `guard-main.js` legge il branch *prima* di eseguire, quindi un comando composto che parte da un branch protetto viene negato per intero. Mai commit su `main`.
  - **Conseguenza da sapere, non da scoprire**: mergiando poi `r9-day-boundary` in `main` entrano anche i commit già presenti su `wave-2-visual-pass` (fix cultura `e8bd480`/`d14b49f`/`d5a75c4`, i due spec del 19 agosto, i findings `W2V-*` a tracker). È lavoro finito e approvato, ma è un merge più largo del solo R9: se l'autore lo vuole isolato, ribasare R9 su `main` **prima** del merge.
- **Nessuna migrazione EF, nessun cambio di schema.** `schema_version` resta **2.17**. Se un task sembra richiedere una migration, fermarsi: è fuori spec (§6). L'unica scrittura di manutenzione è il backfill del Task 11, che gira nell'`ApplicationInitializer`, **non** in SQL.
- **Il giorno si deriva da `CompletedAtMs` / `StartedAtMs` / `EndedAtMs`, mai da `CompletedAt`.** `CompletedAt` è un `DateTime` che EF materializza con `Kind = Unspecified`: convertirlo in `DateTimeOffset` senza `SpecifyKind` prende l'offset della macchina e sbaglia in silenzio. I campi `_ms` sono istanti non ambigui. Dove serve per forza un `DateTime` (es. `HabitNode.CreatedAt`, `HabitConfig.StreakBrokenAt`, che non hanno gemello `_ms`) usare l'overload `DayBoundary.DayOf(DateTime utcTimestamp)`, che fa lo `SpecifyKind` per te.
- **Mai `DateTime.UtcNow` / `DateTime.Now` / `DateTimeOffset.Now`**: solo `IClock`. E d'ora in poi mai nemmeno `DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)`: "oggi" è `boundary.Today(clock)`.
- **Le firme pubbliche di `ICompletionService`, `IAbstinenceService`, `ITrendQueryService`, `IHourlyDistributionQueryService`, `IExportQueryService` non cambiano.** I servizi risolvono il proprio `DayBoundary` internamente (una `await` per chiamata, mai per riga). È la scelta che tiene il diff piccolo e i call site intatti.
- **Build/test**: sempre `-p:UseAppHost=false` (lock `CreateAppHost` di DriveFS). La soluzione è `Recordum.slnx`. Suite WPF **serializzata**, o su questa VM può appendersi a zero fallimenti (W24-E1): `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false -- xUnit.ParallelizeTestCollections=false`. Se si appende, **rilanciare, non indagare**.
- **Ogni test di regressione va provato contro il codice rotto** (CLAUDE.md §8): disattivare la conversione appena scritta, vedere il rosso, ripristinare. È la trappola di W24-R2/R3 — test verdi che non sarebbero mai falliti. Ogni task lo chiede in uno step esplicito; non è facoltativo.
- **I fusi orari nei test si costruiscono, non si cercano.** Mai `TimeZoneInfo.FindSystemTimeZoneById`: dipende dal tz database dell'OS e questa suite gira su Windows e su macOS. Usare `TimeZoneInfo.CreateCustomTimeZone` (offset fisso) o l'helper `TestTimeZones.RomeLike` del Task 1 (con regola di ora legale).
- **Stringhe utente** in `.resx`: una stringa nuova = 3 edit (`Strings.resx` EN + `Strings.it.resx` IT + accessor a mano in `Strings.Designer.cs`, bloccato dall'hook per Edit/Write → **editare via shell**). Questo piano non dovrebbe averne bisogno: se un task ne richiede una, è il segnale che sta allargando lo scope.
- **Convenzioni test**: xUnit + FluentAssertions. Data usa `DbFixture` (`tests/Recordum.Data.Tests/Fixtures`), il cui `FakeClock` ha `LocalTimeZone = TimeZoneInfo.Utc` di default — ed è **il motivo per cui la suite esistente non deve spostarsi in massa**: con fuso UTC e reset `00:00`, `DayBoundary` è l'identità e i vecchi assert restano veri. Un test che *vuole* misurare la conversione si costruisce il proprio `FakeClock(start, tz)`.
- Commit frequenti. Messaggi `feat(day):` / `fix(day):` / `test(...)` / `docs:`, footer:

  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01NiQhrzo4Ktpjn77F9ufVcy
  ```

## Il rischio da dire prima

Passando da UTC a locale **lo storico si ri-interpreta**: ogni registrazione fatta fra mezzanotte e l'offset locale cambia giorno, e con essa streak, heatmap ed export dello storico. È corretto (prima erano nel giorno sbagliato), ma **un numero di streak può cambiare al primo avvio dopo l'aggiornamento**. Va scritto nel tracker (Task 12), non scoperto dall'autore durante il dogfooding.

---

## §0 — L'audit dei siti (spec §4)

La spec conta **55** `DateOnly.FromDateTime`. Verificato: sono 55, **più tre** derivazioni della stessa famiglia scritte come `.UtcDateTime.Date`, che il grep della spec non vedeva (`HabitTreeQueryService:28`, `QuickAddViewModelBase:34` e `:51`) — **58 in famiglia A**. E c'è una **famiglia B** che la spec nomina solo di sfuggita chiedendo `RangeOf`: **~30 siti nella direzione opposta** (`DateOnly → istante`), tutti che assumono **mezzanotte UTC**. Sono i filtri di query: convertendo solo la famiglia A i bucket sarebbero giusti ma la **finestra** da cui pescano resterebbe sfalsata, e il risultato sarebbe peggiore di adesso.

Legenda: **C** = converti · **P** = lascia, è presentazione · **D** = lascia, è già un `DateOnly` di dominio.

### Famiglia A — istante → giorno (58)

| # | Sito | Cosa deriva | | Task |
|---|---|---|---|---|
| A1 | `SchedulingAwareStreakAggregator:63` | giorno di creazione habit (gate schedule + settimana) | **C** | 4 |
| A2 | `SchedulingAwareStreakAggregator:96` | "oggi" (flag `IsCurrent`) | **C** | 4 |
| A3 | `SchedulingAwareStreakAggregator:149` | `StreakBrokenAt` → giorno (recovery Manual) | **C** | 4 |
| A4 | `SchedulingAwareStreakAggregator:192` | giorno di creazione (ramo astinenza) | **C** | 4 |
| A5 | `SchedulingAwareStreakAggregator:194` | "oggi" (ramo astinenza) | **C** | 4 |
| A6 | `SchedulingAwareStreakAggregator:213` | inizio periodo astinenza | **C** | 4 |
| A7 | `SchedulingAwareStreakAggregator:217` | fine periodo astinenza | **C** | 4 |
| A8 | `CompletionService:135` | bucket heatmap | **C** | 3 |
| A9 | `CompletionService:162` | bucket somma giornaliera | **C** | 3 |
| A10 | `CompletionService:205` | bucket magnitudine giornaliera (calendario) | **C** | 3 |
| A11 | `DiaryService:30` | "oggi" per decidere `IsRetroactive` | **C** | 10 |
| A12 | `NoteService:87` | ancora di ordinamento nota (`TargetDate ?? CreatedAt`), client-side | **C** | 9 |
| A13 | `ChartQueryService:69` | "oggi" per il range 30d/90d/1y | **C** | 5 |
| A14 | `ExportQueryService:198` | "oggi" (ancora heatmap export) | **C** | 6 |
| A15 | `ExportQueryService:208` | inizio periodo astinenza | **C** | 6 |
| A16 | `ExportQueryService:211` | fine periodo astinenza | **C** | 6 |
| A17 | `TrendQueryService:34` | bucket andamento | **C** | 5 |
| A18 | `HabitTreeQueryService:28` (`.UtcDateTime.Date`) | "oggi" della riga Home + strisce 14g | **C** | 7 |
| A19 | `DemoDataSeeder:67` | "oggi" del seed demo | **C** | 10 |
| A20 | `DemoDataSeeder:188` | "oggi" del seed demo | **C** | 10 |
| A21 | `DateOnlyToDateTimeConverter:13` | `DateTime` del DatePicker → `DateOnly` | **P** | — |
| A22 | `ExportDialogViewModel:105` | "oggi" per il range di default | **C** | 9 |
| A23 | `AbstinencePeriodRowViewModel:49` | giorno d'inizio del periodo (riga Registrazioni) | **C** | 9 |
| A24 | `AbstinencePeriodRowViewModel:63` | giorno di fine | **C** | 9 |
| A25 | `CalendarNavigatorViewModel:36` | "oggi" del calendario (già locale, manca il reset) | **C** | 9 |
| A26 | `GoalProgressCardViewModel:94` | "oggi" (periodo goal) | **C** | 9 |
| A27 | `GoalProgressCardViewModel:99` | giorno di creazione (goal aperto) | **C** | 9 |
| A28 | `GoalProgressCardViewModel:106` | "oggi" (finestra rolling) | **C** | 9 |
| A29 | `HourlyHistogramViewModel:29` | "oggi" (finestra 90g) | **C** | 9 |
| A30 | `StreakSummaryViewModel:49` | "oggi" | **C** | 9 |
| A31 | `StreakSummaryViewModel:50` | giorno di creazione (storico all-time) | **C** | 9 |
| A32 | `StreakSummaryViewModel:80` | fine periodo astinenza (tentativi 90g) | **C** | 9 |
| A33 | `StreakTimelineViewModel:18` | "oggi" | **C** | 9 |
| A34 | `TrendChartViewModel:57` | "oggi" | **C** | 9 |
| A35 | `YearHeatmapViewModel:25` | "oggi" (ancora della griglia anno) | **C** | 9 |
| A36 | `YearHeatmapViewModel:82` | inizio periodo astinenza | **C** | 9 |
| A37 | `YearHeatmapViewModel:86` | fine periodo astinenza | **C** | 9 |
| A38 | `HabitRecordsViewModel:76` | "oggi" del filtro periodo (già locale, manca il reset) | **C** | 9 |
| A39 | `HabitScreenViewModel:668` | "oggi" passato al calendario (già locale, manca il reset) | **C** | 9 |
| A40 | `HabitScreenViewModel:909` | "oggi" dei marker astinenza — **questo è R10** | **C** | 9 |
| A41 | `HabitScreenViewModel:943` | inizio periodo (marker) | **C** | 9 |
| A42 | `HabitScreenViewModel:945` | fine periodo (marker) | **C** | 9 |
| A43 | `JournalQuickEntryViewModel:71` | "oggi" del composer nota veloce (già locale) | **C** | 9 |
| A44 | `JournalQuickEntryViewModel:124` | ancora della nota (`TargetDate ?? CreatedAt`) | **C** | 9 |
| A45 | `NoteRowViewModel:34` | ancora della nota | **C** | 9 |
| A46 | `NotesViewModel:121` | ancora della nota (`AnchorOf`) | **C** | 9 |
| A47 | `NotesViewModel:132` | "oggi" del filtro periodo (già locale) | **C** | 9 |
| A48 | `AbstinenceQuickAddViewModel:83` | giorno dell'ultima ricaduta | **C** | 9 |
| A49 | `JournalQuickAddViewModel:32` | `CompletionDate.Date` → `DateOnly` | **D** | — |
| A50 | `ActionTodayContent:56` | "oggi" (già locale) | **C** | 8 |
| A51 | `ActionTodayContent:72` | giorni distinti negli ultimi 30 (già locale) | **C** | 8 |
| A52 | `CounterTodayContent:53` | "oggi" (già locale) | **C** | 8 |
| A53 | `ResistanceTodayContent:57` | "oggi" (UTC — incoerente coi fratelli) | **C** | 8 |
| A54 | `StateTodayContent:95` | "oggi" (già locale) | **C** | 8 |
| A55 | `StateTodayContent:122` | "oggi" (last-wins della giornata) | **C** | 8 |
| A56 | `HomeRowViewModel:282` | "oggi" della cifra chiave streak (già locale) | **C** | 8 |
| A57 | `QuickAddViewModelBase:34` (`.UtcDateTime.Date`) | data iniziale del quick-add | **C** | 10 |
| A59 | `PanoramicaViewModel` → `Goal.LoadAsync` (goal di gruppo) | **mancava in questa tabella**: aggiunta dopo la review di branch, che ci ha trovato I1 — il gruppo riceveva il confine del soggetto precedente perché lo screen risolveva `_dayBoundary` dopo il ramo gruppo | **C** | 9 |
| A58 | `QuickAddViewModelBase:51` (`.UtcDateTime.Date`) | "oggi" per decidere `IsRetroactive` | **C** | 10 |

**Lo scisma, misurato**: 22 di questi calcolano già "oggi" in locale (A25, A38, A39, A43, A47, A50–A52, A54–A56 e i loro derivati), 14 in UTC. Fra mezzanotte e le 02:00 le due metà indicano **giorni diversi** — è la famiglia di cui R10 è un campione. Dopo questo piano il modo di calcolare "oggi" è **uno**.

### Famiglia B — giorno → istante (finestre di query)

| # | Sito | | Task |
|---|---|---|---|
| B1 | `CompletionService:127-128` (heatmap) | **C** | 3 |
| B2 | `CompletionService:144-145` (somma giornaliera) | **C** | 3 |
| B3 | `CompletionService:173-174` (magnitudine mensile) | **C** | 3 |
| B4 | `AbstinenceService:69-70` (`ListPeriodsAsync`) — **attenzione**: il chiamante (`AbstinencePeriodListViewModel`) passa `DateOnly.MinValue`/`MaxValue` come **sentinelle** per "tutte le registrazioni". `RangeOf` deve trattarle entrambe, o `StartOf(MinValue)` lancia in ogni fuso a offset positivo (R9-C2) | **C** | 4 |
| B5 | `HabitConfigTimeline:18` (fine giornata per risolvere la config del giorno) | **C** | 4 |
| B6 | `HourlyDistributionQueryService:14-15` | **C** | 5 |
| B7 | `TrendQueryService:14-15` | **C** | 5 |
| B8 | `ExportQueryService:62-63` | **C** | 6 |
| B9 | `ExportQueryService:163-164` | **C** | 6 |
| B10 | `HabitTreeQueryService:29-31` (`startMs`/`endMs`/`todayMs` + divisione per 86 400 000) | **C** | 7 |
| B11 | `GoalProgressCardViewModel:56-57` | **C** | 9 |
| B12 | `CompletionListViewModel:54-55` | **C** | 9 |
| B13 | `ResistanceTodayContent:60` (`fromMs` tentativi 90g) | **C** | 8 |
| B14 | `ActionTodayContent:84` (`LocalDayWindow` fatta a mano) | **C** → `RangeOf` | 8 |
| B15 | `StateTodayContent:123` (stessa finestra locale a mano) | **C** → `RangeOf` | 8 |
| B16 | `DiaryService:32` (timestamp retroattivo = mezzanotte UTC) | **C** → mezzogiorno locale | 10 |
| B17 | `ChartQueryService:106` (`DayNumber` → OADate per l'asse X) | **P** | — |
| B18 | `TrendChartControl.xaml.cs:63` (idem, resa del grafico) | **P** | — |
| B19 | `DateOnlyToDateTimeConverter:10` | **P** | — |
| B20 | `HabitScreenViewModel:673` e `:1055` (`QuickAdd.CompletionDate = date.ToDateTime(...)`) | **D** | — |

**La divisione per 86 400 000 di B10 è un difetto a parte**: assume che ogni giorno duri 24 ore. Col confine locale un giorno di ora legale ne dura 23 o 25, e l'aritmetica sfasa la striscia di un quadretto attorno al cambio d'ora. Il Task 7 la sostituisce con `DayOf`, non la "corregge".

### Famiglia C — scritture (l'istante è l'istante)

`CompletedAt = clock.UtcNow.UtcDateTime` in `ActionTodayContent:112`, `StateTodayContent:132`, `ActionQuickActionHandler:17`, `CounterQuickActionHandler:24`, `ActionQuickAddViewModel:25`, `CounterSeriesQuickAddViewModel:209`, `CounterTapQuickAddViewModel:39`, `MeasuredQuickAddViewModel:62`, `ActiveTimerService:66`, `DiaryService:39` — **lascia tutte**: una registrazione fatta adesso si stampiglia adesso, e la rilettura la colloca da sé (spec §5).

Cambiano solo le **retroattive** (Task 10): `QuickAddViewModelBase:52-53`, `DiaryService:32`, e `CompletionRowViewModel:74` (la modifica a mano della data di una registrazione, che oggi reinterpreta come UTC il valore mostrato).

### Non classificato = dimenticato

Il Task 12 aggiunge un **test-guardia** che rilegge i sorgenti e fallisce se ricompare un `DateOnly.FromDateTime(...UtcDateTime)` o un `.UtcNow.UtcDateTime.Date` fuori dalla whitelist di questa tabella. È il gemello del guard di R7 e di `BuilderTemplateParityTests`: senza di lui questa tabella scade al primo merge.

---

## File Structure

```
src/Recordum.Core/
  Time/DayBoundary.cs                              [C] il valore puro (DayOf/Today/RangeOf)
  Abstractions/Services/IResetTimeResolver.cs      [M] +GetDayBoundaryAsync +batch +app-wide
src/Recordum.Data/
  Services/ResetTimeResolver.cs                    [M] produce DayBoundary; batch a 2 query
  Services/CompletionService.cs                    [M] A8-A10, B1-B3
  Services/AbstinenceService.cs                    [M] B4
  Services/DiaryService.cs                         [M] A11, B16
  Services/NoteService.cs                          [M] A12
  Services/Aggregators/SchedulingAwareStreakAggregator.cs [M] A1-A7
  Services/Aggregators/HabitConfigTimeline.cs      [M] B5
  Services/Queries/TrendQueryService.cs            [M] A17, B7
  Services/Queries/HourlyDistributionQueryService.cs [M] B6
  Services/Queries/ChartQueryService.cs            [M] A13
  Services/Queries/ExportQueryService.cs           [M] A14-A16, B8-B9
  Services/Queries/HabitTreeQueryService.cs        [M] A18, B10
  Maintenance/RetroactiveNoonBackfill.cs           [C] passo di manutenzione (Task 11)
src/Recordum.WPF/
  Bootstrap/ApplicationInitializer.cs              [M] invoca il backfill dopo le migrazioni
  Bootstrap/ServiceRegistrar.cs                    [M] registra il backfill
  Bootstrap/DemoDataSeeder.cs                      [M] A19-A20
  ViewModels/HomeViewModel.cs                      [M] risolve i confini in batch
  ViewModels/HomeRowViewModel.cs                   [M] A56
  ViewModels/HabitScreen/HabitScreenViewModel.cs   [M] risolve una volta; A39-A42 (R10)
  ViewModels/HabitScreen/Today/*.cs                [M] A50-A55, B13-B15
  ViewModels/HabitScreen/Detail/*.cs               [M] A26-A37, B11
  ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs [M] A25
  ViewModels/HabitScreen/{HabitRecordsViewModel,CompletionListViewModel}.cs [M] A38, B12
  ViewModels/HabitScreen/Notes/*.cs, Journal/*.cs  [M] A43-A46
  ViewModels/HabitScreen/QuickAdd/*.cs             [M] A48, A57-A58
  ViewModels/HabitScreen/CompletionRowViewModel.cs [M] modifica data retroattiva
  ViewModels/Export/ExportDialogViewModel.cs       [M] A22
tests/
  Recordum.Core.Tests/Time/{DayBoundaryTests,TestTimeZones}.cs      [C]
  Recordum.Data.Tests/Services/ResetTimeResolverTests.cs            [C]
  Recordum.Data.Tests/Integration/DayBoundaryParityTests.cs         [C] le 5 superfici
  Recordum.Data.Tests/Maintenance/RetroactiveNoonBackfillTests.cs   [C]
  Recordum.Data.Tests/Architecture/DayDerivationGuardTests.cs       [C] il guard
  (+ modifiche ai test esistenti dove cambia un costruttore)
```

---

### Task 1: Core — `DayBoundary`, il valore puro

Il cuore. Tutto il resto del piano è cablaggio: se questo è giusto e provato, i casi limite (mezzanotte, reset alle 04:00, giorno di 23 e di 25 ore) non tornano mai più a mordere. È in `Recordum.Core` perché è dominio senza dipendenze, e perché così si prova **senza database**.

**Files:**
- Create: `src/Recordum.Core/Time/DayBoundary.cs`
- Create: `tests/Recordum.Core.Tests/Time/TestTimeZones.cs`
- Create: `tests/Recordum.Core.Tests/Time/DayBoundaryTests.cs`

**Interfaces:**
- Consumes: `Recordum.Core.Abstractions.IClock` (`UtcNow`, `LocalTimeZone`).
- Produces (tutti i task successivi dipendono da queste firme esatte):
  - `Recordum.Core.Time.DayBoundary` — `sealed record`, costruttore `(TimeZoneInfo TimeZone, TimeOnly ResetTime)`
  - `static DayBoundary DayBoundary.Utc` — fuso UTC + reset `00:00` (l'identità: è il comportamento di oggi)
  - `DateOnly DayOf(DateTimeOffset instant)`
  - `DateOnly DayOf(long unixMs)`
  - `DateOnly DayOf(DateTime utcTimestamp)` — fa `SpecifyKind(Utc)` internamente
  - `DateOnly Today(IClock clock)`
  - `DateTimeOffset StartOf(DateOnly day)`
  - `DateTimeOffset NoonOf(DateOnly day)` — mezzogiorno locale, l'istante a cui si stampigliano le retroattive (Task 10 e 11)
  - `(DateTimeOffset From, DateTimeOffset ToExclusive) RangeOf(DateOnly day)`
  - `(DateTimeOffset From, DateTimeOffset ToExclusive) RangeOf(DateOnly fromDay, DateOnly toDayInclusive)`
  - `TestTimeZones.RomeLike` / `TestTimeZones.Fixed(int hours)` (solo test)

- [ ] **Step 1: Scrivere l'helper dei fusi di prova**

`tests/Recordum.Core.Tests/Time/TestTimeZones.cs`. Non è un test: è l'attrezzo che rende deterministici i test di ora legale su qualunque OS.

```csharp
namespace Recordum.Core.Tests.Time;

/// <summary>Fusi costruiti a mano: mai FindSystemTimeZoneById, che dipende dal tz database dell'OS
/// (questa suite gira su Windows e su macOS).</summary>
public static class TestTimeZones
{
    /// <summary>UTC+1 con ora legale +1h dall'ultima domenica di marzo (02:00) all'ultima domenica
    /// di ottobre (03:00) — la forma di Europe/Rome, senza dipendere da Europe/Rome.
    /// Nel 2026: salto in avanti il 29 marzo, indietro il 25 ottobre.</summary>
    public static TimeZoneInfo RomeLike { get; } = BuildRomeLike();

    /// <summary>Fuso a offset fisso, senza ora legale (es. Fixed(2) = UTC+2, Fixed(-5) = UTC-5).</summary>
    public static TimeZoneInfo Fixed(int hours)
    {
        var id = $"Test{(hours >= 0 ? "+" : "")}{hours}";
        return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(hours), id, id);
    }

    private static TimeZoneInfo BuildRomeLike()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0), month: 3, week: 5, DayOfWeek.Sunday);
        var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 3, 0, 0), month: 10, week: 5, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1), start, end);
        return TimeZoneInfo.CreateCustomTimeZone(
            "Test/RomeLike", TimeSpan.FromHours(1), "Rome-like (test)", "CET", "CEST", [rule]);
    }
}
```

- [ ] **Step 2: Scrivere i test che falliscono**

`tests/Recordum.Core.Tests/Time/DayBoundaryTests.cs`. Sono la spec §7 tradotta: mezzanotte, reset 04:00, i due giorni di ora legale, un fuso negativo.

```csharp
using FluentAssertions;
using Recordum.Core.Time;
using Xunit;

namespace Recordum.Core.Tests.Time;

public class DayBoundaryTests
{
    private static DayBoundary Rome(int resetHour = 0)
        => new(TestTimeZones.RomeLike, new TimeOnly(resetHour, 0));

    // --- DayOf: il caso che ha aperto R9 ---

    [Fact]
    public void Half_past_midnight_local_belongs_to_the_local_day_not_the_UTC_one()
    {
        // 2026-08-18T22:30Z = 2026-08-19 00:30 a Roma (UTC+2 d'estate).
        var instant = new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero);
        Rome().DayOf(instant).Should().Be(new DateOnly(2026, 8, 19));
    }

    [Theory]
    // reset 00:00: il confine è mezzanotte locale
    [InlineData(0, "2026-08-18T21:59:59Z", "2026-08-18")]
    [InlineData(0, "2026-08-18T22:00:00Z", "2026-08-19")]
    // reset 04:00: le 02:30 locali appartengono al giorno prima, le 04:00 in punto al giorno dopo
    [InlineData(4, "2026-08-19T00:30:00Z", "2026-08-18")] // 02:30 locali
    [InlineData(4, "2026-08-19T01:59:59Z", "2026-08-18")] // 03:59:59 locali
    [InlineData(4, "2026-08-19T02:00:00Z", "2026-08-19")] // 04:00:00 locali — inclusivo in avanti
    public void DayOf_honours_the_reset_time(int resetHour, string instantIso, string expectedDay)
        => Rome(resetHour).DayOf(DateTimeOffset.Parse(instantIso, CultureInfo.InvariantCulture))
            .Should().Be(DateOnly.Parse(expectedDay, CultureInfo.InvariantCulture));

    [Fact]
    public void Negative_offset_zone_is_handled_too()
    {
        // UTC-5: le 20:00 locali del 18 sono l'01:00Z del 19.
        var utcMinus5 = new DayBoundary(TestTimeZones.Fixed(-5), TimeOnly.MinValue);
        utcMinus5.DayOf(new DateTimeOffset(2026, 8, 19, 1, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateOnly(2026, 8, 18));
    }

    // --- RangeOf: la finestra e il giorno devono essere la stessa cosa ---

    [Fact]
    public void RangeOf_and_DayOf_agree_on_every_boundary()
    {
        var b = Rome(4);
        var day = new DateOnly(2026, 8, 19);
        var (from, toExclusive) = b.RangeOf(day);

        b.DayOf(from).Should().Be(day);
        b.DayOf(from.AddTicks(-1)).Should().Be(day.AddDays(-1));
        b.DayOf(toExclusive.AddTicks(-1)).Should().Be(day);
        b.DayOf(toExclusive).Should().Be(day.AddDays(1));
    }

    [Fact]
    public void RangeOf_spans_from_the_first_day_to_the_end_of_the_last()
    {
        var b = Rome();
        var (from, toExclusive) = b.RangeOf(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
        from.Should().Be(b.StartOf(new DateOnly(2026, 8, 1)));
        toExclusive.Should().Be(b.StartOf(new DateOnly(2026, 9, 1)));
    }

    // --- Ora legale: la classe di bug che questo piano può introdurre ---

    [Fact]
    public void Spring_forward_day_is_23_hours_long()
    {
        var (from, to) = Rome().RangeOf(new DateOnly(2026, 3, 29));
        (to - from).Should().Be(TimeSpan.FromHours(23));
    }

    [Fact]
    public void Fall_back_day_is_25_hours_long()
    {
        var (from, to) = Rome().RangeOf(new DateOnly(2026, 10, 25));
        (to - from).Should().Be(TimeSpan.FromHours(25));
    }

    [Fact]
    public void A_reset_inside_the_skipped_hour_starts_the_day_at_the_transition()
    {
        // Il 29 marzo l'orologio salta da 02:00 a 03:00: le 02:00 locali non esistono.
        // Il giorno comincia all'istante della transizione (01:00Z), non un'ora prima o dopo.
        Rome(2).StartOf(new DateOnly(2026, 3, 29))
            .Should().Be(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_reset_inside_the_repeated_hour_starts_the_day_at_its_first_pass()
    {
        // Il 25 ottobre le 02:30 locali capitano due volte (00:30Z con +2, 01:30Z con +1).
        // Il giorno comincia alla prima, così entrambe le occorrenze gli appartengono.
        var b = Rome(2).WithReset(new TimeOnly(2, 30));
        b.StartOf(new DateOnly(2026, 10, 25))
            .Should().Be(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero));
        b.DayOf(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero))
            .Should().Be(new DateOnly(2026, 10, 25));
    }

    [Fact]
    public void Every_instant_of_a_DST_day_lands_in_exactly_one_day()
    {
        // La prova di non-sovrapposizione e non-buco: passo di 10 minuti su entrambe le transizioni.
        var b = Rome(2).WithReset(new TimeOnly(2, 30));
        foreach (var d in new[] { new DateOnly(2026, 3, 29), new DateOnly(2026, 10, 25) })
        {
            var (from, to) = b.RangeOf(d);
            for (var t = from; t < to; t = t.AddMinutes(10))
                b.DayOf(t).Should().Be(d, $"{t:O} sta dentro la finestra di {d}");
        }
    }

    // --- NoonOf: dove atterrano le retroattive (Task 10 e 11) ---

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(6)]
    public void Noon_always_belongs_to_the_day_it_names(int resetHour)
    {
        // È la ragione per cui la scelta è "mezzogiorno" e non "mezzanotte": sta dentro il giorno
        // per qualunque reset plausibile (00:00-06:00).
        var b = Rome(resetHour);
        var day = new DateOnly(2026, 8, 15);
        b.DayOf(b.NoonOf(day)).Should().Be(day);
    }

    [Fact]
    public void Noon_belongs_to_its_day_even_when_the_day_is_23_or_25_hours_long()
    {
        var b = Rome();
        foreach (var d in new[] { new DateOnly(2026, 3, 29), new DateOnly(2026, 10, 25) })
            b.DayOf(b.NoonOf(d)).Should().Be(d);
    }

    // --- L'identità: è ciò che rende la suite esistente ancora vera ---

    [Fact]
    public void Utc_boundary_is_the_identity_of_todays_behaviour()
    {
        var instant = new DateTimeOffset(2026, 8, 18, 23, 30, 0, TimeSpan.Zero);
        DayBoundary.Utc.DayOf(instant).Should().Be(DateOnly.FromDateTime(instant.UtcDateTime));
    }
}
```

> `WithReset` è lo `with` del record (`b with { ResetTime = ... }`): scriverlo così nei test è una comodità, non un metodo da aggiungere. Se preferisci, usa `new DayBoundary(TestTimeZones.RomeLike, new TimeOnly(2, 30))` e togli `WithReset`. Ricordati `using System.Globalization;` per i `Parse`.

- [ ] **Step 3: Eseguire i test e verificare che falliscano**

```
dotnet test tests/Recordum.Core.Tests -p:UseAppHost=false --filter FullyQualifiedName~DayBoundaryTests
```
Atteso: errori di compilazione — `DayBoundary` non esiste.

- [ ] **Step 4: Scrivere l'implementazione**

`src/Recordum.Core/Time/DayBoundary.cs`:

```csharp
using Recordum.Core.Abstractions;

namespace Recordum.Core.Time;

/// <summary>
/// A quale giorno appartiene un istante, per una data abitudine: ora locale + orario di reset.
/// <para>Valore puro e sincrono — nessuna dipendenza, nessun I/O. Si risolve <b>una volta</b> in cima
/// a un'operazione (via <see cref="Abstractions.Services.IResetTimeResolver"/>) e poi si chiama dentro
/// i cicli: i siti di bucketing stanno dentro <c>foreach</c> e dentro VM dove una <c>await</c> per riga
/// diventerebbe una query per riga. Vedi spec R9 (2026-08-19) §3.</para>
/// <para>Il confine è <b>inclusivo in avanti</b>: con reset 04:00, le 04:00:00 in punto sono il primo
/// istante del giorno nuovo, le 03:59:59 l'ultimo del vecchio.</para>
/// </summary>
public sealed record DayBoundary(TimeZoneInfo TimeZone, TimeOnly ResetTime)
{
    /// <summary>Fuso UTC, reset a mezzanotte: l'identità, cioè il comportamento pre-R9.
    /// Usarlo solo come default esplicito nei test o dove non c'è (ancora) un'abitudine.</summary>
    public static DayBoundary Utc { get; } = new(TimeZoneInfo.Utc, TimeOnly.MinValue);

    public TimeZoneInfo TimeZone { get; } =
        TimeZone ?? throw new ArgumentNullException(nameof(TimeZone));

    /// <summary>Il giorno di appartenenza dell'istante.</summary>
    public DateOnly DayOf(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime;
        return DateOnly.FromDateTime(local - ResetTime.ToTimeSpan());
    }

    /// <summary>Il giorno di appartenenza di un timestamp Unix ms (<c>completed_at_ms</c>,
    /// <c>started_at_ms</c>, <c>ended_at_ms</c>). È la forma da preferire: il millisecondo è un
    /// istante non ambiguo, la colonna <c>DateTime</c> gemella no.</summary>
    public DateOnly DayOf(long unixMs) => DayOf(DateTimeOffset.FromUnixTimeMilliseconds(unixMs));

    /// <summary>Il giorno di appartenenza di un timestamp salvato come <c>DateTime</c> UTC
    /// (<c>HabitNode.CreatedAt</c>, <c>HabitConfig.StreakBrokenAt</c>: non hanno gemello <c>_ms</c>).
    /// EF li materializza con <c>Kind = Unspecified</c>, quindi lo <c>SpecifyKind</c> lo facciamo qui
    /// una volta invece di dimenticarcelo in venti chiamanti.</summary>
    public DateOnly DayOf(DateTime utcTimestamp)
        => DayOf(new DateTimeOffset(DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc)));

    /// <summary>L'unica definizione di "oggi" dell'applicazione.</summary>
    public DateOnly Today(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return DayOf(clock.UtcNow);
    }

    /// <summary>Il primo istante del giorno.</summary>
    public DateTimeOffset StartOf(DateOnly day)
    {
        var wall = day.ToDateTime(ResetTime);

        // Ora legale in avanti: l'orario di reset non esiste sull'orologio di parete (es. reset 02:00
        // il giorno in cui si salta da 02:00 a 03:00). Il giorno comincia all'istante della
        // transizione, cioè al primo orario di parete che esiste dopo il buco.
        while (TimeZone.IsInvalidTime(wall)) wall = wall.AddMinutes(1);

        // Ora legale all'indietro: l'orario di reset capita due volte. Vale la prima passata
        // (offset maggiore), così entrambe le occorrenze appartengono al giorno che comincia.
        var offset = TimeZone.IsAmbiguousTime(wall)
            ? TimeZone.GetAmbiguousTimeOffsets(wall).Max()
            : TimeZone.GetUtcOffset(wall);

        return new DateTimeOffset(wall, offset);
    }

    /// <summary>Mezzogiorno locale del giorno: l'istante a cui si stampigliano le registrazioni
    /// retroattive (spec R9 §5). Sta dentro il giorno per qualunque reset plausibile (00:00-06:00) e
    /// regge il cambio d'ora — le transizioni sono di notte, quindi il mezzogiorno dell'orologio
    /// esiste sempre e non serve il trattamento degli orari invalidi di <see cref="StartOf"/>.</summary>
    public DateTimeOffset NoonOf(DateOnly day)
    {
        var wall = day.ToDateTime(new TimeOnly(12, 0));
        return new DateTimeOffset(wall, TimeZone.GetUtcOffset(wall));
    }

    /// <summary>La finestra semiaperta [inizio, fine) del giorno — per i filtri di query, che devono
    /// pescare esattamente ciò che <see cref="DayOf(long)"/> poi bucketizza in quel giorno.</summary>
    public (DateTimeOffset From, DateTimeOffset ToExclusive) RangeOf(DateOnly day)
        => (StartOf(day), StartOf(day.AddDays(1)));

    /// <summary>La finestra semiaperta che copre da <paramref name="fromDay"/> a
    /// <paramref name="toDayInclusive"/> compreso.</summary>
    public (DateTimeOffset From, DateTimeOffset ToExclusive) RangeOf(DateOnly fromDay, DateOnly toDayInclusive)
        => (StartOf(fromDay), StartOf(toDayInclusive.AddDays(1)));
}
```

- [ ] **Step 5: Eseguire i test e verificare che passino**

```
dotnet test tests/Recordum.Core.Tests -p:UseAppHost=false --filter FullyQualifiedName~DayBoundaryTests
```
Atteso: tutti verdi.

Se `A_reset_inside_the_skipped_hour...` fallisce, il colpevole è quasi sempre `GetUtcOffset` su un orario invalido: il ciclo `IsInvalidTime` **deve** girare prima di leggere l'offset.

- [ ] **Step 6: Provare i test contro il codice rotto**

Obbligatorio (CLAUDE.md §8). Uno alla volta, ripristinando ogni volta:
1. In `DayOf`, togliere `- ResetTime.ToTimeSpan()` → devono fallire i casi reset 04:00 e `RangeOf_and_DayOf_agree`.
2. In `StartOf`, togliere il ciclo `IsInvalidTime` → deve fallire `A_reset_inside_the_skipped_hour` (o lanciare).
3. In `StartOf`, sostituire `.Max()` con `.Min()` sugli offset ambigui → deve fallire `A_reset_inside_the_repeated_hour`.
4. In `DayOf`, sostituire `TimeZoneInfo.ConvertTime(instant, TimeZone)` con `instant.UtcDateTime` → deve fallire `Half_past_midnight_local...`.
5. In `NoonOf`, sostituire `new TimeOnly(12, 0)` con `TimeOnly.MinValue` (la mezzanotte di prima) → deve fallire `Noon_always_belongs_to_the_day_it_names` per i reset 04:00 e 06:00. È il difetto esatto che il Task 10 chiude.

Se una di queste quattro rotture **non** produce rosso, il test corrispondente non misura ciò che dice: sistemarlo prima di andare avanti.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.Core/Time/DayBoundary.cs tests/Recordum.Core.Tests/Time/
git commit -m "feat(day): DayBoundary — il giorno di appartenenza come funzione pura"
```

---

### Task 2: `IResetTimeResolver` produce confini (uno, in batch, e per l'app)

La cascata esiste già e funziona; le manca solo di restituire qualcosa di utile. La **versione batch** non è un lusso: senza, l'albero Home risolve N abitudini con N cascate e mangia il budget di 500 ms (spec §3.2, rischio §8.4).

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Services/IResetTimeResolver.cs`
- Modify: `src/Recordum.Data/Services/ResetTimeResolver.cs`
- Create: `tests/Recordum.Data.Tests/Services/ResetTimeResolverTests.cs`

**Interfaces:**
- Consumes: `DayBoundary` (Task 1), `IClock.LocalTimeZone`, `IAppSettingService.GetAsync("daily_reset_time")`.
- Produces:
  - `ValueTask<DayBoundary> GetDayBoundaryAsync(Guid habitNodeId, CancellationToken ct = default)`
  - `ValueTask<IReadOnlyDictionary<Guid, DayBoundary>> GetDayBoundariesAsync(IReadOnlyCollection<Guid> habitNodeIds, CancellationToken ct = default)`
  - `ValueTask<DayBoundary> GetAppDayBoundaryAsync(CancellationToken ct = default)` — nessuna abitudine di riferimento (dialog di export, seeder demo)
  - `ResetTimeResolver` guadagna `IClock clock` nel costruttore primario

- [ ] **Step 1: Scrivere i test che falliscono**

`tests/Recordum.Data.Tests/Services/ResetTimeResolverTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Services;

public class ResetTimeResolverTests
{
    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static (DbFixture fx, ResetTimeResolver resolver) Build()
    {
        var fx = new DbFixture(new FakeClock(
            new DateTimeOffset(2026, 8, 19, 0, 30, 0, TimeSpan.Zero), Plus2));
        var settings = new AppSettingService(fx.Factory, fx.Clock, NullLogger<AppSettingService>.Instance);
        var resolver = new ResetTimeResolver(
            fx.Factory, settings, fx.Clock, NullLogger<ResetTimeResolver>.Instance);
        return (fx, resolver);
    }

    private static async Task<Guid> AddNodeAsync(
        DbFixture fx, Guid? parentId, TimeOnly? resetTime, bool deleted = false)
    {
        var id = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode
        {
            Id = id,
            ParentId = parentId,
            NodeType = parentId is null ? NodeType.Group : NodeType.Habit,
            HabitType = parentId is null ? null : HabitType.Action,
            Name = "n",
            ResetTime = resetTime,
            DeletedAt = deleted ? new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) : null,
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Boundary_carries_the_clock_timezone_and_the_effective_reset()
    {
        var (fx, resolver) = Build();
        await using var _ = fx;
        var group = await AddNodeAsync(fx, null, new TimeOnly(4, 0));
        var habit = await AddNodeAsync(fx, group, null);   // eredita dal gruppo

        var b = await resolver.GetDayBoundaryAsync(habit);

        b.TimeZone.Should().Be(Plus2);
        b.ResetTime.Should().Be(new TimeOnly(4, 0));
        // 00:30Z = 02:30 locali: con reset 04:00 la registrazione appartiene a ieri.
        b.Today(fx.Clock).Should().Be(new DateOnly(2026, 8, 18));
    }

    [Fact]
    public async Task Without_any_reset_the_boundary_is_local_midnight()
    {
        var (fx, resolver) = Build();
        await using var _ = fx;
        var habit = await AddNodeAsync(fx, null, null);

        var b = await resolver.GetDayBoundaryAsync(habit);

        b.ResetTime.Should().Be(TimeOnly.MinValue);
        // 00:30Z = 02:30 locali del 19: senza reset è già il 19, mentre in UTC era ancora il 19
        // solo per caso — il test che discrimina è quello sopra.
        b.Today(fx.Clock).Should().Be(new DateOnly(2026, 8, 19));
    }

    [Fact]
    public async Task Batch_agrees_with_the_single_resolution_for_every_habit()
    {
        var (fx, resolver) = Build();
        await using var _ = fx;
        var group = await AddNodeAsync(fx, null, new TimeOnly(5, 0));
        var inherits = await AddNodeAsync(fx, group, null);
        var overrides = await AddNodeAsync(fx, group, new TimeOnly(3, 0));
        var orphan = await AddNodeAsync(fx, null, null);

        var batch = await resolver.GetDayBoundariesAsync([inherits, overrides, orphan]);

        batch[inherits].Should().Be(await resolver.GetDayBoundaryAsync(inherits));
        batch[overrides].ResetTime.Should().Be(new TimeOnly(3, 0));
        batch[orphan].ResetTime.Should().Be(TimeOnly.MinValue);
    }

    [Fact]
    public async Task Batch_resolves_deleted_nodes_too()
    {
        // L'albero Cestino passa da qui: un nodo soft-deleted deve avere il suo confine,
        // o la sua riga perde strisce e cifra chiave.
        var (fx, resolver) = Build();
        await using var _ = fx;
        var group = await AddNodeAsync(fx, null, new TimeOnly(6, 0));
        var trashed = await AddNodeAsync(fx, group, null, deleted: true);

        var batch = await resolver.GetDayBoundariesAsync([trashed]);

        batch[trashed].ResetTime.Should().Be(new TimeOnly(6, 0));
    }

    [Fact]
    public async Task Unknown_id_falls_back_to_the_app_default()
    {
        var (fx, resolver) = Build();
        await using var _ = fx;
        var settings = new AppSettingService(fx.Factory, fx.Clock, NullLogger<AppSettingService>.Instance);
        await settings.SetAsync("daily_reset_time", "07:00");

        var single = await resolver.GetDayBoundaryAsync(Guid.NewGuid());
        var batch = await resolver.GetDayBoundariesAsync([Guid.NewGuid()]);
        var app = await resolver.GetAppDayBoundaryAsync();

        single.ResetTime.Should().Be(new TimeOnly(7, 0));
        batch.Values.Single().ResetTime.Should().Be(new TimeOnly(7, 0));
        app.ResetTime.Should().Be(new TimeOnly(7, 0));
    }

    [Fact]
    public async Task A_cycle_in_the_tree_does_not_hang_the_batch()
    {
        // Impossibile via UI (Move valida), ma un DB importato può portarlo: la versione singola
        // ha già un guard, la batch deve averlo uguale o l'albero Home si pianta.
        var (fx, resolver) = Build();
        await using var _ = fx;
        var a = await AddNodeAsync(fx, null, null);
        var b = await AddNodeAsync(fx, a, null);
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var na = await db.HabitNodes.FindAsync(a);
            na!.ParentId = b;
            await db.SaveChangesAsync();
        }

        var batch = await resolver.GetDayBoundariesAsync([a, b]);

        batch[a].ResetTime.Should().Be(TimeOnly.MinValue);
        batch[b].ResetTime.Should().Be(TimeOnly.MinValue);
    }

    [Fact]
    public async Task Batch_reads_the_tree_once_regardless_of_how_many_habits()
    {
        // Il vincolo di perf della spec §3.2, misurato dove si può misurare: il numero di query.
        var (fx, resolver) = Build();
        await using var _ = fx;
        var group = await AddNodeAsync(fx, null, new TimeOnly(4, 0));
        var ids = new List<Guid>();
        for (var i = 0; i < 50; i++) ids.Add(await AddNodeAsync(fx, group, null));

        using var counter = fx.CountCommands();
        _ = await resolver.GetDayBoundariesAsync(ids);

        counter.Count.Should().BeLessThanOrEqualTo(2,
            "il batch legge l'albero una volta e l'AppSetting una volta, non una cascata per abitudine");
    }
}
```

`fx.CountCommands()` non esiste ancora: aggiungerlo a `DbFixture` nello step 2.

- [ ] **Step 2: Aggiungere il contatore di comandi al `DbFixture`**

In `tests/Recordum.Data.Tests/Fixtures/DbFixture.cs`, un interceptor che conta i `DbCommand` eseguiti. Serve solo qui, ma è l'unico modo onesto di provare "una passata, non N".

```csharp
// dentro DbFixture
private readonly CommandCounterInterceptor _counter = new();

/// <summary>Conta i comandi SQL eseguiti finché non viene disposto. Per i test che devono
/// dimostrare "una passata sola", non "abbastanza veloce".</summary>
public CommandCountScope CountCommands() => new(_counter);

public sealed class CommandCountScope(CommandCounterInterceptor counter) : IDisposable
{
    private readonly int _start = counter.Count;
    public int Count => counter.Count - _start;
    public void Dispose() { }
}

public sealed class CommandCounterInterceptor : DbCommandInterceptor
{
    private int _count;
    public int Count => Volatile.Read(ref _count);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
```

Registrarlo nella `FixtureFactory` insieme agli altri interceptor (`b.AddInterceptors(..., counter)`), e passarlo dal costruttore. Servono `using System.Data.Common;` e `using Microsoft.EntityFrameworkCore.Diagnostics;`.

- [ ] **Step 3: Eseguire i test e verificare che falliscano**

```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter FullyQualifiedName~ResetTimeResolverTests
```
Atteso: non compila — i tre metodi non esistono e il costruttore non prende `IClock`.

- [ ] **Step 4: Estendere l'interfaccia**

`src/Recordum.Core/Abstractions/Services/IResetTimeResolver.cs`:

```csharp
using Recordum.Core.Time;

namespace Recordum.Core.Abstractions.Services;

/// <summary>
/// Risolve il confine di giornata di un'abitudine: cascata <c>reset_time</c> (habit → gruppi antenati →
/// AppSetting <c>daily_reset_time</c> → <c>00:00</c>) combinata col fuso locale dell'<see cref="IClock"/>.
/// <para>È il <b>produttore</b> di <see cref="DayBoundary"/>: si chiama una volta in cima a un'operazione,
/// mai dentro un ciclo. Per più abitudini insieme esiste <see cref="GetDayBoundariesAsync"/>, che legge
/// l'albero una volta invece di ripetere la cascata per ognuna.</para>
/// </summary>
public interface IResetTimeResolver
{
    ValueTask<TimeOnly> GetEffectiveResetTimeAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Il confine di giornata di una singola abitudine.</summary>
    ValueTask<DayBoundary> GetDayBoundaryAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>I confini di più abitudini in una passata. Ogni id richiesto compare nel risultato:
    /// un id sconosciuto riceve il confine di default dell'app. Include i nodi soft-deleted
    /// (l'albero Cestino passa da qui).</summary>
    ValueTask<IReadOnlyDictionary<Guid, DayBoundary>> GetDayBoundariesAsync(
        IReadOnlyCollection<Guid> habitNodeIds, CancellationToken ct = default);

    /// <summary>Il confine dell'app, senza abitudine di riferimento (default di un dialog, seeder).</summary>
    ValueTask<DayBoundary> GetAppDayBoundaryAsync(CancellationToken ct = default);
}
```

- [ ] **Step 5: Implementare nel resolver**

`src/Recordum.Data/Services/ResetTimeResolver.cs` — aggiungere `IClock clock` al costruttore primario (dopo `settings`, prima del logger) e i tre metodi. La cascata esistente resta com'è.

```csharp
public async ValueTask<DayBoundary> GetDayBoundaryAsync(Guid habitNodeId, CancellationToken ct = default)
    => new(clock.LocalTimeZone, await GetEffectiveResetTimeAsync(habitNodeId, ct).ConfigureAwait(false));

public async ValueTask<DayBoundary> GetAppDayBoundaryAsync(CancellationToken ct = default)
    => new(clock.LocalTimeZone, await GetAppResetAsync(ct).ConfigureAwait(false));

public async ValueTask<IReadOnlyDictionary<Guid, DayBoundary>> GetDayBoundariesAsync(
    IReadOnlyCollection<Guid> habitNodeIds, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(habitNodeIds);
    var appReset = await GetAppResetAsync(ct).ConfigureAwait(false);
    var tz = clock.LocalTimeZone;
    if (habitNodeIds.Count == 0) return new Dictionary<Guid, DayBoundary>();

    // Una lettura sola dell'albero: i nodi sono centinaia, non milioni, e la cascata è una
    // passeggiata in memoria. IgnoreQueryFilters perché l'albero Cestino chiede i confini
    // dei nodi soft-deleted.
    await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
    var nodes = await db.HabitNodes.AsNoTracking().IgnoreQueryFilters()
        .Select(n => new { n.Id, n.ParentId, n.ResetTime })
        .ToDictionaryAsync(n => n.Id, ct).ConfigureAwait(false);

    var result = new Dictionary<Guid, DayBoundary>(habitNodeIds.Count);
    foreach (var id in habitNodeIds)
    {
        if (result.ContainsKey(id)) continue;
        result[id] = new DayBoundary(tz, WalkCascade(id, nodes, appReset, logDepth: () => LogDepthExceeded(id)));
    }
    return result;
}

private static TimeOnly WalkCascade(
    Guid startId,
    IReadOnlyDictionary<Guid, NodeCascadeRow> nodes,   // vedi nota sul tipo anonimo
    TimeOnly appReset,
    Action logDepth)
{
    var seen = new HashSet<Guid>();
    var depth = 0;
    var currentId = (Guid?)startId;
    while (currentId is { } id && nodes.TryGetValue(id, out var node))
    {
        if (node.ResetTime is { } t) return t;
        if (!seen.Add(id) || ++depth > MaxDepth) { logDepth(); break; }
        currentId = node.ParentId;
    }
    return appReset;
}

private async ValueTask<TimeOnly> GetAppResetAsync(CancellationToken ct)
{
    var raw = await settings.GetAsync("daily_reset_time", ct).ConfigureAwait(false);
    return raw is not null
        && TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
        ? parsed
        : DefaultReset;
}
```

Note d'implementazione:
- Il tipo anonimo della proiezione non attraversa un confine di metodo: dichiarare un piccolo `private readonly record struct NodeCascadeRow(Guid Id, Guid? ParentId, TimeOnly? ResetTime)` e proiettare su quello.
- Rifattorizzare anche `GetEffectiveResetTimeAsync` perché usi `GetAppResetAsync` invece di ripetere il parsing: due copie della stessa lettura divergono al primo cambio di formato.
- `LogDepthExceeded` esiste già.

- [ ] **Step 6: Registrare la nuova dipendenza**

`src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs:112` registra già `IResetTimeResolver`: l'`IClock` è nel container, quindi non serve toccare la registrazione. Verificare solo che il costruttore risolva (lo prova lo smoke test del container, se c'è; altrimenti lo prova l'avvio del Task 11).

- [ ] **Step 7: Eseguire i test e verificare che passino**

```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter FullyQualifiedName~ResetTimeResolverTests
```

Poi la suite Data intera, per intercettare i costruttori rotti:
```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false
```

- [ ] **Step 8: Provare i test contro il codice rotto**

1. Nella batch, sostituire la lettura d'albero con una chiamata a `GetEffectiveResetTimeAsync` per ogni id → `Batch_reads_the_tree_once...` deve diventare rosso (e solo lui).
2. Togliere `IgnoreQueryFilters()` → `Batch_resolves_deleted_nodes_too` deve fallire.
3. Togliere il guard `seen`/`depth` da `WalkCascade` → `A_cycle_in_the_tree_does_not_hang_the_batch` deve andare in loop (fermarlo a mano) o fallire per timeout. Se non succede, il test non prova il ciclo: correggerlo.

- [ ] **Step 9: Commit**

```bash
git add src/Recordum.Core/Abstractions/Services/IResetTimeResolver.cs src/Recordum.Data/Services/ResetTimeResolver.cs tests/Recordum.Data.Tests/
git commit -m "feat(day): il resolver produce DayBoundary, singolo e in batch"
```

---

### Task 3: `CompletionService` — il perno delle letture (A8-A10, B1-B3)

Heatmap, somma giornaliera e magnitudine del calendario sono **la stessa lettura** vista da tre angoli, e ci passano sopra streak, calendario, grafici, export e la fascia "Oggi". Convertirlo qui significa che i chiamanti non cambiano di una riga.

**Files:**
- Modify: `src/Recordum.Data/Services/CompletionService.cs:121-230`
- Modify: `tests/Recordum.Data.Tests/Fixtures/DbFixture.cs` (proprietà `ResetTimes`)
- Modify: i test che costruiscono `new CompletionService(...)` (36 siti in 9 file)
- Modify: `tests/Recordum.Data.Tests/Integration/CompletionServiceTests.cs` (+3 test nuovi)

**Interfaces:**
- Consumes: `IResetTimeResolver.GetDayBoundaryAsync` (Task 2), `DayBoundary.RangeOf` / `DayOf(long)` (Task 1).
- Produces: `CompletionService` con un parametro in più nel costruttore primario — `IResetTimeResolver resetTimes`, **ultimo**, dopo `IEventBus eventBus`. Firme di `ICompletionService` invariate.

- [ ] **Step 1: Aggiungere `ResetTimes` al `DbFixture`**

Così i 36 siti di costruzione hanno un argomento da passare che è una riga sola.

```csharp
// in DbFixture, dopo Factory
private IResetTimeResolver? _resetTimes;

/// <summary>Il resolver reale sul DB della fixture: i servizi che risolvono il confine di giornata
/// lo prendono da qui. Con FakeClock a fuso UTC e nessun reset configurato, il confine è l'identità
/// (mezzanotte UTC) — ed è perché i test scritti prima di R9 restano veri.</summary>
public IResetTimeResolver ResetTimes => _resetTimes ??= new ResetTimeResolver(
    Factory,
    new AppSettingService(Factory, Clock, NullLogger<AppSettingService>.Instance),
    Clock,
    NullLogger<ResetTimeResolver>.Instance);
```

- [ ] **Step 2: Scrivere i test che falliscono**

In `tests/Recordum.Data.Tests/Integration/CompletionServiceTests.cs`:

```csharp
private static readonly TimeZoneInfo Plus2 =
    TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

private static async Task<(DbFixture fx, CompletionService svc, Guid habitId)> SeedLocalAsync(
    DateTimeOffset now, TimeOnly? reset = null)
{
    var fx = new DbFixture(new FakeClock(now, Plus2));
    var habitId = Guid.NewGuid();
    await using (var db = await fx.Factory.CreateDbContextAsync())
    {
        db.HabitNodes.Add(new HabitNode
        {
            Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Action,
            Name = "h", ResetTime = reset,
        });
        await db.SaveChangesAsync();
    }
    var svc = new CompletionService(
        fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance,
        new RecordingEventBus(), fx.ResetTimes);
    return (fx, svc, habitId);
}

[Fact]
public async Task Heatmap_buckets_a_completion_by_its_local_day()
{
    // 2026-08-18T22:30Z = 19 agosto 00:30 in un fuso +2: è del 19, non del 18.
    var (fx, svc, habitId) = await SeedLocalAsync(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));
    await using var _ = fx;
    var at = new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero);
    await svc.RecordAsync(new Completion
    {
        HabitNodeId = habitId, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
    });

    var cells = await svc.GetHeatmapAsync(habitId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 20));

    cells.Should().ContainSingle().Which.Date.Should().Be(new DateOnly(2026, 8, 19));
}

[Fact]
public async Task Heatmap_window_includes_a_completion_at_the_very_edge_of_the_local_day()
{
    // Il bucket giusto non basta: la finestra deve pescarlo. Con range = [17, 17] e reset 00:00,
    // una registrazione delle 22:10Z del 16 (= 17 alle 00:10 locali) deve entrare.
    var (fx, svc, habitId) = await SeedLocalAsync(new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero));
    await using var _ = fx;
    var at = new DateTimeOffset(2026, 8, 16, 22, 10, 0, TimeSpan.Zero);
    await svc.RecordAsync(new Completion
    {
        HabitNodeId = habitId, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
    });

    var cells = await svc.GetHeatmapAsync(habitId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 17));

    cells.Should().ContainSingle().Which.Date.Should().Be(new DateOnly(2026, 8, 17));
}

[Fact]
public async Task A_reset_time_moves_the_completion_to_the_previous_day()
{
    // Stesso istante, reset 04:00: le 02:30 locali del 19 appartengono al 18.
    var (fx, svc, habitId) = await SeedLocalAsync(
        new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero), new TimeOnly(4, 0));
    await using var _ = fx;
    var at = new DateTimeOffset(2026, 8, 19, 0, 30, 0, TimeSpan.Zero);
    await svc.RecordAsync(new Completion
    {
        HabitNodeId = habitId, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
    });

    var cells = await svc.GetHeatmapAsync(habitId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 20));

    cells.Should().ContainSingle().Which.Date.Should().Be(new DateOnly(2026, 8, 18));
}

[Fact]
public async Task Daily_magnitude_uses_the_same_day_as_the_heatmap()
{
    var (fx, svc, habitId) = await SeedLocalAsync(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));
    await using var _ = fx;
    var at = new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero);
    await svc.RecordAsync(new Completion
    {
        HabitNodeId = habitId, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
    });

    var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 8);

    mags.Should().ContainSingle().Which.Date.Should().Be(new DateOnly(2026, 8, 19));
}
```

- [ ] **Step 3: Eseguire i test e verificare che falliscano**

```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter FullyQualifiedName~CompletionServiceTests
```
Atteso: non compila (il costruttore non prende ancora il resolver). Aggiungere il parametro (step 4) e rieseguire: i quattro test nuovi devono fallire con la data sbagliata (`18` invece di `19`), **non** con un errore di compilazione — è quel rosso che dimostra il difetto.

- [ ] **Step 4: Convertire il servizio**

Costruttore:
```csharp
public sealed partial class CompletionService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<CompletionService> logger,
    IEventBus eventBus,
    IResetTimeResolver resetTimes) : ICompletionService
```

`GetHeatmapAsync` (A8, B1):
```csharp
var day = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
var (from, toExclusive) = day.RangeOf(fromDate, toDate);
var fromMs = from.ToUnixTimeMilliseconds();
var toMs = toExclusive.ToUnixTimeMilliseconds();
await using var db = await dbFactory.CreateDbContextAsync(ct);
var rows = await db.Completions.AsNoTracking()
    .Where(c => c.HabitNodeId == habitNodeId && c.CompletedAtMs >= fromMs && c.CompletedAtMs < toMs)
    .Select(c => new { c.CompletedAtMs, c.IsRetroactive })
    .ToListAsync(ct);
return rows
    .GroupBy(r => day.DayOf(r.CompletedAtMs))
    .Select(g => new HeatmapCell(g.Key, g.Count(), g.Count(x => x.IsRetroactive)))
    .OrderBy(c => c.Date)
    .ToList();
```

Tre cose da notare, e sono le stesse in tutti i task che seguono:
1. **`< toMs`, non `<= toMs`**: `RangeOf` restituisce una finestra semiaperta. Il vecchio `TimeOnly.MaxValue` con `<=` era un modo di dire "fine giornata" che perdeva l'ultimo millisecondo; adesso il confine è esatto.
2. **la proiezione prende `CompletedAtMs`, non `CompletedAt`**: vedi Global Constraints. Dove `CompletedAt` non serve ad altro, toglierlo dalla `Select`.
3. **`GroupBy` su `day.DayOf(...)`**: in memoria, dopo `ToListAsync`. Non provare a tradurlo in SQL.

`GetDailySumAsync` (A9, B2): identica sostituzione, proiettando `v.Completion.CompletedAtMs` al posto di `v.Completion.CompletedAt`.

`GetDailyMagnitudeAsync` (A10, B3): il range è il mese *di quel confine*, non il mese civile UTC:
```csharp
var day = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
var first = new DateOnly(year, month, 1);
var last = first.AddMonths(1).AddDays(-1);
var (from, toExclusive) = day.RangeOf(first, last);
var fromMs = from.ToUnixTimeMilliseconds();
var toMs = toExclusive.ToUnixTimeMilliseconds();
...
foreach (var g in comps.GroupBy(c => day.DayOf(c.CompletedAtMs)))
```
La proiezione di questo metodo usa `CompletedAtMs` sia per il bucket sia per l'ordinamento last-wins: `c.CompletedAt` esce del tutto.

`ListByHabitAsync` **non si tocca**: prende già istanti (`DateTimeOffset?`), non giorni. Sono i suoi chiamanti a doverli calcolare con `RangeOf` (Task 8, 9).

- [ ] **Step 5: Aggiornare i 36 siti di costruzione**

Il compilatore li elenca tutti. La forma più comune è su una riga:
```bash
grep -rln "new CompletionService(" tests/ | xargs sed -i 's/new RecordingEventBus())/new RecordingEventBus(), fx.ResetTimes)/g'
```
**Attenzione**: quel `sed` è giusto solo dove la variabile della fixture si chiama `fx` e l'ultimo argomento è `new RecordingEventBus()`. Compilare subito dopo e sistemare a mano i residui — sono pochi e il compilatore li nomina.

```
dotnet build tests/Recordum.Data.Tests -p:UseAppHost=false
```

- [ ] **Step 6: Eseguire i test**

```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false
```
Atteso: i quattro nuovi verdi, **e tutti i preesistenti ancora verdi** — girano con `FakeClock` a fuso UTC e senza reset, dove `DayBoundary` è l'identità. Se qualcuno di loro si sposta, non è "rumore da aggiornare": è un caso in cui il vecchio comportamento dipendeva davvero da UTC. Leggerlo prima di cambiarlo.

- [ ] **Step 7: Provare i test contro il codice rotto**

Rimettere `GroupBy(r => DateOnly.FromDateTime(r.CompletedAt))` (e la proiezione che serve) in `GetHeatmapAsync`: `Heatmap_buckets_a_completion_by_its_local_day` deve diventare rosso. Poi rimettere il solo range a mezzanotte UTC (bucket nuovo, finestra vecchia): deve diventare rosso `Heatmap_window_includes_a_completion_at_the_very_edge_of_the_local_day` — è la prova che le due metà servono entrambe. Ripristinare.

- [ ] **Step 8: Commit**

```bash
git add src/Recordum.Data/Services/CompletionService.cs tests/Recordum.Data.Tests/
git commit -m "fix(day): heatmap, somme e magnitudine bucketizzano per giorno locale (A8-A10, B1-B3)"
```

---

### Task 4: Streak e astinenza (A1-A7, B4-B5)

Il motore streak è il consumatore che l'utente sente di più: è lì che un giorno spostato diventa "ho perso la serie". Qui dentro convivono tre derivazioni diverse — completamenti, periodi di astinenza, e la data di creazione dell'abitudine che fa da gate a schedule e settimana.

**Files:**
- Modify: `src/Recordum.Data/Services/Aggregators/SchedulingAwareStreakAggregator.cs`
- Modify: `src/Recordum.Data/Services/Aggregators/HabitConfigTimeline.cs`
- Modify: `src/Recordum.Data/Services/AbstinenceService.cs:65-75`
- Modify: `tests/Recordum.Data.Tests/Integration/Aggregators/{SchedulingAwareStreakAggregatorTests,AbstinenceStreakTests,HabitConfigTimelineTests}.cs`
- Modify: `tests/Recordum.Data.Tests/Perf/StreakAggregatorPerfTests.cs` (costruttore)

**Interfaces:**
- Consumes: `IResetTimeResolver.GetDayBoundaryAsync`, `DayBoundary.{DayOf(long), DayOf(DateTime), Today, RangeOf}`.
- Produces:
  - `SchedulingAwareStreakAggregator` con `IResetTimeResolver resetTimes` in coda al costruttore (dopo `IClock clock`)
  - `AbstinenceService` con `IResetTimeResolver resetTimes` in coda al costruttore
  - `HabitConfigTimeline(HabitConfig current, IReadOnlyList<HabitConfigHistory> history, DayBoundary day)` — terzo parametro obbligatorio

- [ ] **Step 1: Scrivere i test che falliscono**

In `SchedulingAwareStreakAggregatorTests`, un test che misura il confine invece di assumerlo:

```csharp
private static readonly TimeZoneInfo Plus2 =
    TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

[Fact]
public async Task A_completion_just_after_local_midnight_counts_for_the_new_day()
{
    // Habit giornaliero creato il 17. Registrazione unica alle 22:30Z del 18 = 19 alle 00:30 locali.
    // In UTC quella registrazione cadeva il 18 e il 19 risultava mancato: la serie si spezzava.
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 6, 0, 0, TimeSpan.Zero), Plus2);
    await using var fx = new DbFixture(clock);
    var habitId = await SeedDailyHabitAsync(fx, createdUtc: new DateTimeOffset(2026, 8, 17, 6, 0, 0, TimeSpan.Zero));
    await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero));

    var runs = await BuildAggregator(fx, ...).GetHistoryAsync(
        habitId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 19));

    runs.Should().ContainSingle().Which.End.Should().Be(new DateOnly(2026, 8, 19));
}

[Fact]
public async Task With_a_04_00_reset_a_late_night_completion_still_counts_for_the_day_before()
{
    // Chi si corica tardi: registra all'01:30 locale del 19 con reset 04:00 → è ancora il 18.
    // È il caso d'uso che rende reset_time una promessa, non una decorazione.
    ...
    runs.Should().ContainSingle().Which.End.Should().Be(new DateOnly(2026, 8, 18));
}
```

In `AbstinenceStreakTests`, il gemello sui periodi:

```csharp
[Fact]
public async Task An_abstinence_period_started_after_local_midnight_starts_on_the_new_day()
{
    // StartedAtMs = 2026-08-18T22:30Z → il periodo comincia il 19 locale, non il 18.
    ...
    runs.Should().ContainSingle().Which.Start.Should().Be(new DateOnly(2026, 8, 19));
}
```

In `HabitConfigTimelineTests`, la conseguenza di B5:

```csharp
[Fact]
public async Task The_config_of_a_day_is_resolved_at_the_end_of_that_local_day()
{
    // Una modifica di config fatta alle 23:30 locali del 18 (21:30Z) vale già per il 18,
    // e la vecchia config vale fino alla sua fine locale — non fino alle 23:59:59 UTC.
    ...
}
```

> I tre test sopra hanno il corpo abbreviato dove ripete l'impalcatura già presente nel file (`SeedAsync`, `BuildAggregator`): riusare quella, non riscriverla. Ciò che **non** va abbreviato è l'assert: deve nominare il giorno atteso a mano, mai calcolarlo con la stessa formula che il codice usa — un assert che ricalcola non prova niente.

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter "FullyQualifiedName~StreakAggregator|FullyQualifiedName~AbstinenceStreak|FullyQualifiedName~HabitConfigTimeline"
```
Atteso: rossi con il giorno sbagliato (18 invece di 19, serie spezzata invece che unica).

- [ ] **Step 3: Convertire l'aggregatore**

In cima a `GetHistoryAsync`, subito dopo aver caricato `habitNode`:
```csharp
var day = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
```
e passarlo a `ComputeAbstinenceAsync`. Poi, riga per riga:

| era | diventa |
|---|---|
| `DateOnly.FromDateTime(habitNode.CreatedAt)` (A1, A4) | `day.DayOf(habitNode.CreatedAt)` |
| `DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)` (A2, A5) | `day.Today(clock)` |
| `DateOnly.FromDateTime(brokenAt)` (A3) | `day.DayOf(brokenAt)` |
| `DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(p.StartedAtMs).UtcDateTime)` (A6) | `day.DayOf(p.StartedAtMs)` |
| idem su `EndedAtMs` (A7) | `day.DayOf(p.EndedAtMs!.Value)` |
| `new HabitConfigTimeline(cfg, history)` | `new HabitConfigTimeline(cfg, history, day)` |

- [ ] **Step 4: Convertire `HabitConfigTimeline` (B5)**

```csharp
internal sealed class HabitConfigTimeline
{
    private readonly HabitConfig _current;
    private readonly HabitConfigHistory[] _history;
    private readonly DayBoundary _day;

    public HabitConfigTimeline(HabitConfig current, IReadOnlyList<HabitConfigHistory> history, DayBoundary day)
    {
        _current = current;
        _history = history.OrderBy(h => h.ValidFrom).ToArray();
        _day = day;
    }

    public HabitConfig ResolveFor(DateOnly date)
    {
        // L'ultimo istante del giorno secondo il confine dell'abitudine, non le 23:59:59 UTC:
        // la config che vale per un giorno è quella in vigore quando quel giorno finisce.
        var dt = _day.RangeOf(date).ToExclusive.UtcDateTime.AddTicks(-1);
        ...
    }
}
```
`ValidFrom` / `ValidUntil` sono `DateTime` UTC: il confronto resta fra `DateTime` UTC, cambia solo *quale* istante rappresenta "la fine del giorno D".

- [ ] **Step 5: Convertire `AbstinenceService.ListPeriodsAsync` (B4)**

```csharp
var day = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
var (fromInstant, toExclusive) = day.RangeOf(from, to);
var fromMs = fromInstant.ToUnixTimeMilliseconds();
var toMs = toExclusive.ToUnixTimeMilliseconds();
```
Il predicato di intersezione resta quello che c'è; cambiano solo i due estremi. Se usa `<= toMs`, portarlo a `< toMs` coerentemente con la finestra semiaperta.

- [ ] **Step 6: Aggiornare i siti di costruzione**

`new SchedulingAwareStreakAggregator(...)` e `new AbstinenceService(...)` nei test e in `ServiceRegistrar`. Il compilatore li elenca; l'argomento nuovo è `fx.ResetTimes` nei test.

- [ ] **Step 7: Eseguire i test**

```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false
```
Atteso: i nuovi verdi, i vecchi invariati (fuso UTC = identità).

- [ ] **Step 8: Provare i test contro il codice rotto**

Rimettere `DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)` al posto di `day.Today(clock)` in `GetHistoryAsync`: deve fallire `A_completion_just_after_local_midnight...`. Poi rimettere il solo bucket UTC in `ComputeAbstinenceAsync`: deve fallire il test dei periodi. Ripristinare.

- [ ] **Step 9: Commit**

```bash
git add src/Recordum.Data/Services/ tests/Recordum.Data.Tests/
git commit -m "fix(day): streak, astinenza e timeline config leggono il giorno locale (A1-A7, B4-B5)"
```

---

### Task 5: Andamento, distribuzione oraria e grafici (A13, A17, B6-B7)

**Files:**
- Modify: `src/Recordum.Data/Services/Queries/TrendQueryService.cs:11-45`
- Modify: `src/Recordum.Data/Services/Queries/HourlyDistributionQueryService.cs:11-30`
- Modify: `src/Recordum.Data/Services/Queries/ChartQueryService.cs:67-80`
- Modify: i test dei tre servizi + i loro siti di costruzione

**Interfaces:**
- Consumes: `IResetTimeResolver`, `DayBoundary`.
- Produces: i tre servizi con `IResetTimeResolver resetTimes` in coda al costruttore. Firme pubbliche invariate.

- [ ] **Step 1: Scrivere i test che falliscono**

```csharp
[Fact]
public async Task Trend_buckets_a_completion_by_its_local_day()
{
    // Stessa registrazione delle 22:30Z del 18 in un fuso +2: il punto è del 19.
    var points = await trends.GetTrendAsync(
        habitId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 20), TrendGranularity.Day);

    points.Should().ContainSingle().Which.Bucket.Should().Be(new DateOnly(2026, 8, 19));
}

[Fact]
public async Task Hourly_window_follows_the_local_day_not_UTC_midnight()
{
    // La distribuzione oraria converte già l'ora in locale; era la FINESTRA a essere UTC.
    // Chiedendo [19, 19] la registrazione delle 22:30Z del 18 deve comparire, nel bucket 0.
    var hours = await hourly.GetHourlyAsync(habitId, new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 19));

    hours[0].Should().Be(1);
    hours.Sum().Should().Be(1);
}

[Fact]
public async Task Chart_range_30d_ends_on_the_local_today()
{
    // clock = 2026-08-19T00:30Z in fuso +2 → oggi locale è il 19, non il 18 come diceva UTC.
    // Il range "30d" è quindi [21 luglio, 19 agosto]: si misura sul punto più recente della serie,
    // seminando una registrazione a mezzogiorno locale di ogni giorno dal 18 al 19.
    var model = await charts.RenderAsync(chartId, ChartTheme.Light);

    var lastLabel = model.Series.Single().Points.Last().Label;
    lastLabel.Should().Be("2026-08-19");
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

- [ ] **Step 3: Convertire `TrendQueryService`**

```csharp
var day = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
var (from, toExclusive) = day.RangeOf(fromDate, toDate);
var fromMs = from.ToUnixTimeMilliseconds();
var toMs = toExclusive.ToUnixTimeMilliseconds();
...
    .Where(c => c.HabitNodeId == habitNodeId && c.CompletedAtMs >= fromMs && c.CompletedAtMs < toMs)
    .Select(c => new CompletionProjection(c.CompletedAtMs, ...))   // ms, non CompletedAt
...
    .GroupBy(c => BucketKey(day.DayOf(c.CompletedAtMs), granularity))
```
`CompletionProjection` cambia il primo campo da `DateTime` a `long`: è un record interno al file, il compilatore guida.

- [ ] **Step 4: Convertire `HourlyDistributionQueryService`**

Solo la finestra (B6) e la proiezione:
```csharp
var day = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
var (from, toExclusive) = day.RangeOf(fromDate, toDate);
...
    .Select(c => c.CompletedAtMs)
...
foreach (var ms in timestamps)
{
    var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(ms), day.TimeZone);
    buckets[local.Hour]++;
}
```
Il bucket orario resta l'**ora locale reale**, non spostata dal reset: "di solito verso le 20:00" parla dell'orologio, non del giorno di appartenenza. Metterlo come commento nel codice, perché è la domanda che si farà il prossimo lettore.

- [ ] **Step 5: Convertire `ChartQueryService.ResolveRange` (A13)**

`ResolveRange` è sincrono e non conosce l'abitudine (un chart può avere più serie su abitudini diverse). Risolvere il confine **dell'app** una volta in cima al metodo che lo chiama e passarglielo:
```csharp
private static (DateOnly from, DateOnly to) ResolveRange(ChartConfig config, DayBoundary day, IClock clock)
{
    var today = day.Today(clock);
    ...
}
```
con `var day = await resetTimes.GetAppDayBoundaryAsync(ct)` nel chiamante. Usare il confine dell'app e non quello della prima serie è deliberato: il range di un grafico è una scelta di presentazione trasversale, non una proprietà di un'abitudine. Annotarlo nel commento.

- [ ] **Step 6: Eseguire i test, aggiornare i siti di costruzione, rieseguire la suite Data**

- [ ] **Step 7: Provare i test contro il codice rotto**

Rimettere la finestra a mezzanotte UTC in `HourlyDistributionQueryService`: `Hourly_window_follows_the_local_day...` deve fallire (0 invece di 1). Ripristinare.

- [ ] **Step 8: Commit**

```bash
git commit -am "fix(day): andamento, distribuzione oraria e range dei grafici sul giorno locale (A13, A17, B6-B7)"
```

---

### Task 6: Export (A14-A16, B8-B9)

L'export è la superficie dove un giorno sbagliato **esce dal programma** e finisce in un file che l'utente archivia. Ha anche la sua ancora heatmap indipendente, che va allineata a quella del Dettaglio.

**Files:**
- Modify: `src/Recordum.Data/Services/Queries/ExportQueryService.cs:55-250`
- Modify: `tests/Recordum.Data.Tests/**/ExportQueryService*` e i siti di costruzione

**Interfaces:**
- Consumes: `IResetTimeResolver`, `DayBoundary`.
- Produces: `ExportQueryService` con `IResetTimeResolver resetTimes` in coda al costruttore.

- [ ] **Step 1: Scrivere il test che fallisce**

```csharp
[Fact]
public async Task Exported_range_includes_a_completion_recorded_after_local_midnight()
{
    // Export di [2026-08-19, 2026-08-19] in fuso +2: la registrazione delle 22:30Z del 18
    // è del 19 per l'utente e deve comparire nel file.
    var data = await exports.BuildAsync(new ExportRequest(..., From: new DateOnly(2026, 8, 19), To: new DateOnly(2026, 8, 19)));

    data.Habits.Single().Completions.Should().ContainSingle();
}

[Fact]
public async Task Export_summary_agrees_with_the_streak_engine_on_the_same_data()
{
    // Il summary stampa "current"/"longest": devono essere gli stessi numeri che il Dettaglio mostra,
    // non una seconda opinione. Confronto diretto con IStreakAggregator sullo stesso DB.
    ...
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

- [ ] **Step 3: Convertire**

- Righe 62-63 e 163-164 (B8, B9): `var (fromUtc, toExclusive) = day.RangeOf(request.From, request.To);` e portare i predicati a `< toExclusive`. Il confine è quello dell'**abitudine esportata**: se l'export copre più abitudini, risolverlo dentro il ciclo per-abitudine, non una volta per tutte (abitudini diverse possono avere reset diversi — è tutto il punto della cascata).
- Riga 198 (A14): `var today = day.Today(clock);`
- Righe 208-211 (A15, A16): `day.DayOf(p.StartedAtMs)` / `day.DayOf(end)`.

- [ ] **Step 4: Eseguire i test e la suite Data**

- [ ] **Step 5: Provare il test contro il codice rotto**

Rimettere `request.From.ToDateTime(TimeOnly.MinValue)`: `Exported_range_includes_a_completion_recorded_after_local_midnight` deve fallire. Ripristinare.

- [ ] **Step 6: Commit**

```bash
git commit -am "fix(day): l'export ritaglia e bucketizza sul giorno locale (A14-A16, B8-B9)"
```

---

### Task 7: L'albero Home in batch (A18, B10) — e il budget dei 500 ms

Qui il confine non è uno: è uno **per abitudine**. È il posto dove la versione batch del Task 2 esiste per un motivo, e dove la divisione per 86 400 000 va tolta, non aggiustata.

**Files:**
- Modify: `src/Recordum.Data/Services/Queries/HabitTreeQueryService.cs:23-190`
- Modify: `tests/Recordum.Data.Tests/Performance/HomeTreeBudgetTests.cs`
- Modify: i 29 siti di costruzione di `HabitTreeQueryService`
- Create: test di striscia in `tests/Recordum.Data.Tests/Integration/` (o estendere quello esistente sull'albero)

**Interfaces:**
- Consumes: `IResetTimeResolver.GetDayBoundariesAsync`, `DayBoundary.{Today, RangeOf, DayOf}`.
- Produces: `HabitTreeQueryService(IDbContextFactory<RecordumDbContext>, IClock, IResetTimeResolver)`.

- [ ] **Step 1: Scrivere i test che falliscono**

```csharp
[Fact]
public async Task Two_habits_with_different_resets_get_different_strips_from_the_same_completion_time()
{
    // Stessa registrazione (22:30Z), due abitudini: una senza reset (→ oggi), una con reset 04:00
    // (→ ieri). Se la striscia le trattasse uguale, il confine per-abitudine sarebbe finto.
    var rows = await tree.GetHomeTreeAsync();

    rows.Single(r => r.Id == plain).Heatmap14.Last().Should().Be(1);
    rows.Single(r => r.Id == lateReset).Heatmap14.Last().Should().Be(0);
    rows.Single(r => r.Id == lateReset).Heatmap14[^2].Should().Be(1);
}

[Fact]
public async Task The_strip_does_not_shift_across_a_daylight_saving_change()
{
    // Fuso con ora legale, finestra che contiene il 29 marzo 2026 (giorno di 23 ore):
    // una registrazione a mezzogiorno locale di ogni giorno deve dare 14 quadretti pieni,
    // non 13 pieni e uno spostato. È il difetto che la divisione per 86.400.000 introduce.
    rows.Single().Heatmap14.Should().AllSatisfy(v => v.Should().Be(1));
}
```
Per il secondo test serve un `FakeClock` con `TestTimeZones.RomeLike`: duplicare le tre righe di `CreateCustomTimeZone` in una piccola classe `Fixtures/TestTimeZones.cs` del progetto Data (una copia di 12 righe è più onesta di un `Compile Include` che lega due progetti di test).

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

- [ ] **Step 3: Convertire**

```csharp
// 1. quali abitudini stiamo per costruire
var nodeIds = await db.HabitNodes.IgnoreQueryFilters().AsNoTracking()
    .Where(predicate).Select(n => n.Id).ToListAsync(ct);

// 2. un confine per ciascuna, in UNA passata (spec §3.2)
var boundaries = await resetTimes.GetDayBoundariesAsync(nodeIds, ct).ConfigureAwait(false);

// 3. la finestra da leggere è l'unione delle finestre: il primo inizio e l'ultima fine fra
//    tutti i confini, così una query sola copre abitudini con reset diversi.
var appDay = await resetTimes.GetAppDayBoundaryAsync(ct).ConfigureAwait(false);
var windows = boundaries.Values.Append(appDay)
    .Select(b => { var t = b.Today(clock); return b.RangeOf(t.AddDays(-13), t); })
    .ToList();
var startMs = windows.Min(w => w.From).ToUnixTimeMilliseconds();
var endMs = windows.Max(w => w.ToExclusive).ToUnixTimeMilliseconds();
```

Poi il bucketing, che **non è più SQL**:
```csharp
// Prima: GroupBy in SQL su (HabitNodeId, (CompletedAtMs - startMs) / 86_400_000).
// Ora no: il giorno dipende dall'abitudine, e non dura sempre 24 ore (ora legale).
// La finestra è di 14 giorni, quindi le righe sono poche: si raggruppa in memoria.
var windowRows = await db.Completions.AsNoTracking()
    .Where(c => c.CompletedAtMs >= startMs && c.CompletedAtMs < endMs)
    .Select(c => new { c.HabitNodeId, c.CompletedAtMs })
    .ToListAsync(ct);

ImmutableArray<int> BuildStrip(Guid habitId, IEnumerable<long> stamps)
{
    var b = boundaries.TryGetValue(habitId, out var found) ? found : appDay;
    var today = b.Today(clock);
    var arr = new int[14];
    foreach (var ms in stamps)
    {
        var offset = today.DayNumber - b.DayOf(ms).DayNumber;   // 0 = oggi, 13 = 13 giorni fa
        if (offset is >= 0 and < 14) arr[13 - offset]++;
    }
    return ImmutableArray.Create(arr);
}
```
La stessa `BuildStrip` (con il selettore del valore al posto del conteggio) serve per `counterDailySumsByHabit` e `stateDailyLastByHabit`: fattorizzare **un** helper che mappa `ms → indice 0..13` e usarlo in tutti e tre, invece di ripetere la divisione in tre posti come oggi.

Restano da convertire, nello stesso metodo:
- `todayMs` → per-abitudine: `boundaries[id].RangeOf(today).From.ToUnixTimeMilliseconds()`; i totali "oggi" (`counterTotalsByHabit`, `stateLatestByHabit`) si filtrano con quello.
- la banda astinenza (`abstinenceBandByHabit`): sostituire `dayStart = startMs + i * 86_400_000` con `boundaries[id].RangeOf(day)`.

- [ ] **Step 4: Aggiornare i 29 siti di costruzione**

```bash
grep -rn "new HabitTreeQueryService(" tests/ src/ --include=*.cs | grep -v obj/
```
Nei test Data l'argomento è `fx.ResetTimes`; nei test WPF è il fake/stub del resolver del progetto WPF (crearne uno se non esiste: `FakeResetTimeResolver` che restituisce `DayBoundary.Utc`, o un confine configurabile).

- [ ] **Step 5: Eseguire i test, incluso il budget**

```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter FullyQualifiedName~HomeTreeBudget
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false
```
`HomeTreeBudgetTests` gira su 30 abitudini e 50 000 completamenti: **è lui il gate di perf di questo task**. Se il p95 supera i 300 ms, il colpevole più probabile è aver perso il `Where` sulla finestra (leggendo tutti i 50 000 invece dei soli 14 giorni). Non allargare la soglia: trovare la query.

- [ ] **Step 6: Provare i test contro il codice rotto**

Rimettere la divisione per 86 400 000: deve fallire `The_strip_does_not_shift_across_a_daylight_saving_change`. Sostituire il confine per-abitudine con uno solo per tutte: deve fallire `Two_habits_with_different_resets...`. Ripristinare.

- [ ] **Step 7: Commit**

```bash
git commit -am "fix(day): la riga Home usa un confine per abitudine, risolto in una passata (A18, B10)"
```

---

### Task 8: WPF — la fascia "Oggi" e la riga Home (A50-A56, B13-B15)

Da qui in poi si lavora nella UI, e vale la regola d'architettura di questo piano: **i VM non risolvono il confine, lo ricevono**. Chi carica la schermata lo risolve una volta e lo passa giù. Così l'unica dipendenza nuova sta in due VM (`HabitScreenViewModel`, `HomeViewModel`) invece che in venti, e i ~90 siti di costruzione dei VM nei test restano intatti.

Questa è anche la superficie dove lo scisma si **vede**: fra mezzanotte e le 02:00 la fascia "Oggi" e il calendario indicano D mentre i dati atterrano su D−1.

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Today/{TodayStripViewModel,ActionTodayContent,CounterTodayContent,ResistanceTodayContent,StateTodayContent,JournalTodayContent}.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (risolve e distribuisce)
- Modify: `src/Recordum.WPF/ViewModels/{HomeViewModel,HomeRowViewModel}.cs`
- Modify: `tests/Recordum.WPF.Tests/**` (i test che chiamano `LoadAsync` / `LoadKeyFigureAsync`)

**Interfaces:**
- Consumes: `IResetTimeResolver` (iniettato **solo** in `HabitScreenViewModel` e `HomeViewModel`), `DayBoundary`.
- Produces:
  - `TodayStripViewModel.LoadAsync(HabitNode node, DayBoundary day, CancellationToken ct = default)`
  - stessa firma per i cinque `*TodayContent.LoadAsync`
  - `HomeRowViewModel.LoadKeyFigureAsync(DayBoundary day, CancellationToken ct = default)`
  - `HabitScreenViewModel` espone `internal DayBoundary CurrentDayBoundary` (serve ai Task 9 e 10 e ai test)

- [ ] **Step 1: Scrivere i test che falliscono**

`tests/Recordum.WPF.Tests/ViewModels/TodayStripDayBoundaryTests.cs`:

```csharp
private static readonly TimeZoneInfo Plus2 =
    TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

[Fact]
public async Task Action_today_counts_a_completion_made_after_local_midnight()
{
    // 00:30 locali del 19 (= 22:30Z del 18). Prima: "non fatto oggi", perché la finestra locale
    // pescava sul giorno giusto ma "oggi" veniva da un'altra formula.
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 18, 22, 40, 0, TimeSpan.Zero), Plus2);
    var day = new DayBoundary(Plus2, TimeOnly.MinValue);
    var vm = new ActionTodayContent(...);

    await vm.LoadAsync(node, day);

    vm.IsDoneToday.Should().BeTrue();
}

[Fact]
public async Task Resistance_today_uses_the_same_today_as_its_siblings()
{
    // A53: era l'unico dei cinque a calcolare "oggi" in UTC. Con clock alle 22:40Z del 18
    // in fuso +2, la finestra dei tentativi 90g parte dal 21 maggio, non dal 20.
    ...
}

[Fact]
public async Task A_04_00_reset_moves_todays_counter_total_to_the_previous_day()
{
    // Registrazione alle 02:30 locali con reset 04:00: il totale di "oggi" è 0, quello di ieri è 1.
    ...
}

[Fact]
public async Task Home_row_streak_uses_the_boundary_it_is_given()
{
    var row = new HomeRowViewModel(...);
    await row.LoadKeyFigureAsync(new DayBoundary(Plus2, new TimeOnly(4, 0)));
    row.KeyFigureText.Should().Contain("1");
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

```
dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false -- xUnit.ParallelizeTestCollections=false
```
(filtrare con `--filter FullyQualifiedName~TodayStripDayBoundary` per il giro veloce)

- [ ] **Step 3: Far scendere il confine dallo screen**

In `HabitScreenViewModel`, iniettare `IResetTimeResolver resetTimes` e risolvere **una volta** per abitudine selezionata, prima del `Task.WhenAll` dei caricamenti:

```csharp
// Un solo posto in tutta la schermata decide a che giorno appartengono le cose (spec R9 §3.3):
// i pannelli lo ricevono, non lo ricalcolano. Prima erano nove formule diverse.
_dayBoundary = await _resetTimes.GetDayBoundaryAsync(node.Id, ct).ConfigureAwait(true);
```
e passarlo a `Today.LoadAsync(node, _dayBoundary, ct)`. Esporlo come `internal DayBoundary CurrentDayBoundary => _dayBoundary;`.

In `HomeViewModel:493`, risolvere in batch prima del fan-out:
```csharp
var boundaries = await _resetTimes.GetDayBoundariesAsync(
    rows.Select(r => r.HabitNodeId).ToList()).ConfigureAwait(true);
await Task.WhenAll(rows.Select(r =>
    r.LoadKeyFigureAsync(boundaries.TryGetValue(r.HabitNodeId, out var b) ? b : appDay)));
```
(`appDay` da `GetAppDayBoundaryAsync`, risolto una volta.) È l'unico punto WPF con N abitudini insieme: se qui si chiamasse la versione singola, tornerebbero N cascate.

- [ ] **Step 4: Convertire i cinque contenuti "Oggi" + la riga Home**

| sito | era | diventa |
|---|---|---|
| A50, A52, A53, A54, A55, A56 | tre formule diverse per "oggi" | `day.Today(_clock)` |
| A51 | `DateOnly.FromDateTime(ConvertTime(...))` sui completamenti degli ultimi 30g | `day.DayOf(c.CompletedAtMs)` |
| B14, B15 | `LocalDayWindow(day)` scritta a mano in due file | `day.RangeOf(d)` — **cancellare** i due metodi privati |
| B13 | `fromMs` dei tentativi 90g a mezzanotte UTC | `day.RangeOf(from).From.ToUnixTimeMilliseconds()` |

Le due `LocalDayWindow` duplicate sono esattamente il difetto che questo piano chiude: due copie della stessa idea che nessuno teneva allineate (e infatti `ResistanceTodayContent` non ne aveva nessuna e usava UTC). Vanno **rimosse**, non lasciate come wrapper.

- [ ] **Step 5: Eseguire i test**

```
dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false -- xUnit.ParallelizeTestCollections=false
```
Se si appende a zero fallimenti, **rilanciare** (W24-E1), non indagare.

- [ ] **Step 6: Provare i test contro il codice rotto**

Rimettere `DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime)` in `ResistanceTodayContent`: deve fallire `Resistance_today_uses_the_same_today_as_its_siblings`. Ripristinare.

- [ ] **Step 7: Commit**

```bash
git commit -am "fix(day): la fascia Oggi e la riga Home ricevono un solo 'oggi' (A50-A56, B13-B15)"
```

---

### Task 9: WPF — Dettaglio, calendario, registrazioni, note, marker (A12, A22-A48, B11-B12) — chiude R10

Il blocco più largo del piano, ma anche il più meccanico: sono tutte sostituzioni della stessa forma, con lo stesso confine che arriva dallo screen. **A40 è R10**: dopo questo task il finding si chiude per costruzione, perché non resta un secondo modo di calcolare "oggi".

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Detail/{DetailViewModel,GoalProgressCardViewModel,HourlyHistogramViewModel,StreakSummaryViewModel,StreakTimelineViewModel,TrendChartViewModel,YearHeatmapViewModel}.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/{HabitScreenViewModel,HabitRecordsViewModel,CompletionListViewModel,AbstinencePeriodRowViewModel}.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/{NotesViewModel,NoteRowViewModel}.cs`, `Journal/JournalQuickEntryViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/AbstinenceQuickAddViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/Export/ExportDialogViewModel.cs`
- Modify: `src/Recordum.Data/Services/NoteService.cs:87` (A12)

**Interfaces:**
- Consumes: `HabitScreenViewModel.CurrentDayBoundary` (Task 8), `IResetTimeResolver` in `ExportDialogViewModel` (è un dialog autonomo: usa `GetAppDayBoundaryAsync`), `DayBoundary`.
- Produces: `DayBoundary day` come **secondo parametro** di `LoadAsync` su tutti i VM del Dettaglio e su `CalendarNavigatorViewModel.LoadAsync(int year, int month, DateOnly? selected, DayBoundary day, CancellationToken ct = default)`; `NoteService` guadagna `IResetTimeResolver` nel costruttore.

- [ ] **Step 1: Scrivere i test che falliscono**

`tests/Recordum.WPF.Tests/ViewModels/DetailDayBoundaryTests.cs` — non uno per VM, ma quattro che coprono le quattro forme diverse:

```csharp
[Fact]
public async Task Abstinence_markers_use_the_same_today_as_the_calendar()   // R10
{
    // clock 2026-08-18T22:40Z in fuso +2. Prima: i marker calcolavano "19"? no — UTC diceva "18",
    // il navigatore diceva "19". La griglia evidenziava un giorno e la colorazione un altro.
    var screen = BuildScreen(new FakeClock(new DateTimeOffset(2026, 8, 18, 22, 40, 0, TimeSpan.Zero), Plus2));
    await screen.LoadAsync(node);

    screen.Calendar.Today.Should().Be(new DateOnly(2026, 8, 19));
    screen.LastAbstinenceMarkersToday.Should().Be(new DateOnly(2026, 8, 19));
}

[Fact]
public async Task Year_heatmap_anchor_follows_the_local_today() { ... }

[Fact]
public async Task Goal_rolling_window_ends_on_the_local_today() { ... }

[Fact]
public async Task A_note_written_after_local_midnight_is_anchored_to_the_new_day() { ... }
```
`Calendar.Today` e `LastAbstinenceMarkersToday` sono superfici da esporre `internal` se non lo sono già: un test che non può leggere il valore non prova niente. Se esporli sembra invasivo, l'alternativa accettabile è asserire sul **marker** del giorno 19 nella collezione, non su un campo.

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

- [ ] **Step 3: Convertire, in quattro giri**

**(a) Dettaglio** — `DetailViewModel.LoadAsync` riceve il confine dallo screen e lo gira ai sei figli. Sostituzioni: A26-A37 → `day.Today(clock)`, `day.DayOf(node.CreatedAt)`, `day.DayOf(p.StartedAtMs)`, `day.DayOf(p.EndedAtMs!.Value)`; B11 → `day.RangeOf(from, to)`.

**(b) Calendario e registrazioni** — A25 (`_today` del navigatore, ora impostato in `LoadAsync` dal confine ricevuto), A38, A39, B12.

Per `CalendarNavigatorViewModel`: `SelectableYears` resta costruito nel costruttore dall'anno locale del clock. È la scelta consapevole: il confine può cambiare l'anno solo a cavallo del 31 dicembre, la lista copre 15 anni indietro e uno avanti, e spostare la costruzione dentro `LoadAsync` significherebbe ricostruire una `ObservableCollection` bindata a ogni caricamento — che in questo repo è come si perdono i dati (`4d01f9c`, `1d96967`). Scriverlo come commento accanto al campo.

**(c) Note e diario** — A12, A44, A45, A46: l'ancora `TargetDate ?? CreatedAt` compare in **quattro** posti con la stessa espressione copiata. Fattorizzarla una volta e chiamarla dagli altri tre:
```csharp
// Recordum.Core/Time/NoteAnchor.cs
public static DateOnly For(Note note, DayBoundary day)
    => note.TargetDate ?? day.DayOf(note.CreatedAt);
```
Quattro copie della stessa riga sono quattro occasioni di divergere: è già successo con `LocalDayWindow` (Task 8). A47 e A43 → `day.Today(clock)`.

**(d) Astinenza e dialog** — A23, A24, A48 (`day.DayOf(ms)`), A22 (`ExportDialogViewModel`: `var day = await _resetTimes.GetAppDayBoundaryAsync(ct); var today = day.Today(_clock);`).

- [ ] **Step 4: Eseguire i test WPF e Data**

- [ ] **Step 5: Provare i test contro il codice rotto**

Rimettere `DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime)` in `BuildAbstinenceMarkersAsync` (A40): deve fallire `Abstinence_markers_use_the_same_today_as_the_calendar`. Se **non** fallisce, il test sta guardando il navigatore due volte invece di confrontare due sorgenti diverse: correggerlo prima di andare avanti. Ripristinare.

- [ ] **Step 6: Commit**

```bash
git commit -am "fix(day): Dettaglio, calendario, note e marker su un solo 'oggi' (A12, A22-A48, B11-B12) — chiude R10"
```

---

### Task 10: Le retroattive si stampigliano a mezzogiorno locale (A11, A19-A20, A57-A58, B16)

Una registrazione retroattiva per il giorno D oggi si salva a **D 00:00 UTC**, che sotto il confine nuovo può cadere nel giorno prima. Mezzogiorno locale sta comodamente dentro il giorno per qualunque reset plausibile (00:00–06:00) e regge il cambio d'ora (spec §5).

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/QuickAddViewModelBase.cs:30-58`
- Modify: `src/Recordum.Data/Services/DiaryService.cs:28-40`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/CompletionRowViewModel.cs:70-90`
- Modify: `src/Recordum.WPF/Bootstrap/DemoDataSeeder.cs:60-75,180-195,300-310`
- Modify: i test di quick-add, diario e riga registrazione

**Interfaces:**
- Consumes: `DayBoundary` (dallo screen, via `QuickAddViewModelBase.DayBoundary` settabile), `IResetTimeResolver` (in `DiaryService` e `DemoDataSeeder`).
- Produces:
  - `QuickAddViewModelBase.DayBoundary { get; set; }` (default `DayBoundary.Utc`, impostata da `HabitScreenViewModel` quando costruisce il quick-add)
  - `protected (DateTime Timestamp, long Ms, bool IsRetroactive) ResolveCompletionTimestamp()` — invariata come firma, diversa come semantica
  - `DiaryService` con `IResetTimeResolver resetTimes` in coda al costruttore

- [ ] **Step 1: Scrivere i test che falliscono**

```csharp
[Fact]
public void A_retroactive_entry_lands_at_local_noon_of_the_day_it_names()
{
    // Fuso +2, giorno 15 agosto: l'istante salvato è 10:00Z, cioè mezzogiorno locale.
    var vm = new TestQuickAdd(...) { DayBoundary = new DayBoundary(Plus2, TimeOnly.MinValue) };
    vm.CompletionDate = new DateTime(2026, 8, 15);

    var (ts, ms, retro) = vm.Resolve();

    retro.Should().BeTrue();
    ts.Should().Be(new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc));
}

[Theory]
[InlineData(0)]
[InlineData(6)]
public void A_retroactive_entry_stays_on_its_day_for_any_plausible_reset(int resetHour)
{
    // Il test che dà senso a "mezzogiorno": qualunque reset fra 00:00 e 06:00, il giorno non si muove.
    var day = new DayBoundary(Plus2, new TimeOnly(resetHour, 0));
    var vm = new TestQuickAdd(...) { DayBoundary = day };
    vm.CompletionDate = new DateTime(2026, 8, 15);

    var (_, ms, _) = vm.Resolve();

    day.DayOf(ms).Should().Be(new DateOnly(2026, 8, 15));
}

[Fact]
public void A_retroactive_entry_stays_on_its_day_across_a_daylight_saving_change()
{
    // 29 marzo 2026 (giorno di 23 ore) e 25 ottobre (25 ore) con TestTimeZones.RomeLike.
    ...
}

[Fact]
public void An_entry_for_today_keeps_the_current_instant_and_is_not_retroactive()
{
    // Non si tocca ciò che non è retroattivo: adesso è adesso.
    ...
}

[Fact]
public async Task A_diary_entry_for_a_past_day_lands_at_local_noon() { ... }   // DiaryService
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

- [ ] **Step 3: Convertire `QuickAddViewModelBase`**

```csharp
/// <summary>Il confine di giornata dell'abitudine selezionata, impostato dallo screen. Default
/// <see cref="DayBoundary.Utc"/> per i test che non lo impostano.</summary>
public DayBoundary DayBoundary { get; set; } = DayBoundary.Utc;

protected QuickAddViewModelBase(ICompletionService completions, IClock clock)
{
    ...
    CompletionDate = DayBoundary.Today(clock).ToDateTime(TimeOnly.MinValue);   // A57
}

/// <summary>Costruisce (istante, ms, retroattiva) per <see cref="CompletionDate"/>.
/// <para>Una registrazione per oggi si stampiglia <b>adesso</b>. Una retroattiva per il giorno D si
/// stampiglia a <b>mezzogiorno locale di D</b>: sta dentro il giorno per qualunque reset plausibile
/// (00:00–06:00) e regge il cambio d'ora, mentre la vecchia mezzanotte UTC poteva cadere nel giorno
/// prima non appena il confine è diventato locale (spec R9 §5).</para></summary>
protected (DateTime Timestamp, long Ms, bool IsRetroactive) ResolveCompletionTimestamp()
{
    var date = DateOnly.FromDateTime(CompletionDate.Date);
    var today = DayBoundary.Today(Clock);                                      // A58
    if (date == today)
    {
        var now = Clock.UtcNow;
        return (now.UtcDateTime, now.ToUnixTimeMilliseconds(), false);
    }
    var noon = DayBoundary.NoonOf(date);   // Task 1
    return (noon.UtcDateTime, noon.ToUnixTimeMilliseconds(), true);
}
```

`HabitScreenViewModel` imposta `QuickAdd.DayBoundary = _dayBoundary` subito dopo aver costruito il quick-add (nello stesso punto in cui oggi imposta `CompletionDate`).

- [ ] **Step 4: Convertire `DiaryService` (A11, B16)**

```csharp
var day = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
var today = day.Today(clock);
var retro = dayParam != today;
var instant = retro ? day.NoonOf(dayParam) : clock.UtcNow;
var ts = instant.UtcDateTime;
var ms = instant.ToUnixTimeMilliseconds();
```
(`dayParam` è il parametro `DateOnly day` del metodo, rinominato qui solo per non confonderlo con la variabile `day` del confine: nel codice reale scegliere due nomi distinti — es. `entryDay` e `boundary` — invece di ombreggiare.)

Il mezzogiorno locale vive in `DayBoundary.NoonOf` (Task 1), non in un helper del quick-add: è lo stesso istante che il backfill del Task 11 deve calcolare, e due implementazioni della stessa regola divergono.

- [ ] **Step 5: Convertire `CompletionRowViewModel`**

Oggi `SaveEditAsync` fa `DateTime.SpecifyKind(EditedDate, DateTimeKind.Utc)`: reinterpreta come UTC un valore che l'utente ha letto e scritto come ora locale. Diventa:
```csharp
// L'utente modifica un orario di parete: convertirlo dal fuso dell'abitudine, non dichiararlo UTC.
var edited = new DateTimeOffset(EditedDate, DayBoundary.TimeZone.GetUtcOffset(EditedDate));
var newDate = edited.UtcDateTime;
var newMs = edited.ToUnixTimeMilliseconds();
```
`DayBoundary` arriva dalla lista che possiede la riga (`CompletionListViewModel`, già toccata al Task 9 per B12). `IsRetroactive` **non cambia**: modificare la data a mano non trasforma una registrazione live in retroattiva, e cambiarlo qui altererebbe silenziosamente i conteggi di recovery `Retroactive`.

- [ ] **Step 6: Convertire `DemoDataSeeder` (A19-A20)**

`day.Today(clock)` dal confine dell'app (`GetAppDayBoundaryAsync`), e le registrazioni seminate su giorni passati a `day.NoonOf(d)` invece che a mezzanotte UTC — altrimenti il seeder demo produce esattamente i dati che questo piano considera sbagliati.

- [ ] **Step 7: Eseguire i test**

- [ ] **Step 8: Provare i test contro il codice rotto**

Rimettere `DateTime.SpecifyKind(date, DateTimeKind.Utc)` in `ResolveCompletionTimestamp`: devono fallire `A_retroactive_entry_lands_at_local_noon...` e almeno uno dei due casi di `..._for_any_plausible_reset`. Ripristinare.

- [ ] **Step 9: Commit**

```bash
git commit -am "fix(day): le registrazioni retroattive si stampigliano a mezzogiorno locale (A11, A19-A20, A57-A58, B16)"
```

---

### Task 11: Il backfill delle retroattive esistenti

Le retroattive già in archivio dichiarano un giorno che l'utente ha scelto a mano, e sono salvate a mezzanotte UTC: sotto il confine nuovo alcune scivolano al giorno prima. Ri-stampigliarle a mezzogiorno locale **non cambia** il loro significato, lo preserva — ed è l'eccezione motivata alla regola "nessun backfill" (spec §5).

**Non è una migration.** Calcolare "mezzogiorno locale" richiede `TimeZoneInfo` e la storia dell'ora legale; `datetime(…, 'localtime')` di SQLite è inaffidabile sulle date passate.

**Files:**
- Create: `src/Recordum.Data/Maintenance/RetroactiveNoonBackfill.cs`
- Create: `src/Recordum.Core/Abstractions/Services/IMaintenanceStep.cs` (o interfaccia diretta, vedi sotto)
- Modify: `src/Recordum.WPF/Bootstrap/{ApplicationInitializer,ServiceRegistrar}.cs`
- Modify: `src/Recordum.Data/SeedData/AppSettingSeed.cs` (+1 chiave)
- Create: `tests/Recordum.Data.Tests/Maintenance/RetroactiveNoonBackfillTests.cs`

**Interfaces:**
- Consumes: `IResetTimeResolver.GetDayBoundariesAsync`, `DayBoundary.{DayOf, NoonOf}`, `IAppSettingService`.
- Produces: `RetroactiveNoonBackfill.RunAsync(CancellationToken ct) → ValueTask<int>` (numero di righe spostate; 0 se già fatto).

> **Nota sull'AppSetting**: la chiave nuova (`maintenance.retro_noon_backfill_done`, default `"false"`) va aggiunta a `AppSettingSeed.All` — che è **dati di seed**, non schema: nessuna migration, ma **il conteggio degli AppSetting cambia** (da 18 a 19) e ci sono test che lo asseriscono. Cercarli e aggiornarli: `grep -rn "AppSettingSeed" tests/`. Su un DB già esistente la chiave non c'è: il codice deve trattare "assente" come `"false"`, non come errore.

- [ ] **Step 1: Scrivere i test che falliscono**

```csharp
[Fact]
public async Task Moves_a_retroactive_completion_to_local_noon_of_the_day_it_declares()
{
    // Retroattiva per il 15 agosto salvata a 2026-08-15T00:00Z. In fuso +2 quel giorno con reset
    // 04:00 significa "14 agosto": il backfill la porta a 10:00Z, cioè mezzogiorno locale del 15.
    var moved = await backfill.RunAsync();

    moved.Should().Be(1);
    (await Reload(id)).CompletedAtMs.Should().Be(
        new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
}

[Fact]
public async Task Leaves_non_retroactive_completions_alone()
{
    // L'istante è l'istante: una registrazione live non si tocca mai (spec §5).
    ...
    (await Reload(liveId)).CompletedAtMs.Should().Be(originalMs);
}

[Fact]
public async Task Is_idempotent()
{
    await backfill.RunAsync();
    var after = (await Reload(id)).CompletedAtMs;

    var second = await backfill.RunAsync();

    second.Should().Be(0);
    (await Reload(id)).CompletedAtMs.Should().Be(after);
}

[Fact]
public async Task Runs_once_even_if_the_flag_is_the_only_thing_that_stops_it()
{
    // Il guard è l'AppSetting, non l'assenza di lavoro: reset del flag → rigira, e il risultato
    // è lo stesso (i valori sono già a mezzogiorno, quindi 0 righe spostate).
    ...
}

[Fact]
public async Task Uses_each_habits_own_boundary()
{
    // Due abitudini con reset diversi: ognuna riceve il proprio mezzogiorno locale
    // (che in un fuso solo coincide, ma il codice non deve assumerlo).
    ...
}

[Fact]
public async Task Updates_both_CompletedAt_and_CompletedAtMs()
{
    // Lasciarne indietro uno dei due è il modo classico di rompere la sincronizzazione LWW.
    ...
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

- [ ] **Step 3: Implementare il passo di manutenzione**

```csharp
namespace Recordum.Data.Maintenance;

/// <summary>
/// Passo di manutenzione una tantum introdotto con R9: ri-stampiglia le registrazioni
/// <b>retroattive</b> esistenti al mezzogiorno locale del giorno che già dichiarano.
/// <para>Erano salvate a mezzanotte UTC del giorno scelto dall'utente; col confine locale +
/// <c>reset_time</c> quell'istante può cadere nel giorno precedente. Spostarle non cambia il loro
/// significato, lo preserva — quel giorno l'utente l'aveva scelto a mano. Le registrazioni
/// <b>non</b> retroattive non si toccano: l'istante è l'istante, e la rilettura le colloca da sé.</para>
/// <para>Non è una migration: "mezzogiorno locale" richiede TimeZoneInfo e la storia dell'ora legale,
/// che <c>datetime(…,'localtime')</c> di SQLite non sa fare sulle date passate. Gira nell'initializer
/// dopo le migrazioni, è idempotente, ed è guardato da un AppSetting.</para>
/// </summary>
public sealed partial class RetroactiveNoonBackfill(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IResetTimeResolver resetTimes,
    IAppSettingService settings,
    ILogger<RetroactiveNoonBackfill> logger)
{
    internal const string DoneKey = "maintenance.retro_noon_backfill_done";

    public async ValueTask<int> RunAsync(CancellationToken ct = default)
    {
        if (await settings.GetAsync(DoneKey, ct).ConfigureAwait(false) == "true") return 0;

        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.Completions
            .Where(c => c.IsRetroactive)
            .Select(c => new { c.Id, c.HabitNodeId, c.CompletedAtMs })
            .ToListAsync(ct).ConfigureAwait(false);

        if (rows.Count == 0)
        {
            await settings.SetAsync(DoneKey, "true", ct).ConfigureAwait(false);
            return 0;
        }

        var boundaries = await resetTimes
            .GetDayBoundariesAsync(rows.Select(r => r.HabitNodeId).Distinct().ToList(), ct)
            .ConfigureAwait(false);

        var moved = 0;
        foreach (var r in rows)
        {
            var day = boundaries[r.HabitNodeId];
            // Il giorno DICHIARATO dalla riga è quello del suo istante letto in UTC: è così che è
            // stato scritto (mezzanotte UTC del giorno scelto). Non rileggerlo col confine nuovo,
            // o si sposterebbe proprio ciò che stiamo cercando di tenere fermo.
            var declared = DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeMilliseconds(r.CompletedAtMs).UtcDateTime);
            var noon = day.NoonOf(declared);
            var newMs = noon.ToUnixTimeMilliseconds();
            if (newMs == r.CompletedAtMs) continue;

            var entity = await db.Completions.FirstAsync(c => c.Id == r.Id, ct).ConfigureAwait(false);
            entity.CompletedAt = noon.UtcDateTime;
            entity.CompletedAtMs = newMs;
            moved++;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await settings.SetAsync(DoneKey, "true", ct).ConfigureAwait(false);
        LogBackfilled(moved);
        return moved;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Retroactive noon backfill: {Moved} completions restamped")]
    private partial void LogBackfilled(int moved);
}
```

Il commento su `declared` è la parte che va capita, non copiata: il backfill legge il giorno **come lo leggeva il codice vecchio** (UTC), perché è quello che l'utente aveva scelto quando ha salvato. Rileggerlo col confine nuovo sposterebbe la riga di un giorno — l'esatto contrario dello scopo.

- [ ] **Step 4: Agganciarlo all'avvio**

In `ApplicationInitializer.InitializeAsync`, subito **dopo** `db.Database.MigrateAsync(ct)` e prima dei plugin:
```csharp
// Manutenzione una tantum R9: le retroattive salvate a mezzanotte UTC vanno a mezzogiorno locale.
// Dopo le migrazioni (ha bisogno dello schema aggiornato), prima di tutto ciò che legge giorni.
await retroBackfill.RunAsync(ct).ConfigureAwait(false);
```
`RetroactiveNoonBackfill` entra nel costruttore dell'initializer e va registrato in `ServiceRegistrar`. **Non** avvolgerlo in try/catch silenzioso: se fallisce, deve fallire l'avvio come già fa una migrazione fallita — un backfill saltato in silenzio è peggio di un avvio che si lamenta.

- [ ] **Step 5: Eseguire i test e la suite completa**

```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false
dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false -- xUnit.ParallelizeTestCollections=false
```
Aggiornare i test che contano gli AppSetting (18 → 19).

- [ ] **Step 6: Provare i test contro il codice rotto**

Togliere il guard `DoneKey`: `Is_idempotent` resta verde (il secondo giro non sposta niente comunque) ma deve tornare `moved > 0`? No — verifica invece togliendo il `continue` su `newMs == r.CompletedAtMs`: `Is_idempotent` deve fallire su `second.Should().Be(0)`. Poi togliere il filtro `c.IsRetroactive`: deve fallire `Leaves_non_retroactive_completions_alone`. Ripristinare.

- [ ] **Step 7: Disciplina dati prima di provarlo sul DB vero (spec §5, E21)**

Prima di lanciare la build su dati reali: **backup `.recbak` manuale**, poi prova su una **copia** del DB, poi l'originale. Scriverlo nel messaggio di commit, così resta nella cronologia e non solo in questo file.

- [ ] **Step 8: Commit**

```bash
git commit -am "feat(day): backfill una tantum delle retroattive a mezzogiorno locale

Passo di manutenzione idempotente nell'ApplicationInitializer, guardato da AppSetting.
Prima di girarlo su dati reali: .recbak manuale + prova su copia (spec §5, E21)."
```

---

### Task 12: La prova che il punto di verità è uno solo — e il guard che lo tiene tale

Due cose che nessun task precedente poteva fare, perché ognuno vedeva solo la propria superficie: il test che confronta **le cinque superfici fra loro** (spec §7) e il guard che impedisce alla tabella §0 di scadere al primo merge.

**Files:**
- Create: `tests/Recordum.Data.Tests/Integration/DayBoundaryParityTests.cs`
- Create: `tests/Recordum.Data.Tests/Architecture/DayDerivationGuardTests.cs`
- Modify: `docs/phase-4-issues.md` (R9 → `fixed`, R10 → `fixed`, nota sullo storico che si ri-interpreta)
- Modify: `CLAUDE.md` (§8 convenzioni + §7 progresso)

**Interfaces:**
- Consumes: tutto ciò che i Task 3-11 hanno costruito.
- Produces: nessuna API nuova.

- [ ] **Step 1: Scrivere il test di parità**

È il test che la spec §7 chiede a parole: *"una registrazione alle 00:30 locali con reset 00:00 cade su oggi in streak, heatmap, calendario, andamento ed export — lo stesso giorno in tutti e cinque"*. Un solo DB, un solo istante, cinque letture.

```csharp
using FluentAssertions;
using Xunit;

namespace Recordum.Data.Tests.Integration;

/// <summary>
/// La prova che il punto di verità è uno. Ogni superficie che dice "questo è successo il giorno X"
/// deve dire lo stesso X sugli stessi dati: se una diverge, R9 è tornato.
/// </summary>
public class DayBoundaryParityTests
{
    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    [Theory]
    // reset 00:00: le 00:30 locali del 19 sono del 19
    [InlineData(0, "2026-08-19")]
    // reset 04:00: le stesse 00:30 locali sono ancora del 18
    [InlineData(4, "2026-08-18")]
    public async Task All_five_surfaces_agree_on_the_day_of_one_completion(int resetHour, string expected)
    {
        var expectedDay = DateOnly.Parse(expected, CultureInfo.InvariantCulture);
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 8, 0, 0, TimeSpan.Zero), Plus2);
        await using var fx = new DbFixture(clock);
        var habitId = await SeedCounterHabitAsync(fx, reset: new TimeOnly(resetHour, 0));

        // 2026-08-18T22:30Z = 19 agosto, 00:30 locali.
        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero), value: 3m);

        var from = new DateOnly(2026, 8, 15);
        var to = new DateOnly(2026, 8, 20);

        // 1. heatmap
        var heatmap = await Completions(fx).GetHeatmapAsync(habitId, from, to);
        heatmap.Should().ContainSingle().Which.Date.Should().Be(expectedDay);

        // 2. calendario (magnitudine giornaliera)
        var mags = await Completions(fx).GetDailyMagnitudeAsync(habitId, 2026, 8);
        mags.Should().ContainSingle().Which.Date.Should().Be(expectedDay);

        // 3. streak
        var runs = await Aggregator(fx).GetHistoryAsync(habitId, from, to);
        runs.Should().ContainSingle();
        runs[0].Start.Should().Be(expectedDay);
        runs[0].End.Should().Be(expectedDay);

        // 4. andamento
        var trend = await Trends(fx).GetTrendAsync(habitId, from, to, TrendGranularity.Day);
        trend.Should().ContainSingle().Which.Bucket.Should().Be(expectedDay);

        // 5. export
        var export = await Exports(fx).BuildAsync(new ExportRequest(/* habit */ habitId, from, to, ...));
        export.Habits.Single().Completions.Should().ContainSingle()
            .Which.Day.Should().Be(expectedDay);
    }

    [Fact]
    public async Task The_five_surfaces_also_agree_across_a_daylight_saving_change()
    {
        // Stessa forma, ma con TestTimeZones.RomeLike e una registrazione per giorno dal 27 al 31
        // marzo 2026 (dentro c'è il giorno di 23 ore): cinque giorni distinti in tutte e cinque
        // le letture, nessun doppione e nessun buco.
        ...
        heatmapDays.Should().Equal(expectedDays);
        trendDays.Should().Equal(expectedDays);
        streakRun.Length.Should().Be(5);
    }
}
```

`ExportRequest` e il DTO delle completions esportate hanno la forma che il repo già usa: leggere `ExportQueryServiceTests` e ricopiarla, non inventarla.

- [ ] **Step 2: Eseguire il test di parità**

```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter FullyQualifiedName~DayBoundaryParity
```
Se una delle cinque diverge, **è un difetto vero di quel task**, non un assert da rilassare. Tornare al task corrispondente.

- [ ] **Step 3: Scrivere il guard anti-regressione**

```csharp
namespace Recordum.Data.Tests.Architecture;

/// <summary>
/// Il gemello del guard di R7 e di BuilderTemplateParityTests: la tabella §0 del piano R9 elenca
/// ogni punto che deriva un giorno da un istante, e senza qualcuno che la rilegga quella tabella
/// scade al primo merge. Questo test fallisce se ricompare una derivazione UTC fuori whitelist.
/// </summary>
public class DayDerivationGuardTests
{
    // I soli siti a cui è lecito derivare un giorno senza DayBoundary, con il perché.
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        // presentazione: converte il DateTime del DatePicker, non un istante (A21/B19)
        "Recordum.WPF/Converters/DateOnlyToDateTimeConverter.cs",
        // il giorno DICHIARATO da una riga già scritta va letto come fu scritto (Task 11)
        "Recordum.Data/Maintenance/RetroactiveNoonBackfill.cs",
        // DayBoundary è l'implementazione: è lui a definire la regola
        "Recordum.Core/Time/DayBoundary.cs",
    };

    [Fact]
    public void No_source_file_derives_a_day_from_an_instant_outside_DayBoundary()
    {
        var root = FindRepoRoot();
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || file.Contains("Migrations")) continue;

            var relative = Path.GetRelativePath(Path.Combine(root, "src"), file).Replace('\\', '/');
            if (Allowed.Contains(relative)) continue;

            var text = File.ReadAllText(file);
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains("DateOnly.FromDateTime", StringComparison.Ordinal)
                    || line.Contains("UtcNow.UtcDateTime.Date", StringComparison.Ordinal))
                    offenders.Add($"{relative}:{i + 1}  {line.Trim()}");
            }
        }

        offenders.Should().BeEmpty(
            "il giorno di appartenenza si deriva solo da DayBoundary (spec R9). "
            + "Se questa derivazione è legittima — presentazione, o un DateOnly già di dominio — "
            + "aggiungerla alla whitelist con il perché, e aggiornare la tabella §0 del piano R9.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Recordum.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Recordum.slnx non trovato risalendo da " + AppContext.BaseDirectory);
    }
}
```

Due avvertenze per chi lo esegue la prima volta:
- **la lista degli offender sarà lunga**: sono i siti classificati **P** e **D** in §0 che sopravvivono legittimamente (`JournalQuickAddViewModel:32`, `ChartQueryService:106`, `TrendChartControl`, i `ToDateTime(TimeOnly.MinValue)` verso i DatePicker). Aggiungerli alla whitelist **uno per uno leggendoli**, con il perché accanto — non in blocco. Se in quella lista compare un sito che i Task 3-11 dovevano convertire, è un pezzo dimenticato: convertirlo, non whitelistarlo. **È questo il vero valore del guard: farlo girare è l'ultimo controllo dell'audit.**
- il pattern `ToDateTime(TimeOnly` **non** entra nel guard: ha troppi usi legittimi di presentazione e produrrebbe rumore. La famiglia B è coperta dai test di comportamento (finestre) dei singoli task.

- [ ] **Step 4: Eseguire il guard, whitelistare leggendo, rieseguire**

```
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter FullyQualifiedName~DayDerivationGuard
```

- [ ] **Step 5: Suite completa, tutti i progetti**

```
dotnet test tests/Recordum.Core.Tests -p:UseAppHost=false
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false
dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false -- xUnit.ParallelizeTestCollections=false
dotnet test tests/Recordum.Plugins.Tests -p:UseAppHost=false
dotnet test tests/Recordum.Plugins.ExportEngine.Tests -p:UseAppHost=false
dotnet test tests/Recordum.Plugins.ChartEngine.Tests -p:UseAppHost=false
```
Baseline da cui si parte: **Core 118 / Data 449 / WPF 1022 / Plugins 66 / ExportEngine 37 / ChartEngine 18**. Annotare i conteggi finali nel commit di chiusura.

- [ ] **Step 6: Aggiornare il tracker**

In `docs/phase-4-issues.md`:
- **R9** → `fixed`, con: la forma della soluzione (un valore puro + un produttore, nessun cambio di schema), il numero di siti convertiti per famiglia, e **il rischio dichiarato**: *passando da UTC a locale lo storico si ri-interpreta, e un numero di streak può cambiare al primo avvio dopo l'aggiornamento — è corretto, non è una regressione*.
- **R10** → `fixed`, chiuso per costruzione da A40 e dal guard: non resta un secondo modo di calcolare "oggi".
- Aggiungere che `reset_time` **adesso ha un consumatore**: era configurabile e inerte in due punti d'interfaccia (Impostazioni → Generale e la sezione ResetTime dell'editor), come F0-14 lo è ancora per le notifiche.
- Dichiarare la conseguenza scelta della spec §6: `reset_time` **resta fuori da `HabitConfigHistory`** (è un setting, non uno stato versionato), quindi **cambiarlo ri-interpreta anche lo storico**, non solo il futuro. È il prezzo dell'alternativa scelta — i dati sono istanti, il giorno è un'interpretazione — e va scritto, perché all'utente che sposta il reset i numeri del passato cambieranno sotto gli occhi.
- Nota per il pass visivo dell'Onda 2, ancora aperto: le voci §1-§3 della checklist che riguardano cifre e strisce vanno **riguardate dopo R9** — i numeri possono essere cambiati, e legittimamente.

- [ ] **Step 7: Aggiornare `CLAUDE.md`**

- §8 (convenzioni): una riga sulla regola nuova — *"Il giorno di appartenenza di un istante si chiede a `DayBoundary` (Core), prodotto da `IResetTimeResolver`: mai `DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)`, mai una finestra a mezzanotte UTC. Un `DayBoundary` si risolve una volta in cima all'operazione e si passa giù; per più abitudini esiste la versione batch, o l'albero Home torna a N query. `DayDerivationGuardTests` fallisce se ricompare una derivazione fuori whitelist."*
- §7 (progresso): il paragrafo di chiusura di R9, con i conteggi suite e la nota sullo storico.
- §5 (modello dominio), voce "Cascata reset_time": togliere l'implicito "(Milestone 3.3+)" e dire che la cascata **è consumata** da `DayBoundary`.

- [ ] **Step 8: Commit di chiusura**

```bash
git add -A
git commit -m "test(day): parità fra le cinque superfici + guard anti-regressione; docs di chiusura R9

Un solo punto decide il giorno di appartenenza: locale + reset_time. Chiude R9 e R10.
Storico ri-interpretato: uno streak può cambiare al primo avvio — corretto, non regressione.
Suite: Core NNN / Data NNN / WPF NNN / Plugins 66 / ExportEngine 37 / ChartEngine 18."
```

---

## Dopo l'esecuzione

1. **Review di branch** (`superpowers:requesting-code-review`) sull'intero diff, non per-task: le due review più utili di quest'anno (C1/C2 dell'Onda 2.4, i sei findings dell'Onda 2.5) hanno trovato difetti che nessuna review per-task poteva vedere. Qui il candidato naturale è **una superficie dimenticata** — un sito di §0 marcato *converti* che nessun task ha toccato — e il guard del Task 12 è la sonda giusta per cercarlo.
2. **Verifica a schermo**, breve e mirata: registrare qualcosa **dopo mezzanotte** e controllare che riga Home, fascia Oggi, calendario, Dettaglio ed export dicano lo stesso giorno; poi impostare `reset_time` alle 04:00 e rifarlo. È la prima volta che quella manopola fa qualcosa.
3. **Merge** in `main` (fast-forward, come le onde precedenti) — leggendo prima la nota su cosa si trascina dietro (Global Constraints, primo punto).
4. **Poi E14** (motore notifiche minimo) e **★ dogfooding**, che è il motivo per cui R9 è stato anticipato: i dati delle prossime settimane sono reali, e questo difetto li avrebbe sporcati in silenzio.

