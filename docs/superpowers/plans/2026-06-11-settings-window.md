# Finestra Impostazioni unificata (Fase A rimodernizzazione UI) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Una `SettingsWindow` modale stile Obsidian (categorie a sinistra, contenuto a destra) che unifica tema, impostazioni generali (reset giornaliero), backup e diagnostica, con le categorie future Sync/Plugin/AI visibili ma disabilitate; sostituisce `SettingsPopup` (orfano), `BackupSettingsDialog` e `DiagnosticInfoDialog`.

**Architecture:** `SettingsWindowViewModel` costruisce una collezione ordinata di `SettingsCategory` (id, nome, icona Lucide, enabled, content-VM); i contenuti riusano i VM esistenti (`SettingsViewModel` tema, `BackupSettingsViewModel`, `DiagnosticInfoViewModel`) più un nuovo `GeneralSettingsViewModel` (`daily_reset_time`). Un `ISettingsWindowLauncher` sul pattern dei launcher esistenti apre la finestra su una categoria iniziale. `MainWindowViewModel` perde il VM tema e i launcher backup-settings/diagnostica, sostituiti dal nuovo launcher.

**Tech Stack:** WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions, stringhe `.resx` EN/IT (pattern `Strings.Designer.cs` manuale con `Get("Dotted.Key")`), icone = `Geometry` in `Resources/Icons.xaml` rese con `Path` stroke-based.

**Spec:** `docs/superpowers/specs/2026-06-11-ui-modernization-plan-design.md` §Fase A.

**Convenzioni di progetto da rispettare:** niente `DateTime.Now` (usare `IClock` se mai servisse — qui non serve); analyzer severi (build con 0 warning: occhio a CA1859/CA1822); commit frequenti; suite WPF deve restare verde (`HomeViewModelTests.CompletionRecorded_event_reloads…` è un flaky noto solo in suite completa — verificarlo isolato se fallisce).

---

## Mappa file

| Azione | Path | Responsabilità |
|---|---|---|
| Create | `src/Recordum.WPF/ViewModels/Settings/SettingsCategoryId.cs` | enum identità categorie |
| Create | `src/Recordum.WPF/ViewModels/Settings/SettingsCategory.cs` | record categoria (id, nome, icona, enabled, content) |
| Create | `src/Recordum.WPF/ViewModels/Settings/ComingSoonContent.cs` | content marker per categorie disabilitate |
| Create | `src/Recordum.WPF/ViewModels/Settings/GeneralSettingsViewModel.cs` | load/save `daily_reset_time` |
| Create | `src/Recordum.WPF/ViewModels/Settings/SettingsWindowViewModel.cs` | costruzione categorie + selezione |
| Create | `src/Recordum.WPF/Services/ISettingsWindowLauncher.cs` + `WpfSettingsWindowLauncher.cs` | apertura modale con categoria iniziale |
| Create | `src/Recordum.WPF/Views/Settings/SettingsWindow.xaml(.cs)` | la finestra (lista categorie + DataTemplate per content) |
| Modify | `src/Recordum.WPF/Resources/Icons.xaml` | +7 geometrie Lucide |
| Modify | `src/Recordum.WPF/Resources/Strings(.it).resx` + `Strings.Designer.cs` | stringhe nuove |
| Modify | `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` | registrazioni |
| Modify | `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs` | rewiring command |
| Modify | `src/Recordum.WPF/MainWindow.xaml` | menu Impostazioni; via il Popup orfano |
| Modify | `src/Recordum.WPF/ViewModels/Backup/IBackupDialogLauncher.cs` + `Services/WpfBackupDialogLauncher.cs` | via `ShowSettingsAsync` |
| Delete | `Views/SettingsPopup.xaml(.cs)`, `Views/Backup/BackupSettingsDialog.xaml(.cs)`, `Views/DiagnosticInfoDialog.xaml(.cs)`, `Services/IDiagnosticInfoLauncher.cs`, `Services/WpfDiagnosticInfoLauncher.cs` | UI assorbita |
| Test | `tests/Recordum.WPF.Tests/ViewModels/Settings/GeneralSettingsViewModelTests.cs` | round-trip reset time |
| Test | `tests/Recordum.WPF.Tests/ViewModels/Settings/SettingsWindowViewModelTests.cs` | categorie + selezione iniziale |
| Test (modify) | `tests/Recordum.WPF.Tests/ViewModels/MainWindowViewModelTests.cs` + `Fixtures/` | nuovo ctor + fake launcher |

Nota fixture: riusare il fake `IAppSettingService` già usato da `MainWindowViewModelTests` (in `tests/Recordum.WPF.Tests/Fixtures/` — verificarne il nome esatto prima di scrivere i test; se non esiste un fake riusabile, crearne uno minimale dict-based nello stesso stile degli altri fake lì presenti).

---

### Task 1: Geometrie Lucide per le categorie

**Files:**
- Modify: `src/Recordum.WPF/Resources/Icons.xaml` (sezione "chrome icons", dopo `Icon.Search`)

- [ ] **Step 1: Aggiungere le 7 geometrie**

Nella sezione `<!-- ── chrome icons (pre-existing) ─…` di `Icons.xaml`, dopo la riga di `Icon.Search`, aggiungere:

```xml
    <Geometry x:Key="Icon.Palette">M12 22a10 10 0 1 1 10-10c0 1.7-1.3 3-3 3h-2.5a2.5 2.5 0 0 0-2.5 2.5c0 .61.22 1.17.58 1.6.36.43.59.99.42 1.53A2 2 0 0 1 13 22Z M13 6.5a.5.5 0 1 1 1 0 .5.5 0 1 1 -1 0 M17 10.5a.5.5 0 1 1 1 0 .5.5 0 1 1 -1 0 M8 7.5a.5.5 0 1 1 1 0 .5.5 0 1 1 -1 0 M6 12.5a.5.5 0 1 1 1 0 .5.5 0 1 1 -1 0</Geometry>
    <Geometry x:Key="Icon.SlidersHorizontal">M21 4h-7 M10 4H3 M21 12h-9 M8 12H3 M21 20h-5 M12 20H3 M14 2v4 M8 10v4 M16 18v4</Geometry>
    <Geometry x:Key="Icon.LifeBuoy">M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20z M12 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M4.93 4.93l4.24 4.24 M14.83 9.17l4.24-4.24 M14.83 14.83l4.24 4.24 M9.17 14.83l-4.24 4.24</Geometry>
    <Geometry x:Key="Icon.Info">M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20z M12 16v-4 M12 8h.01</Geometry>
    <Geometry x:Key="Icon.RefreshCw">M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8 M21 3v5h-5 M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16 M8 16H3v5</Geometry>
    <Geometry x:Key="Icon.Puzzle">M19.439 7.85c-.049.322.059.648.289.878l1.568 1.568c.47.47.706 1.087.706 1.704s-.235 1.233-.706 1.704l-1.611 1.611a.98.98 0 0 1-.837.276c-.47-.07-.802-.48-.968-.925a2.501 2.501 0 1 0-3.214 3.214c.446.166.855.497.925.968a.979.979 0 0 1-.276.837l-1.61 1.61a2.404 2.404 0 0 1-1.705.707 2.402 2.402 0 0 1-1.704-.706l-1.568-1.568a1.026 1.026 0 0 0-.877-.29c-.493.074-.84.504-1.02.968a2.5 2.5 0 1 1-3.237-3.237c.464-.18.894-.527.967-1.02a1.026 1.026 0 0 0-.289-.877l-1.568-1.568A2.402 2.402 0 0 1 1.998 12c0-.617.236-1.234.706-1.704L4.23 8.77c.24-.24.581-.353.917-.303.515.077.877.528 1.073 1.01a2.5 2.5 0 1 0 3.259-3.259c-.482-.196-.933-.558-1.01-1.073-.05-.336.062-.676.303-.917l1.525-1.525A2.402 2.402 0 0 1 12 1.998c.617 0 1.234.236 1.704.706l1.568 1.568c.23.23.556.338.877.29.493-.074.84-.504 1.02-.968a2.5 2.5 0 1 1 3.237 3.237c-.464.18-.894.527-.967 1.02Z</Geometry>
    <Geometry x:Key="Icon.Sparkles">M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z M20 3v4 M22 5h-4 M4 17v2 M5 18H3</Geometry>
```

NON aggiungerle a `IconCatalog.All` (quello è il catalogo del picker abitudini; queste sono icone di chrome).

- [ ] **Step 2: Build**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj --nologo`
Expected: 0 errori 0 warning.

- [ ] **Step 3: Commit**

```bash
git add src/Recordum.WPF/Resources/Icons.xaml
git commit -m "feat(wpf): Lucide chrome icons for settings categories"
```

---

### Task 2: Stringhe resx (EN + IT + Designer)

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`

Esistono già (riusare, non duplicare): `Settings.Title` ("Settings"/"Impostazioni"), `Settings.Theme*`, le stringhe del dialog backup (prefisso `Backup.Settings.*` — verificarne i nomi esatti in `Strings.resx` cercando le chiavi usate da `BackupSettingsDialog.xaml`), `Diagnostic.Title`.

- [ ] **Step 1: Aggiungere a `Strings.resx` (EN)**

```xml
  <data name="Settings.Category.Appearance" xml:space="preserve"><value>Appearance</value></data>
  <data name="Settings.Category.General" xml:space="preserve"><value>General</value></data>
  <data name="Settings.Category.Backup" xml:space="preserve"><value>Backup</value></data>
  <data name="Settings.Category.Diagnostics" xml:space="preserve"><value>Diagnostics</value></data>
  <data name="Settings.Category.Sync" xml:space="preserve"><value>Sync</value></data>
  <data name="Settings.Category.Plugins" xml:space="preserve"><value>Plugins</value></data>
  <data name="Settings.Category.Ai" xml:space="preserve"><value>AI</value></data>
  <data name="Settings.ComingSoon" xml:space="preserve"><value>Coming soon</value></data>
  <data name="Settings.ComingSoon.Sync" xml:space="preserve"><value>Optional cloud sync through third-party providers. Your data never touches a Recordum server.</value></data>
  <data name="Settings.ComingSoon.Plugins" xml:space="preserve"><value>Install and manage plugins that extend Recordum with new chart types, export formats and more.</value></data>
  <data name="Settings.ComingSoon.Ai" xml:space="preserve"><value>Opt-in AI insights and correlations. Disabled by default: nothing leaves your device without consent.</value></data>
  <data name="Settings.General.DailyResetTime" xml:space="preserve"><value>Daily reset time</value></data>
  <data name="Settings.General.DailyResetTime.Hint" xml:space="preserve"><value>The time of day when a new habit day starts (HH:mm). Habits and groups can override it.</value></data>
```

- [ ] **Step 2: Aggiungere a `Strings.it.resx` (IT)**

```xml
  <data name="Settings.Category.Appearance" xml:space="preserve"><value>Aspetto</value></data>
  <data name="Settings.Category.General" xml:space="preserve"><value>Generale</value></data>
  <data name="Settings.Category.Backup" xml:space="preserve"><value>Backup</value></data>
  <data name="Settings.Category.Diagnostics" xml:space="preserve"><value>Diagnostica</value></data>
  <data name="Settings.Category.Sync" xml:space="preserve"><value>Sincronizzazione</value></data>
  <data name="Settings.Category.Plugins" xml:space="preserve"><value>Plugin</value></data>
  <data name="Settings.Category.Ai" xml:space="preserve"><value>AI</value></data>
  <data name="Settings.ComingSoon" xml:space="preserve"><value>In arrivo</value></data>
  <data name="Settings.ComingSoon.Sync" xml:space="preserve"><value>Sincronizzazione cloud opzionale tramite provider terzi. I tuoi dati non passano mai da un server Recordum.</value></data>
  <data name="Settings.ComingSoon.Plugins" xml:space="preserve"><value>Installa e gestisci plugin che estendono Recordum con nuovi grafici, formati di export e altro.</value></data>
  <data name="Settings.ComingSoon.Ai" xml:space="preserve"><value>Insight e correlazioni AI opt-in. Disabilitata di default: nulla lascia il tuo dispositivo senza consenso.</value></data>
  <data name="Settings.General.DailyResetTime" xml:space="preserve"><value>Orario di reset giornaliero</value></data>
  <data name="Settings.General.DailyResetTime.Hint" xml:space="preserve"><value>L'orario in cui inizia un nuovo giorno per le abitudini (HH:mm). Abitudini e gruppi possono sovrascriverlo.</value></data>
```

- [ ] **Step 3: Aggiungere a `Strings.Designer.cs`** (accanto alle property `Settings_*` esistenti)

```csharp
    public static string Settings_Category_Appearance => Get("Settings.Category.Appearance");
    public static string Settings_Category_General => Get("Settings.Category.General");
    public static string Settings_Category_Backup => Get("Settings.Category.Backup");
    public static string Settings_Category_Diagnostics => Get("Settings.Category.Diagnostics");
    public static string Settings_Category_Sync => Get("Settings.Category.Sync");
    public static string Settings_Category_Plugins => Get("Settings.Category.Plugins");
    public static string Settings_Category_Ai => Get("Settings.Category.Ai");
    public static string Settings_ComingSoon => Get("Settings.ComingSoon");
    public static string Settings_ComingSoon_Sync => Get("Settings.ComingSoon.Sync");
    public static string Settings_ComingSoon_Plugins => Get("Settings.ComingSoon.Plugins");
    public static string Settings_ComingSoon_Ai => Get("Settings.ComingSoon.Ai");
    public static string Settings_General_DailyResetTime => Get("Settings.General.DailyResetTime");
    public static string Settings_General_DailyResetTime_Hint => Get("Settings.General.DailyResetTime.Hint");
```

- [ ] **Step 4: Build + commit**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj --nologo` → 0/0.

```bash
git add src/Recordum.WPF/Resources/
git commit -m "feat(wpf): resx strings for the unified settings window (EN/IT)"
```

---

### Task 3: `GeneralSettingsViewModel` (TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/Settings/GeneralSettingsViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/Settings/GeneralSettingsViewModelTests.cs`

- [ ] **Step 1: Scrivere i test (falliranno: il tipo non esiste)**

```csharp
using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Settings;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Settings;

public class GeneralSettingsViewModelTests
{
    // Riusare il fake IAppSettingService esistente nelle Fixtures (lo stesso di MainWindowViewModelTests).
    private readonly FakeAppSettingService _settings = new();

    [Fact]
    public async Task LoadAsync_reads_daily_reset_time()
    {
        await _settings.SetAsync("daily_reset_time", "05:30");
        var sut = new GeneralSettingsViewModel(_settings);

        await sut.LoadAsync();

        sut.DailyResetTime.Should().Be("05:30");
    }

    [Fact]
    public async Task Valid_time_is_persisted_on_change()
    {
        var sut = new GeneralSettingsViewModel(_settings);
        await sut.LoadAsync();

        sut.DailyResetTime = "06:15";

        (await _settings.GetAsync("daily_reset_time")).Should().Be("06:15");
    }

    [Fact]
    public async Task Invalid_time_is_not_persisted()
    {
        await _settings.SetAsync("daily_reset_time", "00:00");
        var sut = new GeneralSettingsViewModel(_settings);
        await sut.LoadAsync();

        sut.DailyResetTime = "25:99";

        (await _settings.GetAsync("daily_reset_time")).Should().Be("00:00");
        sut.HasError.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run → RED**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~GeneralSettings" --nologo`
Expected: errore di compilazione `GeneralSettingsViewModel` inesistente. (Se `FakeAppSettingService` non esiste con quel nome, adattare il test al fake reale presente in Fixtures PRIMA di proseguire.)

- [ ] **Step 3: Implementare**

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.ViewModels.Settings;

/// <summary>"General" settings category: edits the app-wide daily reset time (AppSetting
/// daily_reset_time, HH:mm). Saves on change when the value parses; flags HasError otherwise.</summary>
public sealed partial class GeneralSettingsViewModel(IAppSettingService settings) : ObservableObject
{
    internal const string DailyResetTimeKey = "daily_reset_time";
    private bool _loading;

    [ObservableProperty] private string _dailyResetTime = "00:00";
    [ObservableProperty] private bool _hasError;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        _loading = true;
        try
        {
            DailyResetTime = await settings.GetAsync(DailyResetTimeKey, ct).ConfigureAwait(true) ?? "00:00";
            HasError = false;
        }
        finally { _loading = false; }
    }

    partial void OnDailyResetTimeChanged(string value)
    {
        if (_loading) return;
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            HasError = true;
            return;
        }
        HasError = false;
        _ = settings.SetAsync(DailyResetTimeKey, value);
    }
}
```

Nota: verificare la firma esatta di `IAppSettingService.GetAsync/SetAsync` (in `Recordum.Core/Abstractions/Services`) e adeguare `ct`/overload.

- [ ] **Step 4: Run → GREEN**

Stesso comando del passo 2. Expected: 3 passati.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Settings/ tests/Recordum.WPF.Tests/ViewModels/Settings/
git commit -m "feat(wpf): GeneralSettingsViewModel — daily reset time setting"
```

---

### Task 4: Modello categorie + `SettingsWindowViewModel` (TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/Settings/SettingsCategoryId.cs`
- Create: `src/Recordum.WPF/ViewModels/Settings/SettingsCategory.cs`
- Create: `src/Recordum.WPF/ViewModels/Settings/ComingSoonContent.cs`
- Create: `src/Recordum.WPF/ViewModels/Settings/SettingsWindowViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/Settings/SettingsWindowViewModelTests.cs`

- [ ] **Step 1: Test (RED)**

```csharp
using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.Settings;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Settings;

public class SettingsWindowViewModelTests
{
    private static SettingsWindowViewModel CreateSut()
        => new(
            new SettingsViewModel(new FakeThemeService()),
            new GeneralSettingsViewModel(new FakeAppSettingService()),
            CreateBackupVm(),
            CreateDiagnosticVm());

    // Helper: costruire BackupSettingsViewModel e DiagnosticInfoViewModel coi fake già presenti in
    // Fixtures (gli stessi usati dai loro test esistenti — cercarli e riusarli, NON crearne di nuovi
    // se esistono). Se DiagnosticInfoViewModel non ha test/fake, costruirlo coi suoi servizi fake minimi.
    private static Recordum.WPF.ViewModels.Backup.BackupSettingsViewModel CreateBackupVm() => /* vedi nota sopra */ null!;
    private static Recordum.WPF.ViewModels.DiagnosticInfo.DiagnosticInfoViewModel CreateDiagnosticVm() => /* idem */ null!;

    [Fact]
    public void Categories_are_seven_in_canonical_order_with_future_ones_disabled()
    {
        var sut = CreateSut();

        sut.Categories.Select(c => c.Id).Should().Equal(
            SettingsCategoryId.Appearance, SettingsCategoryId.General, SettingsCategoryId.Backup,
            SettingsCategoryId.Diagnostics, SettingsCategoryId.Sync, SettingsCategoryId.Plugins,
            SettingsCategoryId.Ai);
        sut.Categories.Where(c => !c.IsEnabled).Select(c => c.Id).Should().Equal(
            SettingsCategoryId.Sync, SettingsCategoryId.Plugins, SettingsCategoryId.Ai);
        sut.Categories.Where(c => !c.IsEnabled).Should().OnlyContain(c => c.Content is ComingSoonContent);
    }

    [Fact]
    public void Default_selection_is_appearance()
    {
        var sut = CreateSut();
        sut.SelectInitial(null);
        sut.SelectedCategory!.Id.Should().Be(SettingsCategoryId.Appearance);
    }

    [Fact]
    public void Initial_category_request_is_honoured()
    {
        var sut = CreateSut();
        sut.SelectInitial(SettingsCategoryId.Backup);
        sut.SelectedCategory!.Id.Should().Be(SettingsCategoryId.Backup);
    }

    [Fact]
    public void Initial_request_for_disabled_category_falls_back_to_appearance()
    {
        var sut = CreateSut();
        sut.SelectInitial(SettingsCategoryId.Sync);
        sut.SelectedCategory!.Id.Should().Be(SettingsCategoryId.Appearance);
    }
}
```

(`FakeThemeService`: se non esiste già in Fixtures, crearlo: implementa `IThemeService` con `CurrentMode` get/set e `SetModeAsync` che aggiorna `CurrentMode`.)

- [ ] **Step 2: Run → RED** (tipi inesistenti)

`dotnet test ... --filter "FullyQualifiedName~SettingsWindowViewModel" --nologo`

- [ ] **Step 3: Implementare i 4 tipi**

`SettingsCategoryId.cs`:

```csharp
namespace Recordum.WPF.ViewModels.Settings;

public enum SettingsCategoryId { Appearance, General, Backup, Diagnostics, Sync, Plugins, Ai }
```

`ComingSoonContent.cs`:

```csharp
namespace Recordum.WPF.ViewModels.Settings;

/// <summary>Content of a not-yet-available settings category: shown as a "coming soon" panel.
/// Visible-but-disabled future categories are an approved deviation from the no-placeholder rule
/// (spec 2026-06-11-ui-modernization-plan-design.md, decisione 3).</summary>
public sealed record ComingSoonContent(string Description);
```

`SettingsCategory.cs`:

```csharp
namespace Recordum.WPF.ViewModels.Settings;

/// <summary>One entry of the settings window's left-hand category list.</summary>
public sealed record SettingsCategory(SettingsCategoryId Id, string Name, string IconKey, bool IsEnabled, object Content);
```

`SettingsWindowViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.WPF.Resources;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.ViewModels.DiagnosticInfo;

namespace Recordum.WPF.ViewModels.Settings;

/// <summary>Obsidian-style unified settings window: a fixed, ordered list of categories whose
/// content panes reuse the per-area viewmodels. Disabled categories carry ComingSoonContent.</summary>
public sealed partial class SettingsWindowViewModel : ObservableObject
{
    [ObservableProperty] private SettingsCategory? _selectedCategory;

    public ObservableCollection<SettingsCategory> Categories { get; }

    public SettingsWindowViewModel(
        SettingsViewModel appearance,
        GeneralSettingsViewModel general,
        BackupSettingsViewModel backup,
        DiagnosticInfoViewModel diagnostics)
    {
        Categories = new ObservableCollection<SettingsCategory>
        {
            new(SettingsCategoryId.Appearance,  Strings.Settings_Category_Appearance,  "Icon.Palette",           true,  appearance),
            new(SettingsCategoryId.General,     Strings.Settings_Category_General,     "Icon.SlidersHorizontal", true,  general),
            new(SettingsCategoryId.Backup,      Strings.Settings_Category_Backup,      "Icon.LifeBuoy",          true,  backup),
            new(SettingsCategoryId.Diagnostics, Strings.Settings_Category_Diagnostics, "Icon.Info",              true,  diagnostics),
            new(SettingsCategoryId.Sync,        Strings.Settings_Category_Sync,        "Icon.RefreshCw",         false, new ComingSoonContent(Strings.Settings_ComingSoon_Sync)),
            new(SettingsCategoryId.Plugins,     Strings.Settings_Category_Plugins,     "Icon.Puzzle",            false, new ComingSoonContent(Strings.Settings_ComingSoon_Plugins)),
            new(SettingsCategoryId.Ai,          Strings.Settings_Category_Ai,          "Icon.Sparkles",          false, new ComingSoonContent(Strings.Settings_ComingSoon_Ai)),
        };
    }

    /// <summary>Selects the requested category (launcher deep-link); disabled or null requests
    /// fall back to Appearance.</summary>
    public void SelectInitial(SettingsCategoryId? id)
    {
        var match = id is { } i ? Categories.FirstOrDefault(c => c.Id == i && c.IsEnabled) : null;
        SelectedCategory = match ?? Categories[0];
    }
}
```

- [ ] **Step 4: Run → GREEN**, poi suite Settings completa verde.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Settings/ tests/Recordum.WPF.Tests/
git commit -m "feat(wpf): SettingsWindowViewModel — category model with disabled future sections"
```

---

### Task 5: `SettingsWindow` (view) + launcher + DI

**Files:**
- Create: `src/Recordum.WPF/Views/Settings/SettingsWindow.xaml(.cs)`
- Create: `src/Recordum.WPF/Services/ISettingsWindowLauncher.cs`, `src/Recordum.WPF/Services/WpfSettingsWindowLauncher.cs`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`

- [ ] **Step 1: Launcher**

`ISettingsWindowLauncher.cs`:

```csharp
using Recordum.WPF.ViewModels.Settings;

namespace Recordum.WPF.Services;

public interface ISettingsWindowLauncher
{
    Task ShowAsync(SettingsCategoryId? initialCategory = null);
}
```

`WpfSettingsWindowLauncher.cs` (pattern di `WpfDiagnosticInfoLauncher`):

```csharp
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.ViewModels.DiagnosticInfo;
using Recordum.WPF.ViewModels.Settings;
using Recordum.WPF.Views.Settings;

namespace Recordum.WPF.Services;

public sealed class WpfSettingsWindowLauncher(IServiceProvider services) : ISettingsWindowLauncher
{
    public async Task ShowAsync(SettingsCategoryId? initialCategory = null)
    {
        var vm = services.GetRequiredService<SettingsWindowViewModel>();
        // Load every real category's content before showing (cheap: 4 small reads).
        foreach (var category in vm.Categories)
        {
            switch (category.Content)
            {
                case SettingsViewModel s: await s.InitializeAsync().ConfigureAwait(true); break;
                case GeneralSettingsViewModel g: await g.LoadAsync().ConfigureAwait(true); break;
                case BackupSettingsViewModel b: await b.LoadAsync().ConfigureAwait(true); break;
                case DiagnosticInfoViewModel d: await d.LoadAsync().ConfigureAwait(true); break;
            }
        }
        vm.SelectInitial(initialCategory);
        var window = new SettingsWindow(vm) { Owner = Application.Current?.MainWindow };
        window.ShowDialog();
    }
}
```

(Verificare il nome esatto del metodo di load di `DiagnosticInfoViewModel` — è `LoadAsync()` in `WpfDiagnosticInfoLauncher`.)

- [ ] **Step 2: La finestra**

`SettingsWindow.xaml` — struttura completa; i pannelli Backup e Diagnostica si portano DENTRO i rispettivi `DataTemplate` copiando il contenuto dai dialog esistenti:

```xml
<Window x:Class="Recordum.WPF.Views.Settings.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vms="clr-namespace:Recordum.WPF.ViewModels.Settings"
        xmlns:vm="clr-namespace:Recordum.WPF.ViewModels"
        xmlns:vmb="clr-namespace:Recordum.WPF.ViewModels.Backup"
        xmlns:vmd="clr-namespace:Recordum.WPF.ViewModels.DiagnosticInfo"
        xmlns:enums="clr-namespace:Recordum.Core.Enums;assembly=Recordum.Core"
        xmlns:res="clr-namespace:Recordum.WPF.Resources"
        Title="{x:Static res:Strings.Settings_Title}"
        Width="760" Height="540" MinWidth="640" MinHeight="440"
        WindowStartupLocation="CenterOwner"
        Background="{DynamicResource Brush.Background}">
    <Window.Resources>
        <!-- Aspetto: port di SettingsPopup (tema Sistema/Chiaro/Scuro) -->
        <DataTemplate DataType="{x:Type vm:SettingsViewModel}">
            <StackPanel>
                <TextBlock Text="{x:Static res:Strings.Settings_Theme}" FontWeight="SemiBold"
                           Foreground="{DynamicResource Brush.Text}" Margin="0,0,0,8"/>
                <RadioButton GroupName="Theme" Content="{x:Static res:Strings.Settings_Theme_System}" Margin="0,0,0,4"
                             IsChecked="{Binding ThemeMode, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static enums:ThemeMode.System}}"/>
                <RadioButton GroupName="Theme" Content="{x:Static res:Strings.Settings_Theme_Light}" Margin="0,0,0,4"
                             IsChecked="{Binding ThemeMode, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static enums:ThemeMode.Light}}"/>
                <RadioButton GroupName="Theme" Content="{x:Static res:Strings.Settings_Theme_Dark}"
                             IsChecked="{Binding ThemeMode, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static enums:ThemeMode.Dark}}"/>
            </StackPanel>
        </DataTemplate>

        <!-- Generale -->
        <DataTemplate DataType="{x:Type vms:GeneralSettingsViewModel}">
            <StackPanel>
                <TextBlock Text="{x:Static res:Strings.Settings_General_DailyResetTime}" FontWeight="SemiBold"
                           Foreground="{DynamicResource Brush.Text}"/>
                <TextBox Width="72" HorizontalAlignment="Left" Margin="0,4,0,2"
                         Text="{Binding DailyResetTime, UpdateSourceTrigger=LostFocus}">
                    <TextBox.Style>
                        <Style TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding HasError}" Value="True">
                                    <Setter Property="BorderBrush" Value="#d04848"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </TextBox.Style>
                </TextBox>
                <TextBlock Text="{x:Static res:Strings.Settings_General_DailyResetTime_Hint}"
                           FontSize="{StaticResource FontSize.Small}" TextWrapping="Wrap"
                           Foreground="{DynamicResource Brush.TextMuted}"/>
            </StackPanel>
        </DataTemplate>

        <!-- Backup: PORT del contenuto di Views/Backup/BackupSettingsDialog.xaml.
             Copiare qui dentro il pannello interno del dialog (tutto tranne il tag Window e
             l'eventuale riga bottoni Chiudi), conservando binding e stringhe esistenti.
             Le PasswordBox mantengono gli handler: spostarli nel code-behind della finestra
             (vedi SettingsWindow.xaml.cs sotto) con gli stessi nomi PassBox/ConfirmBox. -->
        <DataTemplate DataType="{x:Type vmb:BackupSettingsViewModel}">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel><!-- ⟵ INSERIRE QUI il contenuto portato del dialog --></StackPanel>
            </ScrollViewer>
        </DataTemplate>

        <!-- Diagnostica: PORT del contenuto di Views/DiagnosticInfoDialog.xaml
             (ScrollViewer+ItemsControl Rows + bottone copia se presente), senza il tag Window. -->
        <DataTemplate DataType="{x:Type vmd:DiagnosticInfoViewModel}">
            <Grid><!-- ⟵ INSERIRE QUI il contenuto portato del dialog --></Grid>
        </DataTemplate>

        <!-- In arrivo -->
        <DataTemplate DataType="{x:Type vms:ComingSoonContent}">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" MaxWidth="380">
                <TextBlock Text="{x:Static res:Strings.Settings_ComingSoon}" FontWeight="SemiBold"
                           FontSize="{StaticResource FontSize.Heading}" HorizontalAlignment="Center"
                           Foreground="{DynamicResource Brush.TextMuted}"/>
                <TextBlock Text="{Binding Description}" TextWrapping="Wrap" TextAlignment="Center"
                           Margin="0,8,0,0" Foreground="{DynamicResource Brush.TextMuted}"/>
            </StackPanel>
        </DataTemplate>
    </Window.Resources>

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="190"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- Categorie -->
        <Border Grid.Column="0" Background="{DynamicResource Brush.Surface}"
                BorderThickness="0,0,1,0" BorderBrush="{DynamicResource Brush.Border}">
            <ListBox ItemsSource="{Binding Categories}" SelectedItem="{Binding SelectedCategory}"
                     Background="Transparent" BorderThickness="0" Margin="6,10">
                <ListBox.ItemContainerStyle>
                    <Style TargetType="ListBoxItem">
                        <Setter Property="IsEnabled" Value="{Binding IsEnabled}"/>
                        <Setter Property="Foreground" Value="{DynamicResource Brush.Text}"/>
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="ListBoxItem">
                                    <Border x:Name="bd" CornerRadius="4" Padding="10,7" Margin="0,1">
                                        <ContentPresenter/>
                                    </Border>
                                    <ControlTemplate.Triggers>
                                        <Trigger Property="IsMouseOver" Value="True">
                                            <Setter TargetName="bd" Property="Background" Value="{DynamicResource Brush.SurfaceElevated}"/>
                                        </Trigger>
                                        <Trigger Property="IsSelected" Value="True">
                                            <Setter TargetName="bd" Property="Background" Value="{DynamicResource Brush.SurfaceElevated}"/>
                                        </Trigger>
                                        <Trigger Property="IsEnabled" Value="False">
                                            <Setter Property="Opacity" Value="0.45"/>
                                        </Trigger>
                                    </ControlTemplate.Triggers>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>
                </ListBox.ItemContainerStyle>
                <ListBox.ItemTemplate>
                    <DataTemplate DataType="{x:Type vms:SettingsCategory}">
                        <StackPanel Orientation="Horizontal">
                            <Path Width="16" Height="16" Margin="0,0,9,0" Stretch="Uniform"
                                  Stroke="{DynamicResource Brush.Text}" StrokeThickness="1.6"
                                  StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
                                  Data="{Binding IconKey, Converter={StaticResource IconKeyToGeometry}}"/>
                            <TextBlock Text="{Binding Name}" VerticalAlignment="Center"/>
                        </StackPanel>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </Border>

        <!-- Contenuto -->
        <Grid Grid.Column="1" Margin="20,16">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>
            <TextBlock Grid.Row="0" Text="{Binding SelectedCategory.Name}"
                       FontSize="{StaticResource FontSize.Heading}" FontWeight="SemiBold"
                       Foreground="{DynamicResource Brush.Text}" Margin="0,0,0,14"/>
            <ContentControl Grid.Row="1" Content="{Binding SelectedCategory.Content}"/>
        </Grid>
    </Grid>
</Window>
```

`SettingsWindow.xaml.cs`:

```csharp
using System.Windows;
using Recordum.WPF.ViewModels.Settings;

namespace Recordum.WPF.Views.Settings;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsWindowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
```

ATTENZIONE port PasswordBox (Backup): le `PasswordBox` non supportano binding — nel dialog originale gli handler `OnPassChanged/OnConfirmChanged` scrivono sul VM. Dentro un `DataTemplate` il code-behind della finestra non vede `PassBox` per nome: sostituire gli handler con eventi `PasswordChanged` dichiarati nel template che castano `((FrameworkElement)sender).DataContext` a `BackupSettingsViewModel` e assegnano `NewPassphrase/ConfirmPassphrase` da `((PasswordBox)sender).Password`. Implementare i due handler in `SettingsWindow.xaml.cs`.

- [ ] **Step 3: Registrazioni DI** in `ServiceRegistrar` (vicino alle righe 137/161/201 esistenti):

```csharp
services.AddTransient<Recordum.WPF.ViewModels.Settings.GeneralSettingsViewModel>();
services.AddTransient<Recordum.WPF.ViewModels.Settings.SettingsWindowViewModel>();
services.AddSingleton<ISettingsWindowLauncher, WpfSettingsWindowLauncher>();
```

- [ ] **Step 4: Build 0/0 + suite Settings verde + commit**

```bash
git add src/Recordum.WPF/
git commit -m "feat(wpf): unified SettingsWindow + launcher (Obsidian-style categories)"
```

---

### Task 6: Rewiring `MainWindowViewModel` + menu (e fix test)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs`
- Modify: `src/Recordum.WPF/MainWindow.xaml`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/MainWindowViewModelTests.cs` (+ nuova fixture fake)

- [ ] **Step 1: Aggiornare i test esistenti (RED)**

In `MainWindowViewModelTests`: il ctor di `MainWindowViewModel` perde `SettingsViewModel settings` e `IDiagnosticInfoLauncher` e guadagna `ISettingsWindowLauncher`. Creare `tests/.../Fixtures/FakeSettingsWindowLauncher.cs`:

```csharp
using Recordum.WPF.Services;
using Recordum.WPF.ViewModels.Settings;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeSettingsWindowLauncher : ISettingsWindowLauncher
{
    public List<SettingsCategoryId?> ShowCalls { get; } = [];
    public Task ShowAsync(SettingsCategoryId? initialCategory = null)
    { ShowCalls.Add(initialCategory); return Task.CompletedTask; }
}
```

Aggiungere al file di test (oltre ad aggiornare TUTTE le costruzioni del SUT):

```csharp
[Fact]
public async Task BackupSettings_opens_settings_window_at_backup_category()
{
    var sut = CreateSut(); // helper esistente del file, aggiornato col fake launcher
    await sut.BackupSettingsCommand.ExecuteAsync(null);
    _settingsLauncher.ShowCalls.Should().Equal(SettingsCategoryId.Backup);
}

[Fact]
public async Task ShowDiagnosticInfo_opens_settings_window_at_diagnostics_category()
{
    var sut = CreateSut();
    await sut.ShowDiagnosticInfoCommand.ExecuteAsync(null);
    _settingsLauncher.ShowCalls.Should().Equal(SettingsCategoryId.Diagnostics);
}

[Fact]
public async Task ShowSettings_opens_settings_window_at_default_category()
{
    var sut = CreateSut();
    await sut.ShowSettingsCommand.ExecuteAsync(null);
    _settingsLauncher.ShowCalls.Should().Equal((SettingsCategoryId?)null);
}
```

Run filtrato `MainWindowViewModel` → RED (compilazione).

- [ ] **Step 2: Implementare il rewiring**

In `MainWindowViewModel`:
- rimuovere: parametro ctor `SettingsViewModel settings`, property `Settings`, la chiamata `await Settings.InitializeAsync(ct)` in `InitializeAsync`, parametro/campo `IDiagnosticInfoLauncher`;
- aggiungere campo `private readonly ISettingsWindowLauncher _settingsLauncher;` + parametro ctor;
- sostituire i command:

```csharp
[RelayCommand] private Task BackupSettingsAsync() => _settingsLauncher.ShowAsync(SettingsCategoryId.Backup);
[RelayCommand] private Task ShowDiagnosticInfoAsync() => _settingsLauncher.ShowAsync(SettingsCategoryId.Diagnostics);
[RelayCommand] private Task ShowSettingsAsync() => _settingsLauncher.ShowAsync();
```

(using `Recordum.WPF.ViewModels.Settings` + `Recordum.WPF.Services`.)

- [ ] **Step 3: MainWindow.xaml**
- rimuovere il blocco `<Popup x:Name="SettingsPopupHost" …>` (righe ~115-121);
- nel `Menu` della barra superiore aggiungere, dopo il MenuItem Aiuto:

```xml
<MenuItem Header="{x:Static res:Strings.Settings_Title}" Command="{Binding ShowSettingsCommand}"/>
```

(Il menu intero sparirà in Fase B; questa voce è il punto d'ingresso ponte.)

- [ ] **Step 4: Run → GREEN** (filtro `MainWindowViewModel`), poi build 0/0.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ tests/Recordum.WPF.Tests/
git commit -m "refactor(wpf): route settings/diagnostics/backup-settings through SettingsWindow launcher"
```

---

### Task 7: Eliminare le UI assorbite

**Files:**
- Delete: `src/Recordum.WPF/Views/SettingsPopup.xaml`, `SettingsPopup.xaml.cs`
- Delete: `src/Recordum.WPF/Views/Backup/BackupSettingsDialog.xaml`, `.xaml.cs`
- Delete: `src/Recordum.WPF/Views/DiagnosticInfoDialog.xaml`, `.xaml.cs`
- Delete: `src/Recordum.WPF/Services/IDiagnosticInfoLauncher.cs`, `WpfDiagnosticInfoLauncher.cs`
- Modify: `src/Recordum.WPF/ViewModels/Backup/IBackupDialogLauncher.cs` (rimuovere `ShowSettingsAsync`), `src/Recordum.WPF/Services/WpfBackupDialogLauncher.cs` (rimuovere implementazione)
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` (rimuovere `IDiagnosticInfoLauncher` registration; `DiagnosticInfoViewModel` resta — lo usa la categoria)

- [ ] **Step 1: Cancellare i file e rimuovere i riferimenti**

```bash
git rm src/Recordum.WPF/Views/SettingsPopup.xaml src/Recordum.WPF/Views/SettingsPopup.xaml.cs
git rm src/Recordum.WPF/Views/Backup/BackupSettingsDialog.xaml src/Recordum.WPF/Views/Backup/BackupSettingsDialog.xaml.cs
git rm src/Recordum.WPF/Views/DiagnosticInfoDialog.xaml src/Recordum.WPF/Views/DiagnosticInfoDialog.xaml.cs
git rm src/Recordum.WPF/Services/IDiagnosticInfoLauncher.cs src/Recordum.WPF/Services/WpfDiagnosticInfoLauncher.cs
```

Poi sistemare i compile error residui (registration in ServiceRegistrar, `ShowSettingsAsync` nell'interfaccia/impl backup launcher, eventuali test del backup launcher che lo citano).

- [ ] **Step 2: Build 0/0 + suite WPF completa**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --nologo`
Expected: tutto verde (tranne, eventualmente, il flaky noto `CompletionRecorded_event_reloads…` — in tal caso verificarlo isolato).

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor(wpf): drop SettingsPopup, BackupSettingsDialog, DiagnosticInfoDialog (absorbed by SettingsWindow)"
```

---

### Task 8: Verifica visiva + tracker

- [ ] **Step 1: Lanciare con la skill `run`** e chiedere all'utente di verificare:
1. menu "Impostazioni" apre la finestra su Aspetto; cambio tema live (Sistema/Chiaro/Scuro);
2. Generale: orario reset salvato (riavvio → persiste), input invalido bordo rosso e non salvato;
3. Backup: valori auto-backup caricati/salvati, Test backup funziona, passphrase settabile;
4. menu "Backup → Impostazioni" apre direttamente la categoria Backup; "Aiuto → Diagnostica" apre Diagnostica;
5. Sync/Plugin/AI visibili, disabilitate (non selezionabili), pannello "In arrivo" MAI raggiungibile;
6. entrambi i temi; ridimensionamento finestra.

- [ ] **Step 2: Aggiornare `docs/phase-4-issues.md`** (sezione "Stato al 2026-06-11"): Fase A spec+plan eseguiti, stato `fixed` in attesa di verifica visiva; aggiornare anche la memoria di progetto.

- [ ] **Step 3: Commit finale docs.**

---

## Self-review (fatto in scrittura)

- **Spec coverage**: categorie (T4), finestra+port (T5), launcher/deep-link (T5/T6), menu ponte (T6), eliminazioni (T7), reset-time UI nuova (T3), placeholder approvati (T4 ComingSoon), stringhe EN/IT (T2), icone (T1), test+verifica (T3/T4/T6/T8). ✔
- **Port XAML**: i due punti "INSERIRE QUI" sono port 1:1 da file esistenti con istruzioni di trasformazione esplicite (niente Window tag; PasswordBox via handler DataContext-based) — scelta deliberata per non duplicare ~80 righe che l'esecutore copia dalla fonte.
- **Tipi coerenti**: `SettingsCategoryId`/`SettingsCategory`/`ComingSoonContent` usati uniformemente; `ShowAsync(SettingsCategoryId?)` ovunque. ✔
- **Rischi noti**: firme esatte di `IAppSettingService` e nomi dei fake in Fixtures da verificare al primo task che li tocca (annotato nei task); `DiagnosticInfoViewModel.LoadAsync` confermato dall'attuale launcher.
