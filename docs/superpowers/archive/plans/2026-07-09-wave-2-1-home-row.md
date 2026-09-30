# Onda 2.1 — U6 riga Home componibile — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Make the Home-row right zone type-aware: a per-type **key figure chip** + an optional **strip** (none/7/14 days), both driven by per-habit `ui_prefs_json` with per-type defaults, plus a **live in-row clock** for Resistance, a Configure "Riga Home" section (the two knobs) and the **F0-11** cleanup of the Configure header's raw-enum ComboBoxes.

**Architecture:** The Home query (`HabitTreeQueryService`) is extended with **cheap** per-type aggregates only (counter today-total, state today-value+label, resistance open-period state, 14-day buckets, journal entries-this-week) — it stays under the `HomeTreeBudgetTests` p95<300ms budget (exit criteria E9/E20). **Streak is NOT computed in the query** (there is no cheap current-streak; the aggregator is heavy). The Action key figure (default = streak) is computed **lazily in the ViewModel after render**, in parallel, via `IStreakAggregator.GetHistoryAsync` (the run with `IsCurrent==true`). Per-type key-figure/strip selection reads `HabitUiPrefs` (from Onda 2.0) with per-type defaults. No schema change.

**Tech Stack:** C# / .NET 10, EF Core + SQLite, WPF + CommunityToolkit.Mvvm, xUnit.

## Global Constraints

- **No schema change / no migration.** All new query fields are cheap aggregates or VM-lazy. Streak stays VM-lazy.
- **Perf budget is a hard gate.** `tests/Recordum.Data.Tests/Performance/HomeTreeBudgetTests.cs` (p95<300ms on the seeded dataset) MUST stay green after the query extension. This is exit criterion E9/E20 — do not add per-row streak/aggregator work to the query. Run this test in the query task.
- **Streak sourcing decision (binding, author-approved):** Action's default key figure is the current streak, computed **after render, off the critical path**, via `IStreakAggregator.GetHistoryAsync(habitNodeId, from, to)` → take the `StreakRun` with `IsCurrent==true`, its `.Length` (0 if none). Compute over a bounded window (e.g. `today.AddYears(-1)`..`today`). Do this in parallel for visible Action rows (pattern: `Task.WhenAll`), never blocking initial render. Rationale + rejected alternatives in the memory `recordum-wave-2-1-streak-decision`.
- **Build/test:** `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo` and `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo`. Solution builds with `-p:UseAppHost=false`. WPF tests run on Windows only (this VM).
- **New user-facing strings = 3 edits each:** add the key to `src/Recordum.WPF/Resources/Strings.resx` (EN) + `Strings.it.resx` (IT) + a manual accessor property in `src/Recordum.WPF/Resources/Strings.Designer.cs`. The `guard-generated.js` hook denies Edit/Write on `*.Designer.cs` (it matches Strings.Designer.cs too) → **edit `Strings.Designer.cs` only via the Bash shell** (python/sed/heredoc), not the Edit tool. English-first.
- **`IClock` for time, never `DateTime.Now`.** The in-row Resistance clock reuses `LiveElapsedClock` (`src/Recordum.WPF/ViewModels/HabitScreen/LiveElapsedClock.cs`) but with **one shared `DispatcherTimer` for the whole Home list**, not one per row (spec §2). See Task 4.
- **Consume `ui_prefs_json` via `IHabitUiPrefsService`** (Onda 2.0: `GetAsync`/`SetAsync`, `HabitUiPrefs` record with `KeyFigure` string?, `Strip` HomeStrip?, `OpenColumns`, `JournalGesture`). `HomeStrip { None, Days7, Days14 }` already exists in `Recordum.Core.Enums`.
- **Branch:** feature branch `wave-2-1-home-row` off `main`; the `guard-main.js` hook blocks direct commits to `main` (create the branch as a separate step before any commit — the hook inspects the command's current branch at dispatch).
- **Author visual verification closes this wave** (state `verified-fixed`), not just green tests — the row chips/strip/clock must be seen on screen per type.

## Per-type matrix (spec §1, defaults in bold)

| Type | Key figure (default **bold**) | Strip default |
|---|---|---|
| Action | **streak** (`×12`) · done-today ✓ · none | filled/empty squares, 7d |
| Counter | **today total** (`22 pg`, with unit) · streak · none | value-height mini-bars, 7d |
| Resistance | **live clock** (`20g 4:12`) · clean days · none | continuous band, 14d |
| Journal | **today ✓/—** · entries this week · none | dots, 7d |
| State | **today value** (scale label, e.g. `Bene`) · 7d avg · none | value-colored cells (ramp), 7d |

Groups: unchanged (no chip/strip).

---

## Task 1: Extend HomeTreeRow + home query with cheap per-type key-figure fields

Add the per-type aggregates the row needs — **except streak** — to the DTO and the query, keeping the perf budget. Widen the completion-bucket window from 7 to 14 days.

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Services/Dtos/HomeTreeRow.cs`
- Modify: `src/Recordum.Data/Services/Queries/HabitTreeQueryService.cs`
- Test: `tests/Recordum.Data.Tests/Integration/HabitTreeQueryServiceTests.cs`
- Test (run, do not modify unless it needs the new ctor args): `tests/Recordum.Data.Tests/Performance/HomeTreeBudgetTests.cs`
- Test (may need new ctor args): `tests/Recordum.Core.Tests/Abstractions/Services/Dtos/HomeTreeRowTests.cs`

**Interfaces:**
- Produces on `HomeTreeRow` (append as optional params to preserve call sites where possible; if you must, update the two DTO tests):
  - `ImmutableArray<int> CompletionsLast14Days` — 14 daily counts (index 0 = 13 days ago … 13 = today).
  - `decimal? TodayCounterTotal` — sum of the primary metric's numeric value for today's completions (counter only; else null).
  - `string? TodayCounterUnitCode` — the primary `MetricDefinition`'s unit code (counter only; may be null).
  - `int? TodayStateValue` + `string? TodayStateLabel` — the latest state completion **today**'s scale integer + resolved label (state only; else null).
  - `AbstinenceStatus AbstinenceStatus` + `long? AbstinenceOpenSinceMs` — resistance derived state; `AbstinenceOpenSinceMs` set only while `Abstaining` (drives the in-row clock).
  - `int? JournalEntriesThisWeek` — count of this habit's completions in the last 7 days (journal only; else null). `IsCompletedToday` already covers "today ✓".
  - Keep `CompletionsLast7Days` (the last 7 of the 14) so existing bindings don't break.
- Consumes: `AbstinenceStatus`/`AbstinencePeriod` from `Recordum.Core.Abstractions.Services`/`Recordum.Core.Models`; the abstinence data lives in table `abstinence_period` (`habit_node_id`, `started_at_ms`, `ended_at_ms` nullable). Open period = `ended_at_ms IS NULL`.

- [ ] **Step 1: Write failing query tests**

In `HabitTreeQueryServiceTests.cs`, add focused tests (follow the file's existing fixture/seed pattern) asserting, on a small seeded tree:
- a Counter habit with two completions today (values 10 and 12 on its primary metric) → `TodayCounterTotal == 22`.
- a State habit whose latest today completion has scale value 3 with labels `["Male","Così","Bene"]` (labels_json on the metric) → `TodayStateValue == 3` and `TodayStateLabel == "Bene"`.
- a Resistance habit with an open `abstinence_period` → `AbstinenceStatus == Abstaining` and `AbstinenceOpenSinceMs == startedAtMs`; one with only a closed period → `Relapsed`, `AbstinenceOpenSinceMs == null`.
- `CompletionsLast14Days.Length == 14` and a completion 10 days ago lands at index 3.

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~HabitTreeQueryServiceTests"`
Expected: FAIL (new fields/args don't exist).

- [ ] **Step 2: Extend the DTO**

Add the fields above to `HomeTreeRow` as constructor parameters (append after `TagIds`, before `Icon`/`SortOrder`, or add with defaults). Keep `CompletionsLast7Days`. Update `HomeTreeRowTests.cs` if it constructs the record positionally.

- [ ] **Step 3: Extend the query**

In `HabitTreeQueryService.BuildRowsAsync`:
1. Widen the bucket window: change `startMs` to `today.AddDays(-13)` and the array size to 14; build `bucketsByHabit` as `ImmutableArray` of 14; derive the 7-day array as the last 7.
2. Add cheap correlated sub-selects in the main `raw` projection (all EF-translatable, no per-row aggregator):
   - `TodayCounterTotal`: for the primary metric (lowest `SortOrder` `MetricDefinition` of the node), `db.CompletionValues.Where(cv => cv.Completion.HabitNodeId == n.Id && cv.Completion.CompletedAtMs >= todayMs && cv.MetricDefinitionId == primaryId).Sum(cv => cv.ValueDecimal ?? cv.ValueInteger ?? cv.ValueSeconds ?? 0)`. Resolve `primaryId`/`unitCode` via a pre-loaded dictionary of primary metrics per node (one query: group `MetricDefinitions` by habit, take min SortOrder) to avoid N+1.
   - `TodayStateValue`/`Label`: latest today completion's primary metric integer; resolve the label from that metric's `LabelsJson` (parse JSON array, index by value-1). Do the label resolution **in memory** after the query (parse `LabelsJson` for state nodes), not in SQL.
   - Resistance: pre-load open/last periods with one query over `abstinence_period` (open where `ended_at_ms == null`; else the max `started_at_ms` closed one), build a per-node dict → `AbstinenceStatus` + `AbstinenceOpenSinceMs`.
   - `JournalEntriesThisWeek`: `db.Completions.Count(c => c.HabitNodeId == n.Id && c.CompletedAtMs >= sevenDaysAgoMs)` for journal nodes.
3. Keep `CurrentStreakCount: 0` (unchanged — streak is VM-lazy).

Keep every added query **set-based** (dictionaries pre-loaded once, like `bucketsByHabit`/`tagsByHabit` already are) — no per-row awaits.

- [ ] **Step 4: Run query tests + the perf budget**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~HabitTreeQueryServiceTests"` → PASS.
Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~HomeTreeBudgetTests"` → PASS (p95<300ms). **If it regresses, the extension is too heavy — move the offending aggregate out or pre-load it as a dictionary; do not commit a red budget.**

- [ ] **Step 5: Full Data suite + commit**

Run the full Data suite (green), then commit (`feat(data): per-type key-figure fields in home query (no streak, budget-safe)` + `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`).

---

## Task 2: Per-type key-figure resolution (Core, pure logic)

A pure resolver deciding, from a `HabitType` + `HabitUiPrefs`, which key figure and strip a row shows — with the per-type defaults from the matrix. Pure so it is trivially unit-tested and reused by the row VM and (later) the builder preview.

**Files:**
- Create: `src/Recordum.Core/HomeRow/HomeRowPresentation.cs` (enums + resolver)
- Test: `tests/Recordum.Core.Tests/HomeRow/HomeRowPresentationTests.cs`

**Interfaces:**
- Produces: enum `KeyFigureKind { None, Streak, DoneToday, CounterTotal, LiveClock, StateValue, JournalToday, JournalEntriesWeek, StateAverage7 }`; `static class HomeRowPresentation` with `KeyFigureKind ResolveKeyFigure(HabitType type, HabitUiPrefs prefs)` and `HomeStrip ResolveStrip(HabitType type, HabitUiPrefs prefs)`.
- Consumes: `HabitUiPrefs.KeyFigure` (string? — the persisted choice id) and `.Strip` (HomeStrip?).

- [ ] **Step 1: Failing tests** — assert: null prefs → per-type default (Action→Streak, Counter→CounterTotal, Resistance→LiveClock, Journal→JournalToday, State→StateValue; strip: Resistance→Days14, others→Days7); a set `prefs.KeyFigure="doneToday"` on an Action → `DoneToday`; `prefs.Strip=None` → `None`. The string ids map: `"streak"`→Streak, `"doneToday"`→DoneToday, `"counterTotal"`→CounterTotal, `"clock"`→LiveClock, `"stateValue"`→StateValue, `"stateAvg7"`→StateAverage7, `"journalToday"`→JournalToday, `"journalWeek"`→JournalEntriesWeek. Unknown/null → type default.

- [ ] **Step 2: Implement** the resolver (a switch on type for defaults; a string→enum map for explicit choices, validated against what each type allows). Keep it pure, no I/O.

- [ ] **Step 3: Run tests → PASS. Commit.**

---

## Task 3: HomeRowViewModel — key-figure chip + strip length + lazy streak

Wire the resolver + Task-1 data + lazy streak into the row VM so the view can bind a key-figure display string, its visibility, and the strip length.

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs`
- Modify: the row construction site (whoever builds `HomeRowViewModel` from `HomeTreeRow` — find via `new HomeRowViewModel(`); pass the new deps: `IHabitUiPrefsService`, `IStreakAggregator`, `IClock`).
- Test: `tests/Recordum.WPF.Tests/ViewModels/HomeRowViewModelTests.cs`
- New strings: `Home.KeyFigure.Streak` (`"×{0}"`), `Home.KeyFigure.CleanDays` (`"{0}g"`), etc. — see Step 3.

**Interfaces:**
- Consumes: `HomeTreeRow` (extended, Task 1), `HomeRowPresentation` (Task 2), `IHabitUiPrefsService`, `IStreakAggregator`, `LiveElapsedClock` shared timer (Task 4 provides the shared tick; for Task 3 model the clock display as a plain string the row exposes).
- Produces: `KeyFigureText` (string), `ShowKeyFigure` (bool), `HomeStrip StripLength`, `ImmutableArray<int> StripCounts` (7 or 14 slice), plus `IsResistanceClock` (bool) + `AbstinenceOpenSinceMs` for Task 4's timer.

- [ ] **Step 1: Failing VM tests** — with a fake `IHabitUiPrefsService` and `FakeStreakAggregator` (exists at `tests/Recordum.WPF.Tests/Fixtures/FakeStreakAggregator.cs`): a Counter row with `TodayCounterTotal=22`, unit "pg" → `KeyFigureText == "22 pg"`; a State row value 3 label "Bene" → `"Bene"`; an Action row, after `LoadKeyFigureAsync`, with the fake aggregator returning a current run length 12 → `KeyFigureText == "×12"`; `StripLength`/`StripCounts` honor the resolved strip (14 for Resistance default, else 7).

- [ ] **Step 2: Implement.** In the ctor, resolve the key figure/strip via `HomeRowPresentation` using prefs fetched by the caller (fetch prefs in the row-construction site and pass the `HabitUiPrefs` in — keeps the VM ctor synchronous). Build `KeyFigureText` for the synchronous kinds (CounterTotal, StateValue, JournalToday, DoneToday, LiveClock initial, JournalEntriesWeek) from the DTO. Add `public async Task LoadKeyFigureAsync(CancellationToken ct)` that, **only when the kind is Streak or StateAverage7**, computes the value off the critical path (`IStreakAggregator.GetHistoryAsync` → `IsCurrent` run length) and sets `KeyFigureText`. The Home list calls `LoadKeyFigureAsync` for its rows in parallel after building them (`Task.WhenAll`) — mirror the existing async-fill pattern (see CR24 markers `Task.WhenAll` in the codebase). Localize all number formats via new strings.

- [ ] **Step 3: Add strings** (EN + IT + Designer via shell): `Home.KeyFigure.Streak` = `"×{0}"` / `"×{0}"`; `Home.KeyFigure.CleanDays` = `"{0}d"` / `"{0}g"`; `Home.KeyFigure.CounterTotal` = `"{0} {1}"` (value, unit) / same; `Home.KeyFigure.EntriesWeek` = `"{0} this week"` / `"{0} questa settimana"`; `Home.KeyFigure.DoneToday`/`NotToday` = `"✓"`/`"—"`. (Add only the ones you actually use.) Edit `Strings.Designer.cs` via Bash heredoc/sed — the Edit tool is hook-blocked on it.

- [ ] **Step 4: Run VM tests + full WPF suite. Commit.**

---

## Task 4: In-row Resistance live clock (single shared Home timer)

Resistance rows show a live-ticking `20g 4:12`. Per spec §2 use **one** `DispatcherTimer` for the whole Home, not one per row.

**Files:**
- Create: `src/Recordum.WPF/ViewModels/Home/HomeClockTicker.cs` (a single shared 1s ticker fanning out to subscribed resistance rows)
- Modify: `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs` (compute `KeyFigureText` from `AbstinenceOpenSinceMs` on tick when the kind is LiveClock)
- Modify: the Home list VM (own one `HomeClockTicker`, register resistance rows on build, dispose on reload/teardown)
- Test: `tests/Recordum.WPF.Tests/ViewModels/Home/HomeClockTickerTests.cs`

**Interfaces:**
- Produces: `HomeClockTicker : IDisposable` with `void Register(Action tick)` / `void Clear()` / starts a single `DispatcherTimer` (guarded `Application.Current is not null`, like `LiveElapsedClock`); on each tick invokes all registered callbacks. Rows compute their compact display via `DurationFormat` (no seconds in-row: format like `20g 4:12`).
- Consumes: `IClock`, `DurationFormat` (`src/Recordum.WPF/Formatting/`).

- [ ] **Step 1: Failing test** — headless (no Application): `HomeClockTicker` with a fake/manual tick invokes all registered callbacks once per tick; `Clear`/`Dispose` stops fan-out. Verify a Resistance row's `KeyFigureText` updates from a given `AbstinenceOpenSinceMs` + advancing `FakeClock` to the expected `20g 4:12`-style compact string (add a `DurationFormat.FormatCompactDays` helper if one doesn't exist — check `DurationFormat` first).

- [ ] **Step 2: Implement** the ticker + row hook. The list VM registers each resistance row's refresh with the single ticker after build and calls `Clear()`/`Dispose()` on reload/teardown (avoid the leak that `LiveElapsedClock`'s doc warns about). Reuse the compact formatter.

- [ ] **Step 3: Run tests + full WPF suite. Commit.**

---

## Task 5: XAML — key-figure chip + strip + Configure "Riga Home" section + F0-11

Render the chip + strip in the row, add the two-knob Configure section, and clean the Configure header's raw-enum ComboBoxes.

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitRowTemplate.xaml` (add key-figure chip col + make the strip length-aware)
- Modify: `src/Recordum.WPF/Views/HeatmapStrip.xaml.cs` (already length-agnostic — verify it renders 14; it iterates `Counts.Length`, so binding `StripCounts` works)
- Create: a "Riga Home" editor section (VM + view) following the existing editor-section pattern (`src/Recordum.WPF/ViewModels/HabitEditor/Sections/*` + their views) — two localized ComboBoxes bound to key-figure and strip choices, persisting via `IHabitUiPrefsService.SetAsync`.
- Modify: `src/Recordum.WPF/Views/EditorPane.xaml` (lines ~64-72) + its VM — **F0-11**: when configuring an **existing** habit (not creating), replace the raw `AvailableNodeTypes`/`AvailableHabitTypes` ComboBoxes with localized, non-editable type + name text (type is immutable post-creation). Keep the selectors only in the create-new flow.
- New strings for the section labels + key-figure/strip option names.
- Tests: section VM tests (`tests/Recordum.WPF.Tests/...`) for persistence; a row smoke test that the chip binds.

**Interfaces:**
- Consumes: `HomeRowViewModel.KeyFigureText`/`ShowKeyFigure`/`StripCounts`/`StripLength` (Task 3), `IHabitUiPrefsService` (write knobs).
- Produces: the "Riga Home" section VM (`HomeRowSectionViewModel` — mirror an existing section VM's shape: load current prefs, expose two `ObservableCollection` option lists + selected items, persist on change).

- [ ] **Step 1: Row chip + strip.** Add a key-figure `TextBlock`/chip column in `HabitRowTemplate.xaml` between the type label and the strip, bound to `KeyFigureText` with `ShowKeyFigure` visibility (tinted with the habit color per the design language; no emoji in chrome). Bind the strip to `StripCounts` (7 or 14). Add a WPF smoke test that a Resistance row renders a non-empty `KeyFigureText`.

- [ ] **Step 2: Configure "Riga Home" section.** Create the section VM + view following an existing section (open `src/Recordum.WPF/ViewModels/HabitEditor/Sections/` and a matching view; copy the registration into the sections host — find how sections are listed/hosted). Two ComboBoxes: key figure (options filtered by the habit's type per `HomeRowPresentation`'s allowed set) and strip (None/7/14). On selection, write `HabitUiPrefs` via `IHabitUiPrefsService.SetAsync` (merge with existing prefs — read, copy `with`, set). Test: selecting a key figure persists it and re-reading returns it.

- [ ] **Step 3: F0-11.** In the Configure (existing-habit) header, replace the two raw-enum ComboBoxes with localized non-editable text (type via a `HabitType`→localized-label converter that already exists — `HabitTypeLabel`; name as text). Keep the ComboBoxes only for the create-new path (guard by an `IsCreating`/`IsExistingHabit` VM flag — check the EditorPane VM for an existing mode flag). Test/verify the create path still shows selectors.

- [ ] **Step 4: Strings** (EN+IT+Designer via shell) for section title, key-figure option labels, strip option labels. **Step 5:** Full WPF suite green. **Step 6:** Commit.

---

## Execution status (2026-07-10)

All 5 tasks implemented + committed on `wave-2-1-home-row` (commits `7b5783a` → `3e59986`):
Task 1 (query fields + ScaleTemplate label fallback), Task 2 (`HomeRowPresentation`),
Task 3 (row VM key figure/strip/lazy streak), Task 4 (`HomeClockTicker`), Task 5
(row chip+strip XAML, "Home row" Configure section, F0-11). Full solution builds 0 errors;
Data 411 / WPF 739 green; `HomeTreeBudgetTests` green.

**Deviation (author-visible):** `StateAverage7` ("7d avg") is **deferred** — the plan pointed its
lazy compute at `IStreakAggregator`, which can't produce a state average, and Task 1 (perf-budgeted,
committed) added no 7-day state-average field. It's dropped from State's *offered* key figures;
the `KeyFigureKind` value + id mapping are kept for forward-compat. Re-list it once a cheap backing
aggregate exists. State default (current value label) is fully backed and works.

**Remaining:** author on-screen verification per type (below) — this closes the wave (`verified-fixed`).

## Final verification (after all tasks)

- [x] Full Data + WPF suites green; `HomeTreeBudgetTests` green (budget held).
- [ ] Build the app and **have the author verify on screen** per type: Action streak chip fills in after render; Counter today-total with unit; Resistance live clock ticking in-row; Journal ✓/entries; State scale label; strip shows 7 vs 14 per the knob; Configure "Riga Home" knobs persist; Configure header no longer shows raw `Habit`/`Action` enums for an existing habit. This visual pass closes the wave (`verified-fixed`).

## Out of scope (this sub-plan)

The "Today" strip above the columns + per-type default columns (U10) = **Onda 2.2**. Registration/repeatable/series chips (U8) = **2.3**. Journal note-first gesture (U7) = **2.4**. Builder 3-tier preview = **2.5**. Calendar selection-vs-completed = 2.2. No group chips/strips.
