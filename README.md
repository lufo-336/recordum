# Recordum

> *Record your existence.*

Recordum is an **open-source, offline-first, cross-platform habit editor**. It rejects the
reductive *done / not-done* model: it is built for people tracking chronic health, training,
writing, abstinence, and anything else that doesn't fit a checkbox.

Habits come in **five composable types** — `action`, `counter`, `resistance`, `journal`, `state` —
and nest into hierarchical groups, so complex needs are expressed by *composing simple habits*
rather than by bloating a single habit with options.

The current shipping target is a **Windows desktop app (WPF)**. The domain, data, and plugin layers
are UI-agnostic and are intended to be reused unchanged by future mobile (MAUI) and sync work.

---

## Principles (non-negotiable)

1. **Offline-first.** Your data lives on your device. Cloud sync is optional and goes through
   third-party providers — never a proprietary server.
2. **No mandatory account.** An account is only needed for optional sync.
3. **Privacy by default.** AI is disabled on first run. Data never leaves the device without
   informed consent.
4. **Complexity through composition**, not through options piled onto one habit.
5. **Depth without intimidation.** Basic use in 30 seconds; advanced options exist but stay out of
   the way (progressive disclosure).
6. **Open source.** No paywall on core features.
7. **English-first.** All user-facing strings live in `.resx` (English primary, Italian secondary).

---

## Tech stack

| Area | Technology |
|---|---|
| Language / runtime | C# / .NET 10 |
| Desktop UI | WPF + [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) |
| Persistence | EF Core + SQLite, encrypted with **SQLCipher** (`SQLitePCLRaw.bundle_e_sqlcipher`) |
| Markdown editor | WebView2 + Toast UI Editor (vendored) |
| PDF export | [QuestPDF](https://www.questpdf.com/) |
| Charts | LiveCharts2 |
| Tests | xUnit + FluentAssertions |

The database is **encrypted at rest**. The master key always comes from an injected
`IDbKeyProvider` (DPAPI by default, opt-in Argon2id passphrase) — it is never hardcoded.

---

## Solution layout

Five projects under `src/` (see [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the full picture):

| Project | Responsibility |
|---|---|
| **Recordum.Core** | Domain models and abstractions (`IClock`, `IPluginContext`, service interfaces). Pure C#, **zero UI/EF dependencies.** |
| **Recordum.Data** | `RecordumDbContext`, Fluent configurations, migrations, service implementations. References Core. |
| **Recordum.Plugins.Contracts** | Host interfaces, manifest records, permission / extension-point / event IDs. |
| **Recordum.Plugins** | Plugin runtime: `PluginManager`, in-process host, `EventBus`, extension-point registry, API router. |
| **Recordum.WPF** | The desktop app: Views, ViewModels (MVVM), and `Bootstrap/` (DI, startup). |

Core / Data / Plugins are meant to be reused **unchanged** by later platforms.

Tests live under `tests/` (`Recordum.Data.Tests`, `Recordum.WPF.Tests`).

---

## Getting started

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows (the desktop app
and SQLCipher are Windows-targeted).

```bash
# Restore + build the whole solution
dotnet build

# Run the desktop app
dotnet run --project src/Recordum.WPF

# Run the tests
dotnet test tests/Recordum.Data.Tests
dotnet test tests/Recordum.WPF.Tests
```

> The build RID is pinned to `win-x64` (`SelfContained=false`) in `Directory.Build.props` to keep the
> working folder small — see the note there. The app stores its encrypted DB, key files, attachments,
> and backups under `%LocalAppData%\Recordum`.

There is a known transitive advisory (`NU1903`, CVE-2025-6965 in `e_sqlite3 ≤ 2.1.11`) with **no
upstream fix yet**; it is intentionally downgraded to a visible warning and will be removed once
SQLitePCLRaw ships SQLite ≥ 3.50.2.

---

## Project status

The project is in **Phase 4 — stabilization toward Desktop v1.0**: a solid core, working data
exploration (heatmaps, charts, streaks), export, backup/restore, and a pleasant UX. AI and the web
front-end are intentionally out of scope until the desktop is a polished v1.0.

The authoritative issue tracker is [`docs/phase-4-issues.md`](docs/phase-4-issues.md); milestone
history is in [`docs/MILESTONES.md`](docs/MILESTONES.md).

---

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — how the pieces fit together (start here).
- [`CLAUDE.md`](CLAUDE.md) — the dense project-context digest (invariants, schema, conventions).
- [`docs/`](docs) — design specs and plans (`docs/superpowers/`), decisions
  ([`docs/decisions/`](docs/decisions)), the plugin API (`docs/plugin-api.md`), and UI design
  mockups (`docs/design/`).

---

## Contributing

- **MVVM** in `Recordum.WPF` via `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`).
- **Dependency injection everywhere**; never read the clock statically — inject `IClock`.
- **User-facing strings** go in `.resx` (English first, Italian second) — never hardcoded.
- **Cascade rules and indexes** are always declared explicitly in Fluent API, never left to EF defaults.
- **Tests** accompany behavioral changes (TDD is the norm).
- EF migrations: `dotnet ef migrations add <Name> --project src/Recordum.Data --startup-project src/Recordum.WPF`
  — verify the generated SQL against the schema docs before applying. Never hand-edit the generated
  `*.Designer.cs` / `*ModelSnapshot.cs` files.

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and [`CLAUDE.md`](CLAUDE.md) for the deeper
conventions.

---

## License

Recordum is released under the **MIT License** — SPDX `MIT`. See [`LICENSE`](LICENSE). You may use,
copy, modify, and distribute the software freely, provided the copyright notice and permission notice
are kept; it comes with no warranty.
