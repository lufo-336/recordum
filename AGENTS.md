# Recordum — Root Scope

> *Record your existence.* — Editor di abitudini open-source, offline-first, multipiattaforma.

## Ambito

Recordum è un editor di abitudini con tipi multipli (action, counter, resistance, journal, state) componibili in alberi gerarchici. Rifiuta la riduzione *fatto / non fatto*: serve chi traccia salute cronica, allenamento, scrittura, astinenza, ecc.

- **Autore**: ITS Web Solutions Architect · Torino
- **Product spec**: v2.0 (aprile 2026)
- **DB schema**: v2.12 (luglio 2026)
- **Namespace**: `Recordum`
- **Licenza**: MIT
- **Scopo corrente**: Fase 4 — Stabilizzazione → Desktop v1.0

### Principi (non negoziabili)

1. **Offline-first.** Dati sul dispositivo. Sync cloud opzionale via provider terzi — mai server proprietario.
2. **Nessun account obbligatorio.** Account solo per sync opzionale.
3. **Privacy by default.** AI disabilitata al primo avvio. Dati non lasciano il dispositivo senza consenso informato.
4. **Complessità per composizione.** Esigenze complesse = gruppi di habit semplici, mai opzioni gonfiate sulla singola habit.
5. **Profondità senza intimidazione.** Uso base in 30s; advanced disponibile ma mai d'intralcio (progressive disclosure).
6. **Open source.** Niente paywall sulle feature core.
7. **English-first.** Tutte le stringhe utente in `.resx` dalla prima riga. Inglese primario, italiano secondario.

## Architettura da preservare

### Stack

| Componente | Tecnologia |
|---|---|
| Linguaggio | C# / .NET 10 |
| UI Windows (Fase 3) | WPF + CommunityToolkit.Mvvm |
| Persistenza | EF Core + SQLite + SQLCipher (`SQLitePCLRaw.bundle_e_sqlcipher`) |
| Editor Markdown | WebView2 + Toast UI Editor (vendored) |
| Backend web (Fase 4) | ASP.NET Core + React/Svelte PWA |
| Export PDF | QuestPDF |
| Mobile (Fase 7) | .NET MAUI |
| Health (Fase 7) | CoreMotion (iOS) / Health Connect (Android) |
| Statistica (Fase 9) | Math.NET Numerics |

### Progetti `src/`

| Progetto | Responsabilità |
|---|---|
| **Recordum.Core** | Domain models, abstractions (`IClock`, `IPluginContext`). C# puro, zero dipendenze UI/EF. |
| **Recordum.Data** | `RecordumDbContext`, Fluent configurations, migrazioni. Referenzia Core. |
| **Recordum.Plugins.Contracts** | Interfacce host, manifest record, permission/extension-point/event ID. Referenzia Core. |
| **Recordum.Plugins** | `PluginManager`, `InProcessPluginHost`, `AlwaysAllowPermissionBroker`, `ExtensionPointRegistry`, `EventBus`, `ApiRouter`. |
| **Recordum.Plugins.ChartEngine** | Core plugin pilota per chart (LiveCharts2) — consuma `chart.renderer`. |
| **Recordum.Plugins.ExportEngine** | Core plugin per export (PDF/HTML/Markdown/JSON/CSV) — consuma `export.format`. |
| **Recordum.WPF** | App + Views + ViewModels + `Bootstrap/` (ServiceRegistrar, ApplicationInitializer, RecordumOptions). |

**Dependency rule:** le dipendenze puntano solo verso il basso. `Recordum.Core` non dipende da EF, WPF, o nulla. `Recordum.Data` implementa le interfacce di Core. `Recordum.WPF` compone tutto via DI.

### Convenzioni di startup

- `App.OnStartup` costruisce il `ServiceProvider`, await `ApplicationInitializer.InitializeAsync()` che applica le migrazioni EF Core e tocca il `PluginManager`; on failure mostra dialog fatale localizzato e shutdown.
- SQLite **encrypted via SQLCipher**. Chiave master sempre da `IDbKeyProvider` iniettato — mai hardcoded.
- **Mai** `DateTime.UtcNow`/`Now` diretti. Tutto via `IClock`/`TimeProvider`.
- `TimestampInterceptor` popola automaticamente `CreatedAt`, `UpdatedAt`, `UpdatedAtMs`, e `CreatedByPluginId`.
- Global query filter soft-delete su `HabitNode`: `HasQueryFilter(n => n.DeletedAt == null)`.

### Schema invarianti

- **Tipi SQLite**: boolean = INTEGER 0/1; date/timestamp human-readable = TEXT ISO 8601; sync key high-precision = INTEGER Unix ms; enum = TEXT + CHECK; JSON blob = TEXT.
- **Convenzione timestamp**: `created_at`/`updated_at`/`valid_from`/`valid_until`/`generated_at`/`last_sync_at` → TEXT ISO 8601. `updated_at_ms`/`completed_at_ms` → INTEGER Unix ms.
- **Naming**: tabelle e colonne snake_case (via `EFCore.NamingConventions`). Classi/proprietà C# PascalCase.
- **Cascade rules + indici** dichiarati esplicitamente in Fluent API — mai default EF Core.
- **Soft delete**: solo su `HabitNode`. Cascade in transazione singola ai discendenti.
- **Provenance v2.3**: `created_by_plugin_id TEXT NULL` su entità top-level user-facing.
- **DB schema corrente**: v2.12 (AbstinencePeriod, luglio 2026).

## Regole operative

### Code conventions

- **MVVM** con `CommunityToolkit.Mvvm` (`ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`).
- **DI** ovunque via `Microsoft.Extensions.DependencyInjection`. `IClock`, `IPluginContext` iniettati. Mai accesso statico al tempo.
- **Stringhe utente** in `.resx` (English-first, italiano secondario). Niente hardcoded.
- **Cascade rules + indici** sempre in Fluent API esplicita, mai default EF.
- **Provenance**: nuove entità top-level user-facing → `created_by_plugin_id TEXT NULL`. Mai su entità child.
- **Plugin context**: service Core che crea entità per conto di un plugin deve farlo dentro uno scope con `IPluginContext.PluginId` valorizzato.
- **Soft delete**: usa il filtro globale; per Trash/diagnostica `IgnoreQueryFilters()`.
- **Test**: behavioral changes accompagnati da test; data layer e streak engine coperti su database in-memory reale.

### Modifiche sicure

- **Mai** editare a mano i file EF generati (`*.Designer.cs`/`*ModelSnapshot.cs`).
- **Mai** usare `DateTime.Now`/`UtcNow` — usare `IClock`/`TimeProvider`.
- **Sicurezza pre-merge**: eseguire `/security-review` e chiudere findings ad alta priorità.
- Prima di mergiare in `main`: feature-branch → PR → merge (mai push diretto su `main`).

### Aggiungere una stringa resx

1. Aggiungere entry in `Strings.resx` (inglese)
2. Aggiungere entry in `Strings.it.resx` (italiano)
3. Aggiungere accessor `=> Get("Key")` in `Strings.Designer.cs` (manutenuto a mano, non code-gen)

**Nota:** `guard-generated.js` matcha `*.Designer.cs` e blocca Edit/Write. Per editare `Strings.Designer.cs` usare shell (`python`/`sed`/heredoc). Verificare con build dopo.

### Roadmap

| Fase | Scope | Stato |
|---|---|---|
| 0–2 | Console + WPF prototyping | ✓ formativo |
| 3 | WPF production: schema + UI + plugin foundation | ✓ completata |
| **4** | **Stabilizzazione → Desktop v1.0** | **→ corrente** |
| 5 | Mobile (MAUI), standalone offline | Pianificata |
| 6 | Sync cloud via provider terzi | Pianificata |
| Future | Plugin WebView2, Web/PWA, AI, sync server | Rimandate |

## Comandi utili

### Build

```bash
# Build completo (tutta la soluzione)
dotnet build

# Build singolo progetto
dotnet build src/Recordum.WPF/Recordum.WPF.csproj

# Build senza restore (iterazione veloce)
dotnet build --no-restore
```

### Test

```bash
# Tutti i test
dotnet test

# Test di un progetto specifico
dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj

# Test with verbosity
dotnet test -v n
```

### Migrazioni EF Core

```bash
# Aggiungere migrazione
dotnet ef migrations add <Name> --project src/Recordum.Data --startup-project src/Recordum.WPF

# Applicare migrazione al DB locale
dotnet ef database update --project src/Recordum.Data --startup-project src/Recordum.WPF
```

Verificare il SQL generato contro la documentazione schema **prima** di eseguire l'update.

### Run (WPF — solo Windows)

```bash
# Build + run
dotnet run --project src/Recordum.WPF

# Run senza rebuild (dopo build)
dotnet run --project src/Recordum.WPF --no-build
```

Per resettare lo stato locale prima del run:
```powershell
Remove-Item -Recurse -Force "$env:LocalAppData\Recordum" -ErrorAction SilentlyContinue
```

## Verifica

- `dotnet build` deve passare senza errori (TreatWarningsAsErrors=true).
- `dotnet test` deve passare (suite attuale: Data ~370, WPF ~683).
- Le migrazioni EF Core devono essere verificate SQL-level prima dell'applicazione.
- Per modifiche UI: avviare l'app e verificare visivamente.
- Per modifiche sicurezza: eseguire `/security-review`.

## Indice AGENTS.md

- [`src/AGENTS.md`](src/AGENTS.md) — Source code: progetti, convenzioni, build, migrazioni
- [`tests/AGENTS.md`](tests/AGENTS.md) — Testing: struttura, comandi, convenzioni
- [`docs/AGENTS.md`](docs/AGENTS.md) — Documentazione: organizzazione, standard
- [`.claude/AGENTS.md`](.claude/AGENTS.md) — Claude configuration: hooks, settings, skills

---

Vedi [`CLAUDE.md`](CLAUDE.md) per il contesto di progetto dettagliato e invarianti completi.
Vedi [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) per una panoramica narrativa dell'architettura.
