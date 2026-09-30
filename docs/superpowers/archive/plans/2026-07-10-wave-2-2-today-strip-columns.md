# Onda 2.2 — U10 fascia "Oggi" + colonne per-tipo — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the habit screen never show "nothing here": add an always-visible per-type **"Oggi" strip** above the column workspace, give each subject **per-type default columns** on first open that then **persist per-habit** (in `ui_prefs_json`), and fix the calendar so a **selected** day (ring) is never confused with a **completed** day (fill).

**Architecture:** The "Oggi" strip is a new control (`TodayStripView` + `TodayStripViewModel`) hosted at the top of `HabitScreen.xaml`, above the column chip bar, bound to `HabitScreenViewModel.TodayStrip`. It shows a per-type content VM chosen from the subject's `HabitType` — each content VM **reuses existing pieces** (the per-type quick-add VMs, `LiveElapsedClock`, `DetailViewModel`'s streak/goal data, `IAbstinenceService`) rather than new engines. Column open/close state moves from a single global AppSetting to **per-habit** `HabitUiPrefs.OpenColumns` (Onda 2.0), with the legacy global `ui.open_columns` kept only as a fallback for habits that have no prefs yet. The calendar day-cell stops using an accent **fill** for selection and uses a **ring** instead.

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm, EF Core + SQLite, xUnit.

## Global Constraints

- **English-first strings = 3 edits each:** add the key to `src/Recordum.WPF/Resources/Strings.resx` (EN) + `Strings.it.resx` (IT) + a manual accessor property in `src/Recordum.WPF/Resources/Strings.Designer.cs`. The `guard-generated.js` hook denies Edit/Write on `*.Designer.cs` (it matches `Strings.Designer.cs` too) → **edit `Strings.Designer.cs` only via the Bash shell** (`sed`/heredoc). This VM has **no python** — use `sed`. English-first.
- **`IClock` for time, never `DateTime.Now`.** Reuse the existing `LiveElapsedClock` (`src/Recordum.WPF/ViewModels/HabitScreen/LiveElapsedClock.cs`) for any live clock; dispose it on teardown/reload (it roots a `DispatcherTimer`).
- **No new schema / no migration.** All persistence uses `HabitUiPrefs.OpenColumns` via `IHabitUiPrefsService` (Onda 2.0). `ui_prefs_json` governance: any new key must be documented in `docs/ARCHITECTURE.md` (key→meaning→wave table) — but this wave adds **no new key** (`OpenColumns` already exists).
- **Analyzer rules are errors, not warnings** (seen this wave chain): `CA1859` (use concrete dict types on private fields), `CA1863` (`#pragma warning disable CA1863` around low-frequency `string.Format` with resx, matching the codebase precedent), `CA1861` (hoist constant arrays to `static readonly`). Fix at the call site.
- **Build/test:** `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo` and `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo`. WPF tests run on Windows only (this VM). **If the app is running it locks `Recordum.WPF.dll`** — `taskkill //F //IM dotnet.exe` is too broad; kill the specific `.NET Host` PID named in the MSB3021 error before rebuilding.
- **Branch:** feature branch `wave-2-2-today-strip-columns` off `main` **after Onda 2.1 is merged** (2.2 builds on 2.1's `HomeRowPresentation`, `IHabitUiPrefsService` wiring, and the per-type Home row). The `guard-main.js` hook blocks direct commits to `main`.
- **Author visual verification closes this wave** (state `verified-fixed`), per type, on all four surfaces touched (E6/E7) — not just green tests.

## New facts from the 2026-07-11 interactive audit (READ BEFORE Task 1 — they change the code you touch)

1. **`HabitScreenViewModel` now owns the Configura editor load** (commit `9af35e0`, regression fix — the wiring was lost in the workspace shell and the column rendered an empty editor):
   - `OnIsConfiguraOpenChanged` calls `_ = LoadEditorAsync()` when the flag turns on outside `_restoringColumns`.
   - `LoadAsync` builds `var editorTask = IsConfiguraOpen ? Editor.LoadAsync(node.Id, ct) : Task.CompletedTask;` and awaits it in the `Task.WhenAll` batch.
   - **Task 1 refactors exactly this area.** Your `RestoreColumnsForSubjectAsync(node, ct)` MUST run **before** the `editorTask` line is evaluated, so `IsConfiguraOpen` already reflects the restored per-habit set when the editor decides to load (during restore the flag handler is suppressed by `_restoringColumns` — the `editorTask` in `LoadAsync` is what loads the editor for a restored-open Configura).
   - **Regression gate:** `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenEditorWiringTests.cs` (3 tests: open-loads, already-open-loads, switch-reloads) must stay green. Run it explicitly after Task 1 Step 2.
2. **Never `Clear()` a bound ItemsSource while row/selection handlers can persist** (data-wipe bug, commit `4d01f9c`): emptying a ComboBox's ItemsSource coerces `SelectedValue` to null through two-way bindings and the handler wrote that null to the DB (Mood lost its scale, counters lost units). Pattern: cover the `Clear()` with the `_loading` guard, or detach handlers before clearing (see `ScalesSectionViewModel`/`MetricsSectionViewModel`). Applies to any VM this wave that swaps lists under bound selectors (Task 7 State slider bounds, Task 6 goal data).
3. **Column-hosted content must respect the column width** (audit finding A8): the editor sections overflow the Configura column (a 600px-wide ComboBox ends past the window edge). `TodayStripView` (Task 3) and every per-type content view must stretch/wrap to the available width — no fixed widths above ~300px, test at a 1280px window with 2+ columns open.
4. **Branch state**: `wave-2-1-home-row` now also carries `4d01f9c` (wipe fix), `9af35e0` (editor wiring), `0f4f637` (chart builder seeds one series row — W1a-VF2). WPF suite **744/744**, Data **411/411**. Wave 2.2 still branches off `main` **after** 2.1 merges.
5. **Interactive verification tooling exists** (scratchpad scripts from the audit, recreate if needed): PowerShell + UIA can drive the running app — `SetProcessDPIAware()` first (125% scaling breaks coordinates otherwise), prefer UIA patterns (`InvokePattern`/`TogglePattern`/`SelectionItemPattern`/`ExpandCollapsePattern`) over synthetic mouse clicks (clicks on the Home tree were unreliable in the audit session — open finding A11); the row "+" button (UIA Invoke) reliably opens a subject tab; capture the window with `Graphics.CopyFromScreen` on `GetWindowRect` bounds after `SetForegroundWindow`. Useful for the per-type visual pass in the Final verification.
6. **Known-open visual findings you will see while testing (do NOT chase them, they are tracked)**: A4 trend/hourly on white cards in dark theme + Mood trend empty (extends W1a-VF1, Onda 3); A5 implausible year heatmap for Mood; A6 redundant type word in the Home row right zone; A7 inconsistent right-zone truncation in the narrow tree. Tracker: `docs/phase-4-issues.md` §"Audit interattivo 2026-07-11".

## Environment / Interface facts (verified 2026-07-10)

- `HabitScreenViewModel` (`src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`) owns the column workspace: `HabitColumn` enum = `{ Cronologia, Dettaglio, Note, Configura, Panoramica, Diario }`. Open flags are `[ObservableProperty] _isCronologiaOpen` etc.; `SetColumnFlag(HabitColumn, bool)`; `CanonicalOpen()` yields the open set; persistence today = global AppSetting `ui.open_columns` via `PersistOpenColumnsAsync()`/`RestoreOpenColumnsAsync()`. **Default-column fallback already exists** in `LoadAsync` (lines ~490-499): group→Panoramica, journal→Diario, else Cronologia, preferring `_lastClosedColumn`.
- `LoadAsync(Guid?, ct)` loads the subject and its child column VMs. `Node` is the loaded `HabitNode`. The per-type quick-add is built in `BuildQuickAddAsync` and assigned to `QuickAdd`.
- Per-type quick-add VMs (all `: QuickAddViewModelBase`, ctor deps shown): `ActionQuickAddViewModel(_completions, _clock)`, `CounterTapQuickAddViewModel(_completions,_clock)` / `CounterSeriesQuickAddViewModel(_completions,_metrics,_refData,_clock,_timers)`, `AbstinenceQuickAddViewModel(_completions,_abstinence,_clock)`, `JournalQuickAddViewModel(_completions,_clock,_diary)`, `MeasuredQuickAddViewModel(_completions,_metrics,_refData,_clock,_timers)` (used for State). `QuickAddViewModelBase` exposes `DateTime CompletionDate` and `abstract Task SaveAsync(ct)`.
- `AbstinenceQuickAddViewModel` already exposes `AbstinenceStatus Status`, `bool CanStart`/`CanLapse`, `StartCommand`/`LapseCommand`, a live-clock display, `event Changed`, and `Task LoadAsync(ct)`. It is the model for the Resistance "Oggi" content.
- `DetailViewModel` (`.../Detail/DetailViewModel.cs`, already constructed in `HabitScreenViewModel`) holds the streak/goal/trend cards used by the Dettaglio column — reuse its computed streak/goal for Action/Counter "Oggi" rather than recomputing.
- `HabitUiPrefs` (`Recordum.Core.Models`): `KeyFigure` string?, `Strip` HomeStrip?, `OpenColumns` IReadOnlyList<string>?, `JournalGesture` JournalPrimaryGesture?. `IHabitUiPrefsService.GetAsync(id)`/`SetAsync(id, prefs)`. **Known footgun (from 2.0 memo):** `OpenColumns` value-equality is reference-equality on the list — always replace the whole list with `prefs with { OpenColumns = newList }`, never mutate in place.
- Calendar day-cell (`CalendarDayCellViewModel`): `bool IsSelected`, `int TintStep` (completion intensity), `bool IsToday`, `string TintColorHex`. The XAML (`Views/HabitScreen/Calendar/CalendarNavigator.xaml`) currently sets the **selected** cell's `Background` to `Brush.Accent` (same fill as a completed/tinted cell) — the F0-13 ambiguity.
- Section registration pattern (for reference) and the `run` skill for launch are unchanged from 2.1.

## Per-type "Oggi" matrix (spec §1, defaults bold)

| Type | "Oggi" strip content |
|---|---|
| Action | ✓ **Segna come fatto** (toggle for today) · current streak · % done last 30d · usual hour |
| Counter | **+ Registra** (focuses the record module) · today total · goal progress if a goal is active |
| Resistance | **large live clock** (`LiveElapsedClock`) · Inizio/Ricaduta · attempts in last 90d |
| Journal | primary gesture: **Scrivi la voce di oggi** *or* **+ Nota veloce** (from `HabitUiPrefs.JournalGesture`; default per template/type) |
| State | quick **slider** for today · last recorded value |

Groups: no "Oggi" strip (out of scope).

---

## Task 1: Per-habit column persistence + per-type default columns

Move the open-column set from the single global AppSetting to per-habit `HabitUiPrefs.OpenColumns`, keeping the global as a legacy fallback. On first open of a subject with no prefs, apply the per-type default; thereafter persist the subject's own set.

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (inject `IHabitUiPrefsService`; per-subject restore/persist)
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` (only if the ctor change needs it — `IHabitUiPrefsService` is already registered; DI resolves the new param)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenColumnsTests.cs` (new or existing columns test file)
- Test fixtures: `tests/Recordum.WPF.Tests/Fixtures/FakeHabitUiPrefsService.cs` (exists from 2.1), `HabitScreenViewModelFactory.cs` (pass the fake)

**Interfaces:**
- Consumes: `IHabitUiPrefsService.GetAsync/SetAsync`, `HabitUiPrefs.OpenColumns` (list of `HabitColumn.ToString()` tokens), the existing `SetColumnFlag`/`CanonicalOpen`.
- Produces: per-subject column persistence. Add `internal async Task RestoreColumnsForSubjectAsync(HabitNode node, CancellationToken ct)` and change `OnColumnFlag`/persist to write the current subject's prefs.

- [ ] **Step 1: Failing tests.** In `HabitScreenColumnsTests.cs`, with `FakeHabitUiPrefsService`:
  - Loading a **journal** habit with **no** prefs opens Diario by default; a non-journal habit opens Cronologia (assert `ShowDiario`/`ShowCronologia`). (Locks the existing per-type default under the new path.)
  - After opening Dettaglio on habit A and switching to habit B then back to A, A shows Dettaglio again (per-habit persistence): assert the fake's `Store[A.Id].OpenColumns` contains `"Dettaglio"` and that re-loading A restores it.
  - A habit whose prefs are null but the legacy global `ui.open_columns` = "Cronologia,Dettaglio" restores those (legacy fallback) — only when the subject has never persisted its own set.

- [ ] **Step 2: Implement.** Inject `IHabitUiPrefsService uiPrefs` into `HabitScreenViewModel` (append to ctor; update `HabitScreenViewModelFactory` + any direct test ctor). In `LoadAsync`, after `Node` is set and before the child loads, call a new `RestoreColumnsForSubjectAsync(node, ct)` that:
  1. reads `HabitUiPrefs.OpenColumns`; if non-empty, `_restoringColumns=true`, clear current flags, set each parsed `HabitColumn`, `_restoringColumns=false`;
  2. else if the legacy global `ui.open_columns` is set, use it (existing `RestoreOpenColumnsAsync` logic, extracted);
  3. else apply the per-type default (existing fallback block).
  Change `OnColumnFlag` (and remove/guard the global `PersistOpenColumnsAsync` for subjects) so persistence writes `_uiPrefs.SetAsync(Node.Id, prefs with { OpenColumns = CanonicalOpen().Select(c => c.ToString()).ToList() })`. Keep writing the global too (cheap, keeps legacy fallback warm for brand-new habits). Guard all writes with `!_restoringColumns` and `Node is not null`.

  **⚠ Sequencing (audit fact 1):** `LoadAsync` now contains `var editorTask = IsConfiguraOpen ? Editor.LoadAsync(node.Id, ct) : Task.CompletedTask;` in the `WhenAll` batch — your restore must run **before** that line so a restored-open Configura loads its editor. After implementing, run `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~HabitScreenEditorWiringTests"` → 3/3 green is a hard gate.

- [ ] **Step 3: Run tests + full WPF suite. Commit** (`feat(wpf): persist open columns per-habit in ui_prefs, per-type defaults`).

---

## Task 2: Calendar — selected (ring) vs completed (fill) — F0-13

Distinguish the selected day from completed days so a green habit color never makes them ambiguous.

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml` (month + week + year cell styles: the `IsSelected` triggers)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/*` — the VM already sets `IsSelected`/`TintStep`; add/confirm a VM test that selecting a completed day keeps `TintStep>0` **and** `IsSelected==true` (both signals independent). The visual distinction is asserted by the author (XAML), the VM test guards the data.

**Interfaces:**
- Consumes: `CalendarDayCellViewModel.IsSelected`, `.TintStep`, `.IsToday`, `.TintColorHex`.
- Produces: selected cell renders as a **ring** (BorderBrush accent, thickness 2, no background fill override) so the tint fill underneath stays visible; today stays a lighter ring; completed stays the tint fill.

- [ ] **Step 1: VM test.** Assert that a day with completions that is also selected exposes `TintStep > 0` and `IsSelected == true` simultaneously (no mutual exclusion in the VM). Run the calendar test filter → PASS (likely already true; this locks it).

- [ ] **Step 2: XAML.** In the month cell `Style.Triggers`, replace the `IsSelected` trigger's `Background=Brush.Accent` with `BorderBrush=Brush.Accent` + `BorderThickness=2` (ring), leaving the tint `Border` fill intact. Make `IsToday` a subtler ring (e.g. 1px accent) that the selected ring overrides. Apply the same change to the week and year cell styles (they have parallel `IsSelected` triggers). Keep the `ContrastForeground` text only where a fill still applies.

- [ ] **Step 3: Run calendar tests + full WPF suite. Commit** (`fix(calendar): selected day is a ring, completed is a fill (F0-13)`).

---

## Task 3: TodayStrip host — always-visible, per-type routing

A new strip at the top of the habit screen, above the column chip bar, always visible for a habit subject (even with zero columns open). It selects a per-type content VM; content VMs arrive in Tasks 4-8.

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Today/TodayStripViewModel.cs`
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Today/ITodayContent.cs` (marker base for per-type content VMs)
- Create: `src/Recordum.WPF/Views/HabitScreen/Today/TodayStripView.xaml` (+ `.xaml.cs`) and a `TodayContentTemplates.xaml` ResourceDictionary (VM→View DataTemplates, like `SectionTemplates.xaml`)
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (own `TodayStrip`, populate it in `LoadAsync`, refresh on the same events that reload columns)
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (host `TodayStripView` in a new top row, above the chip bar; visible when `HasNode && !IsGroupNode`)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Today/TodayStripViewModelTests.cs`

**Interfaces:**
- Produces: `TodayStripViewModel` with `[ObservableProperty] ITodayContent? Content` and `Task LoadAsync(HabitNode node, CancellationToken ct)` that builds the per-type content (a `switch` on `node.HabitType`). Marker interface `ITodayContent` (empty; each content VM implements it). `HabitScreenViewModel.TodayStrip { get; }` (constructed once, `LoadAsync`'d per subject); `bool ShowTodayStrip => HasNode && !IsGroupNode`.
- Consumes: `HabitType`, the content VMs from Tasks 4-8 (until those land, the `switch` returns `null` for every type — the host renders empty but is present).

- [ ] **Step 1: Failing test.** `TodayStripViewModel.LoadAsync` on a habit sets `Content` to the matching content type once Tasks 4-8 exist; for Task 3 alone, assert `LoadAsync` runs without throwing and `Content` is null for a type with no content yet, and that `HabitScreenViewModel.ShowTodayStrip` is true for a habit, false for a group and when `Node` is null.

- [ ] **Step 2: Implement host.** Create `ITodayContent`, `TodayStripViewModel` (ctor takes the services the content VMs need — pass them through from `HabitScreenViewModel`; simplest: give `TodayStripViewModel` a delegate/factory it calls per type, or construct content inline in `LoadAsync` from injected services). Add `TodayStrip` to `HabitScreenViewModel`, call `TodayStrip.LoadAsync(node, ct)` inside `LoadAsync` (in the `Task.WhenAll` batch) and on the reload paths (`ReloadColumnsAsync`/completion+abstinence events). Add `ShowTodayStrip` + notify it in `NotifyShowFlags`.

- [ ] **Step 3: XAML host.** Add a top `RowDefinition` to the `HabitScreen.xaml` grid; place `<today:TodayStripView DataContext="{Binding TodayStrip}" Visibility="{Binding ShowTodayStrip, Converter={StaticResource BoolToVisible}}"/>` above the chip bar. `TodayStripView` is a `ContentControl Content="{Binding Content}"` with merged `TodayContentTemplates.xaml`. Ensure the strip is **outside** the horizontal column ScrollViewer so it stays put and shows even at zero open columns. **⚠ Width (audit fact 3):** the strip and every content view must stretch to the available width — no fixed widths >300px (the editor sections' 600px overflow is open finding A8; don't repeat it).

- [ ] **Step 4: Run tests + full WPF suite. Commit** (`feat(wpf): TodayStrip host above the habit columns (per-type routing)`).

---

## Task 4: Resistance "Oggi" content — the primary live clock

The flagship: the big abstinence clock + Inizio/Ricaduta + attempts(90d) lives in the strip. Reuse `AbstinenceQuickAddViewModel` + `LiveElapsedClock`.

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Today/ResistanceTodayContent.cs`
- Create: `src/Recordum.WPF/Views/HabitScreen/Today/ResistanceTodayView.xaml` (+ `.xaml.cs`) + DataTemplate in `TodayContentTemplates.xaml`
- Modify: `TodayStripViewModel` (`switch` arm for Resistance)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Today/ResistanceTodayContentTests.cs`
- New strings: `Today.Resistance.Attempts` = `"{0} attempts (90d)"` / `"{0} tentativi (90g)"`, plus reuse existing `Abstinence.*` strings for Start/Lapse.

**Interfaces:**
- Consumes: `IAbstinenceService` (`GetStateAsync`, `ListPeriodsAsync`), `IClock`, `LiveElapsedClock`.
- Produces: `ResistanceTodayContent : ITodayContent` exposing `LiveElapsedClock Clock` (Display bound in the view), `AbstinenceStatus Status`, `StartCommand`/`LapseCommand` (delegating to `IAbstinenceService`), `int AttemptsLast90Days`, `Task LoadAsync(HabitNode, ct)`, `IDisposable` (dispose the clock). Raises `event Changed` after Start/Lapse so the host reloads.

- [ ] **Step 1: Failing test.** With a `FakeAbstinenceService`: an open period → `Status==Abstaining`, `Clock.Display` non-empty; `StartCommand` when NotStarted calls `StartAsync`; `LapseCommand` when Abstaining calls `LapseAsync`; `AttemptsLast90Days` counts periods started within 90 days.

- [ ] **Step 2: Implement** the content VM (mirror `AbstinenceQuickAddViewModel`'s Start/Lapse/clock logic; compute attempts from `ListPeriodsAsync(id, today-90, today)`). Wire the Resistance arm in `TodayStripViewModel.LoadAsync`. Build `ResistanceTodayView` (large clock text, Inizio/Ricaduta buttons gated by CanStart/CanLapse, attempts caption). Register the DataTemplate. Dispose the clock on host reload.

- [ ] **Step 3: Run tests + full WPF suite. Commit** (`feat(wpf): Resistance Oggi content — primary live clock + Inizio/Ricaduta`).

---

## Task 5: Action "Oggi" content — done-today toggle + streak + %30d + hour

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Today/ActionTodayContent.cs`
- Create: `src/Recordum.WPF/Views/HabitScreen/Today/ActionTodayView.xaml` (+ `.xaml.cs`) + DataTemplate
- Modify: `TodayStripViewModel` (Action arm)
- Test: `.../Today/ActionTodayContentTests.cs`
- New strings: `Today.Action.MarkDone` = `"Mark as done"` / `"Segna come fatto"`, `Today.Action.Pct30` = `"{0}% last 30 days"` / `"{0}% ultimi 30 giorni"`, `Today.Action.UsualHour` = `"usually around {0}"` / `"di solito verso le {0}"`.

**Interfaces:**
- Consumes: `ICompletionService` (record/undo today's completion), `IStreakAggregator` (current streak, reuse the `IsCurrent` run pattern from 2.1's `HomeRowViewModel.LoadKeyFigureAsync`), `IHourlyDistributionQueryService` (usual hour), `IClock`.
- Produces: `ActionTodayContent : ITodayContent` with `bool IsDoneToday`, `ToggleDoneCommand` (records a completion for today when off, removes today's when on), `int CurrentStreak`, `int Pct30`, `string? UsualHour`, `Task LoadAsync(HabitNode, ct)`.

- [ ] **Step 1: Failing test.** `ToggleDoneCommand` from not-done records a completion and flips `IsDoneToday`; from done removes today's completion; `CurrentStreak` from a fake aggregator's `IsCurrent` run; `Pct30` computed from a 30-day completion count.

- [ ] **Step 2: Implement** + Action arm in `TodayStripViewModel` + view (a checkbox-style toggle + three stat captions) + DataTemplate.

- [ ] **Step 3: Run tests + full WPF suite. Commit** (`feat(wpf): Action Oggi content — done-today toggle + streak/30d/hour`).

---

## Task 6: Counter "Oggi" content — + Registra + today total + goal

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Today/CounterTodayContent.cs` + view + DataTemplate
- Modify: `TodayStripViewModel` (Counter arm); `HabitScreenViewModel` (expose a way to focus the record module — reuse `ToggleFocus(HabitColumn.Cronologia)` or open Cronologia)
- Test: `.../Today/CounterTodayContentTests.cs`
- New strings: `Today.Counter.Record` = `"+ Record"` / `"+ Registra"`. Reuse `Home.KeyFigure.CounterTotal` for the total.

**Interfaces:**
- Consumes: today counter total (query the primary metric's today sum — reuse `IHabitTreeQueryService`'s per-row value is Home-only; here query `ICompletionService`/`IMetricDefinitionService` directly, or reuse `DetailViewModel`'s goal card), `IGoalService` (active goal progress), `IClock`.
- Produces: `CounterTodayContent : ITodayContent` with `decimal TodayTotal`, `string? UnitCode`, `bool HasGoal`, `double GoalFraction`, `RecordCommand` (raises an event the host handles to open/focus the Cronologia record module), `Task LoadAsync(HabitNode, ct)`.

- [ ] **Step 1: Failing test.** `TodayTotal`/`UnitCode` from seeded today completions; `GoalFraction` from an active rolling goal; `RecordCommand` raises the focus-record event.

- [ ] **Step 2: Implement** + Counter arm + view + DataTemplate; host handles the focus-record event by ensuring Cronologia is open (and focused).

- [ ] **Step 3: Run tests + full WPF suite. Commit** (`feat(wpf): Counter Oggi content — + Registra, today total, goal progress`).

---

## Task 7: State "Oggi" content — quick slider + last value

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Today/StateTodayContent.cs` + view + DataTemplate
- Modify: `TodayStripViewModel` (State arm)
- Test: `.../Today/StateTodayContentTests.cs`
- New strings: `Today.State.LastValue` = `"last: {0}"` / `"ultimo: {0}"`.

**Interfaces:**
- Consumes: `MeasuredQuickAddViewModel` (already the State quick-add) or `ICompletionService`+`IMetricDefinitionService` to record a scale value; the scale labels (reuse the `ScaleTemplate`/metric `LabelsJson` resolution — same precedence as 2.1's state label). `IClock`.
- Produces: `StateTodayContent : ITodayContent` with `int SliderValue`, `int Min`/`Max`, `string CurrentLabel`, `string? LastValueLabel`, a command that records `SliderValue` for today (last-wins), `Task LoadAsync(HabitNode, ct)`.

- [ ] **Step 1: Failing test.** Slider bounds from the metric's scale; recording sets today's value (last-wins); `CurrentLabel` resolves via labels_json (metric override wins over `ScaleTemplate`).

- [ ] **Step 2: Implement** + State arm + view (a themed Slider — reuse the existing State slider style from the editor) + DataTemplate. **⚠ (audit fact 2):** if you swap scale/label lists under bound selectors, never `Clear()` while persist handlers are attached — guard with a loading flag or detach first (`4d01f9c` pattern). A State habit whose metric has `ScaleTemplateId == null` (pre-fix wiped data) must degrade gracefully (numeric bounds, no crash).

- [ ] **Step 3: Run tests + full WPF suite. Commit** (`feat(wpf): State Oggi content — quick slider + last value`).

---

## Task 8: Journal "Oggi" content — configurable primary gesture

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Today/JournalTodayContent.cs` + view + DataTemplate
- Modify: `TodayStripViewModel` (Journal arm)
- Test: `.../Today/JournalTodayContentTests.cs`
- New strings: `Today.Journal.WriteEntry` = `"Write today's entry"` / `"Scrivi la voce di oggi"`, `Today.Journal.QuickNote` = `"+ Quick note"` / `"+ Nota veloce"`.

**Interfaces:**
- Consumes: `HabitUiPrefs.JournalGesture` (Onda 2.0 enum `JournalPrimaryGesture { Diary, QuickNote }`; default = Diary unless the template/prefs set QuickNote), `IHabitUiPrefsService`. The primary button raises an event the host handles: Diary → open/focus Diario; QuickNote → open Note and start a quick note (full quick-note+photo flow is Onda 2.4 — here open the Note column focused).
- Produces: `JournalTodayContent : ITodayContent` with `JournalPrimaryGesture Gesture`, `PrimaryCommand`, `Task LoadAsync(HabitNode, ct)`.

- [ ] **Step 1: Failing test.** Gesture defaults to Diary with null prefs; `PrimaryCommand` raises the Diary-open event; with `JournalGesture=QuickNote` the label/command switches to the note path.

- [ ] **Step 2: Implement** + Journal arm + view (one primary button whose label/action follows the gesture) + DataTemplate; host handles the open events.

- [ ] **Step 3: Run tests + full WPF suite. Commit** (`feat(wpf): Journal Oggi content — configurable primary gesture (write/quick note)`).

---

## Final verification (after all tasks)

- [ ] Full Data + WPF suites green; `HomeTreeBudgetTests` green (untouched by this wave but confirm no regression).
- [ ] Build and **have the author verify on screen**, per type: the "Oggi" strip is always present for a habit (even with every column closed — the empty landing is gone); Resistance shows the big ticking clock + Inizio/Ricaduta + attempts; Action toggles done-today and shows streak/30d/hour; Counter shows total + goal and + Registra focuses the record module; State slider records today; Journal primary gesture follows Configura; columns reopen per-habit as last left; the calendar selected day is a ring, completed is a fill, unambiguous on a green habit. This visual pass closes the wave (`verified-fixed`).

## Out of scope (this sub-plan)

Registration internals (repeatable/summing/series chips/time-unit selector = U8) = **Onda 2.3**. The full journal note-first + photo quick-note flow = **Onda 2.4** (this wave only routes the gesture to the Note column). Builder 3-tier preview = **Onda 2.5**. Group "Oggi" strips, free widget grid in the row, unit conversions beyond h/m/s — all out (spec §9).
