# Recordum/src

> Vedi [`../AGENTS.md`](../AGENTS.md) per le regole globali del progetto.

## Ambito

Questa directory contiene tutti i progetti sorgente di Recordum, organizzati in 7 progetti .NET con dipendenze unidirezionali: Core → Data/Plugins.Contracts → Plugins → WPF.

## Progetti

### Recordum.Core
- **Responsabilità**: Domain models, abstractions (`IClock`, `IPluginContext`, service interfaces).
- **Dipendenze**: Zero — C# puro. Nessuna dipendenza UI/EF.
- **Da sapere**: Contiene le interfacce dei servizi (`IHabitNodeService`, `ICompletionService`, `IAbstinenceService`, …) implementate in `Recordum.Data`.

### Recordum.Data
- **Responsabilità**: EF Core DbContext, Fluent configurations, migrazioni, implementazioni servizi.
- **Dipendenze**: Recordum.Core
- **Da sapere**: Il DB è SQLite con SQLCipher. La chiave viene da `IDbKeyProvider` (DPAPI di default, opt-in Argon2id passphrase).
- **Migrazioni**: I file `*.Designer.cs` e `*ModelSnapshot.cs` NON vanno editati a mano (bloccato da hook).
- **Convenzioni**: Tutte le cascade rules e indici dichiarati esplicitamente in Fluent API.

### Recordum.Plugins.Contracts
- **Responsabilità**: Interfacce host, manifest record, permission/extension-point/event ID.
- **Dipendenze**: Recordum.Core
- **Da sapere**: Superficie stabile di compilazione per i plugin. Non ha runtime.

### Recordum.Plugins
- **Responsabilità**: PluginManager, InProcessPluginHost, AlwaysAllowPermissionBroker, ExtensionPointRegistry, EventBus, ApiRouter.
- **Dipendenze**: Recordum.Plugins.Contracts, Recordum.Core
- **Da sapere**: Host in-process per first-party fidati. WebView2 host pianificato per Fase 7.

### Recordum.Plugins.ChartEngine
- **Responsabilità**: Core plugin pilota per chart (LiveCharts2), consuma extension point `chart.renderer`.
- **Dipendenze**: Recordum.Plugins.Contracts, Recordum.Core

### Recordum.Plugins.ExportEngine
- **Responsabilità**: Core plugin per export (PDF/HTML/Markdown/JSON/CSV), consuma extension point `export.format`.
- **Dipendenze**: Recordum.Plugins.Contracts, Recordum.Core

### Recordum.WPF
- **Responsabilità**: App WPF — Views, ViewModels, Bootstrap (ServiceRegistrar, ApplicationInitializer).
- **Dipendenze**: Tutti gli altri progetti src.
- **Da sapere**: MVVM con CommunityToolkit.Mvvm. RID win-x64, SelfContained=false.

## Regole operative

### Convenzioni di codice

- **MVVM** con `CommunityToolkit.Mvvm` (`ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`).
- **DI** ovunque via `Microsoft.Extensions.DependencyInjection`.
- **Tempo**: Mai `DateTime.Now`/`UtcNow` — usare `IClock`/`TimeProvider`.
- **Stringhe utente**: In `.resx` (English-first). Aggiungere stringa = 3 file (`Strings.resx` + `Strings.it.resx` + `Strings.Designer.cs`).
- **Provenance**: Nuove entità top-level → `created_by_plugin_id TEXT NULL`. Mai su child.
- **Soft delete**: Usa `IgnoreQueryFilters()` per Trash/diagnostica.

### Migrazioni

```bash
dotnet ef migrations add <Name> --project src/Recordum.Data --startup-project src/Recordum.WPF
dotnet ef database update --project src/Recordum.Data --startup-project src/Recordum.WPF
```

- Verificare il SQL generato contro `docs/superpowers/specs/` PRIMA di applicare.
- I file `*.Designer.cs`/`*ModelSnapshot.cs` NON si editano a mano.

### Build

```bash
# Build progetto specifico
dotnet build src/Recordum.WPF/Recordum.WPF.csproj

# Build con skip restore
dotnet build --no-restore
```

Il RID è vincolato a `win-x64` (`SelfContained=false` in `Directory.Build.props`).

## Verifica tipica

1. `dotnet build src/Recordum.WPF` — deve compilare (TreatWarningsAsErrors=true, NU1903 declassato)
2. Controllare che il SQL generato dalle migrazioni corrisponda alla documentazione schema
3. Verificare che i puntamenti e i namespace siano corretti

## Indice AGENTS.md

- Vedi `../AGENTS.md`
