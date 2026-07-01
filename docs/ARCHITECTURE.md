# Architecture

This document explains how Recordum is put together, for someone reading the codebase for the first
time. It complements [`../CLAUDE.md`](../CLAUDE.md) (the dense invariants digest) with a narrative
overview.

## The big picture

Recordum is a layered .NET solution. The lower layers know nothing about the UI, so they can be
reused unchanged by future front-ends (mobile, web, sync).

```
Recordum.WPF ──────────────► the Windows desktop app (Views + ViewModels + DI bootstrap)
     │
     ├──► Recordum.Data ────► EF Core + SQLite/SQLCipher: DbContext, migrations, service impls
     │        │
     │        └──► Recordum.Core ─► domain models + abstractions (interfaces, IClock). Pure C#.
     │
     └──► Recordum.Plugins ─► plugin runtime (in-process host, event bus, extension points)
              │
              └──► Recordum.Plugins.Contracts ─► the stable plugin-facing surface. ─► Core
```

**Dependency rule:** dependencies point *downward* only. `Recordum.Core` has no dependency on EF,
WPF, or anything else — it is the shared vocabulary. `Recordum.Data` implements Core's service
interfaces against EF Core. `Recordum.WPF` composes everything via dependency injection.

## Layer by layer

### Recordum.Core — the domain
Plain C# domain models and the abstractions everything else depends on. Two abstractions worth
knowing immediately:

- **`IClock`** — the *only* source of time. Nothing anywhere calls `DateTime.Now`/`UtcNow` directly;
  this keeps behavior deterministic and testable (tests inject a `FakeClock`).
- **Service interfaces** (`IHabitNodeService`, `ICompletionService`, `IAbstinenceService`, …) —
  implemented in `Recordum.Data`, consumed by ViewModels.

### Recordum.Data — persistence
EF Core over SQLite, **encrypted with SQLCipher**. Highlights:

- **`RecordumDbContext`** with Fluent configurations. All cascade rules and indexes are declared
  **explicitly** — never left to EF defaults.
- **Migrations** are the source of truth for the schema (currently **v2.12**). Add one with
  `dotnet ef migrations add <Name> --project src/Recordum.Data --startup-project src/Recordum.WPF`,
  and verify the generated SQL against the schema docs before applying. The generated
  `*.Designer.cs` / `*ModelSnapshot.cs` files are **never** hand-edited (a repo hook enforces this).
- **`TimestampInterceptor`** auto-populates `CreatedAt` / `UpdatedAt` / `UpdatedAtMs` (and plugin
  provenance) on save, so services don't set timestamps by hand.
- **Soft delete** is a global query filter on `HabitNode` (`DeletedAt == null`). Use
  `IgnoreQueryFilters()` for Trash / diagnostics.
- The **encryption key** comes from an injected `IDbKeyProvider` (DPAPI by default, opt-in Argon2id
  passphrase). Losing a passphrase means losing the data — there is no server-side recovery.

### Recordum.Plugins(.Contracts) — extensibility
First-party features (the chart engine, the export engine) are themselves plugins that register
capabilities through extension points, rather than reaching into the core directly. `Contracts`
is the stable surface plugins compile against; `Recordum.Plugins` is the runtime (in-process host,
`EventBus`, `ExtensionPointRegistry`, API router). A sandboxed WebView2 host is planned for later.

### Recordum.WPF — the app
MVVM with `CommunityToolkit.Mvvm`. `Bootstrap/` wires up DI, applies migrations at startup
(`ApplicationInitializer`), and shows a localized fatal dialog on failure. Views bind to ViewModels;
ViewModels depend only on Core interfaces.

## The domain model

Everything is a **`HabitNode`** — both habits and groups — arranged in a recursive tree via
`parent_id`.

- A **group** aggregates children and can carry its own goal; it is not directly completable.
- A **habit** has a `habit_type` and its own completion history, config, streak, goal, and
  notifications. The five types:

  | Type | Meaning |
  |---|---|
  | `action` | binary done / not-done |
  | `counter` | occurrences / measured values; supports named session segments |
  | `resistance` | **abstinence** — see below |
  | `journal` | a dated writing habit (Markdown notes) |
  | `state` | a value on a scale (e.g. mood 1–5) |

**Abstinence (`resistance`) is an explicit-state model** (added July 2026). Instead of completions,
it uses the `abstinence_period` table — one row per attempt (`started_at_ms` + optional
`ended_at_ms`) — behind `IAbstinenceService` (Start / Lapse / DeletePeriod / GetState / ListPeriods).
The current state is *derived*: an open period ⇒ *Abstaining*; the most recent closed one ⇒
*Relapsed*; none ⇒ *NotStarted*. The streak engine, the quick-add (with its live stopwatch), the Home
row, the calendar, and the Cronologia "records" list all read from these periods.

Other domain concepts to know: **config versioning** (every `HabitConfig` change snapshots into
`HabitConfigHistory`), the **`reset_time` cascade** (a habit's streak day-boundary falls back through
ancestor groups to a global default), and **scales** (a local `MetricDefinition.labels_json` overrides
a shared `ScaleTemplate` — CSS-style specificity; the stored value is always the integer).

## The desktop UI shape

`Recordum.WPF` is organized around a **workspace shell**: a home tree on the left and a habit screen
made of toggleable side-by-side **columns** (Panoramica, Diario, Cronologia, Dettaglio, Note,
Configura). Each column is a focused ViewModel:

- **Cronologia** — calendar + quick-add + the "records" list. For abstinence habits the records list
  shows period cards (live clock for the open period, elapsed interval + relapse date for closed
  ones, delete-with-confirmation).
- **Dettaglio** — year heatmap, streak summary + timeline, trend chart, hourly distribution, goal
  progress. Visualizations that don't apply to a given type are hidden (e.g. completion charts for
  abstinence).

Cross-cutting UI concerns: theming (light/dark), an `IClock`-driven live-elapsed clock helper reused
across the quick-add / detail / records views, and an `EventBus` that lets one mutation refresh the
other columns.

## Conventions cheat-sheet

- Time only via `IClock`; a `DispatcherTimer` is created only when a WPF app is actually running
  (so headless unit tests stay deterministic).
- User strings in `.resx` (English first). Adding one = an entry in `Strings.resx` + `Strings.it.resx`
  + one accessor line in `Strings.Designer.cs` (this designer is hand-maintained, not codegen).
- DI everywhere; ViewModels take interfaces, never concrete services.
- Tests accompany behavioral changes; the data layer and streak engine are covered against a real
  in-memory database, not mocks.

For the exhaustive, always-current list of invariants (schema deltas, provenance rules, build/RID
notes, hooks), read [`../CLAUDE.md`](../CLAUDE.md).
