# Creation pickers (colore + icona) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dare al builder "Costruisci da zero" e all'editor Configura due picker condivisi e gradevoli — colore (palette curata + recenti + hex) e icona (set Lucide curato + griglia cliccabile con ricerca) — potenziando i due controlli `ColorSwatchPicker` / `IconPicker` già esistenti.

**Architecture:** I controlli restano UserControl VM-agnostici con Dependency Property (`Color`, `Icon`, nuova `RecentColors`). La persistenza dei recenti vive in un servizio UI (`IRecentColorStore` su `IAppSettingService`). Il catalogo icone è un tipo statico testabile; le geometrie Lucide sono risorse in `Icons.xaml`. I view-model host (builder/editor) caricano i recenti e li registrano al commit reale.

**Tech Stack:** WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions (`[WpfFact]` per i test che toccano risorse/DP WPF), `IAppSettingService` (EF/SQLCipher dietro).

**Spec:** `docs/superpowers/specs/2026-06-09-creation-pickers-design.md`

**Convenzioni del repo (importanti):**
- Test runner: `dotnet test tests/Recordum.WPF.Tests --nologo`. Per un singolo test: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~<Nome>"`.
- **L'app WPF blocca i propri binari quando è in esecuzione** — assicurarsi che non sia aperta prima di buildare (`Get-Process Recordum.WPF | Stop-Process -Force`).
- Build del progetto: `dotnet build src/Recordum.WPF` deve restare **0 warning / 0 errori** (TreatWarningsAsErrors attivo su src). I test tollerano warning.
- Stringhe utente in `.resx` (English-first). Nuove stringhe → `Resources/Strings.resx` + `Strings.it.resx` + `Strings.Designer.cs`.
- Commit message in inglese, footer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

## File Structure

- `src/Recordum.WPF/Services/IRecentColorStore.cs` — **nuovo**: interfaccia store recenti.
- `src/Recordum.WPF/Services/RecentColorStore.cs` — **nuovo**: impl su `IAppSettingService` (CSV `ui.recent_colors`).
- `src/Recordum.WPF/Services/IconCatalog.cs` — **nuovo**: lista nomi icone + `Search`.
- `src/Recordum.WPF/Views/ColorSwatchPicker.xaml(.cs)` — **modifica**: popup, griglia palette estesa, riga recenti, DP `RecentColors`.
- `src/Recordum.WPF/Views/IconPicker.xaml(.cs)` — **modifica**: popup, griglia, ricerca.
- `src/Recordum.WPF/Resources/Icons.xaml` — **modifica**: +~40 geometrie Lucide.
- `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs` — **modifica**: dep `IRecentColorStore`, `RecentColors`, record su Launch.
- `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs` — **modifica**: dep `IRecentColorStore`, `RecentColors`, record su color-change valido.
- `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml` — **modifica**: adozione dei due picker.
- `src/Recordum.WPF/Views/EditorPane.xaml` — **modifica**: bind `RecentColors`.
- `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` — **modifica**: DI `IRecentColorStore`.
- `tests/Recordum.WPF.Tests/Services/RecentColorStoreTests.cs` — **nuovo**.
- `tests/Recordum.WPF.Tests/Services/IconCatalogTests.cs` — **nuovo**.
- `tests/Recordum.WPF.Tests/Resources/IconResourceAlignmentTests.cs` — **nuovo**.
- `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs` — **modifica**: 2 test recenti.
- `tests/Recordum.WPF.Tests/Fixtures/FakeAppSettingService.cs` — **riuso** (verificare che esista; vedi Task 1 Step 1).

---

## Task 1: RecentColorStore (servizio recenti)

**Files:**
- Create: `src/Recordum.WPF/Services/IRecentColorStore.cs`
- Create: `src/Recordum.WPF/Services/RecentColorStore.cs`
- Test: `tests/Recordum.WPF.Tests/Services/RecentColorStoreTests.cs`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` (DI)

- [ ] **Step 1: Verificare la fake AppSetting esistente**

Cercare una fake per `IAppSettingService` nei test: `grep -r "IAppSettingService" tests/Recordum.WPF.Tests/Fixtures`. Esiste già `FakeAppSettingService` con un `Dictionary<string,string> Store` (è usata in `MainWindowViewModelTests` come `parts.AppSettings.Store[...]`). Usarla. Se per qualche motivo non c'è, crearla:

```csharp
// tests/Recordum.WPF.Tests/Fixtures/FakeAppSettingService.cs  (solo se mancante)
using Recordum.Core.Abstractions.Services;
namespace Recordum.WPF.Tests.Fixtures;
public sealed class FakeAppSettingService : IAppSettingService
{
    public Dictionary<string, string> Store { get; } = new();
    public ValueTask<string?> GetAsync(string key, CancellationToken ct = default)
        => new(Store.TryGetValue(key, out var v) ? v : null);
    public ValueTask SetAsync(string key, string value, CancellationToken ct = default) { Store[key] = value; return ValueTask.CompletedTask; }
    public ValueTask<T?> GetEnumAsync<T>(string key, CancellationToken ct = default) where T : struct, Enum
        => new(Store.TryGetValue(key, out var v) && Enum.TryParse<T>(v, out var e) ? e : default(T?));
    public ValueTask SetEnumAsync<T>(string key, T value, CancellationToken ct = default) where T : struct, Enum { Store[key] = value.ToString(); return ValueTask.CompletedTask; }
}
```

- [ ] **Step 2: Scrivere i test (falliscono — tipi non esistono)**

```csharp
// tests/Recordum.WPF.Tests/Services/RecentColorStoreTests.cs
using FluentAssertions;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Xunit;

namespace Recordum.WPF.Tests.Services;

public class RecentColorStoreTests
{
    private static RecentColorStore NewSut(out FakeAppSettingService settings)
    {
        settings = new FakeAppSettingService();
        return new RecentColorStore(settings);
    }

    [Fact]
    public async Task AddAsync_puts_color_at_front()
    {
        var sut = NewSut(out _);
        await sut.AddAsync("#ff0000");
        await sut.AddAsync("#00ff00");
        (await sut.GetAsync()).Should().ContainInOrder("#00ff00", "#ff0000");
    }

    [Fact]
    public async Task AddAsync_dedups_and_promotes_existing()
    {
        var sut = NewSut(out _);
        await sut.AddAsync("#ff0000");
        await sut.AddAsync("#00ff00");
        await sut.AddAsync("#ff0000"); // re-add → moves to front, no dup
        (await sut.GetAsync()).Should().Equal("#ff0000", "#00ff00");
    }

    [Fact]
    public async Task AddAsync_caps_at_eight()
    {
        var sut = NewSut(out _);
        for (int i = 0; i < 10; i++) await sut.AddAsync($"#0000{i:x2}");
        var recents = await sut.GetAsync();
        recents.Should().HaveCount(8);
        recents[0].Should().Be("#000009"); // newest
    }

    [Fact]
    public async Task AddAsync_ignores_invalid_hex()
    {
        var sut = NewSut(out _);
        await sut.AddAsync("not-a-color");
        await sut.AddAsync("#fff"); // 3-digit not supported → invalid
        (await sut.GetAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_normalizes_to_lowercase()
    {
        var sut = NewSut(out _);
        await sut.AddAsync("#AABBCC");
        (await sut.GetAsync()).Should().Equal("#aabbcc");
    }

    [Fact]
    public async Task GetAsync_roundtrips_persisted_csv()
    {
        var sut = NewSut(out var settings);
        settings.Store["ui.recent_colors"] = "#111111,#222222";
        (await sut.GetAsync()).Should().Equal("#111111", "#222222");
    }
}
```

- [ ] **Step 3: Run i test — verificare FAIL (compilazione: `RecentColorStore` non esiste)**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~RecentColorStoreTests" --nologo`
Expected: errore di compilazione / FAIL.

- [ ] **Step 4: Implementare interfaccia + store**

```csharp
// src/Recordum.WPF/Services/IRecentColorStore.cs
namespace Recordum.WPF.Services;

/// <summary>Recently-picked habit colors (UI state, persisted in AppSetting). Most-recent-first.</summary>
public interface IRecentColorStore
{
    ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default);
    ValueTask AddAsync(string hex, CancellationToken ct = default);
}
```

```csharp
// src/Recordum.WPF/Services/RecentColorStore.cs
using System.Text.RegularExpressions;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.Services;

/// <summary>Stores up to 8 recently-picked #RRGGBB colors as a CSV AppSetting, most-recent-first,
/// de-duplicated. Invalid hex is ignored.</summary>
public sealed partial class RecentColorStore : IRecentColorStore
{
    internal const string SettingKey = "ui.recent_colors";
    internal const int Cap = 8;

    private readonly IAppSettingService _settings;
    public RecentColorStore(IAppSettingService settings) => _settings = settings;

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexPattern();

    public async ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default)
    {
        var raw = await _settings.GetAsync(SettingKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async ValueTask AddAsync(string hex, CancellationToken ct = default)
    {
        if (hex is null || !HexPattern().IsMatch(hex)) return;
        var norm = hex.ToLowerInvariant();
        var list = new List<string>(await GetAsync(ct).ConfigureAwait(false));
        list.RemoveAll(c => string.Equals(c, norm, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, norm);
        if (list.Count > Cap) list.RemoveRange(Cap, list.Count - Cap);
        await _settings.SetAsync(SettingKey, string.Join(',', list), ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 5: Registrare in DI**

In `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`, accanto agli altri servizi UI (es. dopo `services.AddSingleton<IThemeService, ThemeService>();`), aggiungere:

```csharp
        services.AddScoped<IRecentColorStore, RecentColorStore>();
```

(Scoped: dipende da `IAppSettingService` che è Scoped — vedi riga 99 del registrar. Verificare l'ordine: registrare dopo che `IAppSettingService` è disponibile non conta per la DI, ma mantenere la coerenza scope evita captive-dependency.)

- [ ] **Step 6: Run i test — verificare PASS**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~RecentColorStoreTests" --nologo`
Expected: PASS (6 test).

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/Services/IRecentColorStore.cs src/Recordum.WPF/Services/RecentColorStore.cs src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs tests/Recordum.WPF.Tests/Services/RecentColorStoreTests.cs tests/Recordum.WPF.Tests/Fixtures/FakeAppSettingService.cs
git commit -m "feat(wpf): RecentColorStore — persisted recent habit colors (cap 8, dedup)"
```

---

## Task 2: ColorSwatchPicker potenziato (palette + recenti + hex in popup)

**Files:**
- Modify: `src/Recordum.WPF/Views/ColorSwatchPicker.xaml`
- Modify: `src/Recordum.WPF/Views/ColorSwatchPicker.xaml.cs`

> Nessun unit test: è un UserControl XAML. Verifica visiva nel Task 6 (relaunch). Il rischio logico (recenti/dedup) è già coperto dal Task 1.

- [ ] **Step 1: Estendere la palette + aggiungere la DP `RecentColors` (code-behind)**

In `ColorSwatchPicker.xaml.cs` sostituire la costante `Palette` con una palette curata multi-tonalità/sfumatura e aggiungere la DP `RecentColors`:

```csharp
public static readonly ImmutableArray<string> Palette = ImmutableArray.Create(
    // hue ramps: light / base / dark  (Tailwind 300/500/700-ish)
    "#fca5a5", "#ef4444", "#b91c1c",   // red
    "#fdba74", "#f97316", "#c2410c",   // orange
    "#fcd34d", "#f59e0b", "#b45309",   // amber
    "#bef264", "#84cc16", "#4d7c0f",   // lime
    "#6ee7b7", "#10b981", "#047857",   // emerald
    "#67e8f9", "#06b6d4", "#0e7490",   // cyan
    "#93c5fd", "#3b82f6", "#1d4ed8",   // blue
    "#a5b4fc", "#6366f1", "#4338ca",   // indigo
    "#c4b5fd", "#8b5cf6", "#6d28d9",   // violet
    "#f0abfc", "#d946ef", "#a21caf",   // fuchsia
    "#f9a8d4", "#ec4899", "#be185d",   // pink
    "#cbd5e1", "#94a3b8", "#475569");  // slate (neutrals)

public static readonly DependencyProperty RecentColorsProperty = DependencyProperty.Register(
    nameof(RecentColors),
    typeof(IEnumerable<string>),
    typeof(ColorSwatchPicker),
    new PropertyMetadata(Array.Empty<string>()));

public IEnumerable<string> RecentColors
{
    get => (IEnumerable<string>)GetValue(RecentColorsProperty);
    set => SetValue(RecentColorsProperty, value);
}

public bool HasRecentColors => RecentColors?.Any() == true;
```

Aggiungere `using System.Linq;` se non presente. Lasciare invariati `Color`/`ColorProperty` e `OnSwatchClick`.

- [ ] **Step 2: Aggiungere il close-popup all'handler di click + un campo Popup**

Nel code-behind aggiungere un riferimento al popup (impostato via XAML `x:Name="PickerPopup"`) e chiuderlo dopo la scelta. Aggiornare `OnSwatchClick` così:

```csharp
private void OnSwatchClick(object sender, MouseButtonEventArgs e)
{
    if (sender is FrameworkElement fe && fe.DataContext is string hex)
    {
        Color = hex;
        PickerPopup.IsOpen = false;
        e.Handled = true;
    }
}

private void OnTriggerClick(object sender, MouseButtonEventArgs e)
{
    PickerPopup.IsOpen = !PickerPopup.IsOpen;
    e.Handled = true;
}
```

- [ ] **Step 3: Riscrivere lo XAML — trigger + popup (palette + recenti + hex)**

Sostituire l'intero contenuto di `ColorSwatchPicker.xaml` (dopo gli xmlns) con:

```xml
<StackPanel Orientation="Horizontal">
    <!-- Trigger: current color swatch (opens the popup) -->
    <Border x:Name="Trigger" Width="28" Height="28" CornerRadius="4"
            Cursor="Hand"
            BorderBrush="{DynamicResource Brush.Border}" BorderThickness="1"
            Background="{Binding Color, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}, Converter={StaticResource HexToBrush}}"
            MouseLeftButtonUp="OnTriggerClick"/>

    <Popup x:Name="PickerPopup" PlacementTarget="{Binding ElementName=Trigger}"
           Placement="Bottom" StaysOpen="False" AllowsTransparency="True">
        <Border Background="{DynamicResource Brush.SurfaceElevated}"
                BorderBrush="{DynamicResource Brush.Border}" BorderThickness="1"
                CornerRadius="6" Padding="10">
            <StackPanel Width="232">
                <!-- Palette grid: 3 swatches per hue column, 12 columns -->
                <ItemsControl ItemsSource="{Binding Source={x:Static local:ColorSwatchPicker.Palette}}">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Width="20" Height="20" Margin="2" CornerRadius="3"
                                    Cursor="Hand"
                                    Background="{Binding ., Converter={StaticResource HexToBrush}}"
                                    MouseLeftButtonUp="OnSwatchClick"/>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <!-- Recents -->
                <TextBlock Text="{x:Static res:Strings.ColorPicker_Recent}"
                           Margin="0,8,0,2"
                           FontSize="{StaticResource FontSize.Small}"
                           Foreground="{DynamicResource Brush.TextMuted}"
                           Visibility="{Binding HasRecentColors, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}, Converter={StaticResource BoolToVisible}}"/>
                <ItemsControl ItemsSource="{Binding RecentColors, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}}"
                              Visibility="{Binding HasRecentColors, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}, Converter={StaticResource BoolToVisible}}">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Width="20" Height="20" Margin="2" CornerRadius="3"
                                    Cursor="Hand"
                                    Background="{Binding ., Converter={StaticResource HexToBrush}}"
                                    MouseLeftButtonUp="OnSwatchClick"/>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <!-- Hex entry + preview -->
                <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                    <TextBox Width="100" VerticalContentAlignment="Center"
                             Text="{Binding Color, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}, UpdateSourceTrigger=LostFocus}"/>
                    <Border Width="24" Height="24" Margin="8,0,0,0" CornerRadius="3"
                            BorderBrush="{DynamicResource Brush.Border}" BorderThickness="1"
                            Background="{Binding Color, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}, Converter={StaticResource HexToBrush}}"/>
                </StackPanel>
            </StackPanel>
        </Border>
    </Popup>
</StackPanel>
```

Aggiungere agli xmlns del file: `xmlns:res="clr-namespace:Recordum.WPF.Resources"`. Verificare che `HexToBrush`, `BoolToVisible`, `FontSize.Small`, i brush `Brush.*` siano risorse globali (lo sono — usate ovunque, vedi `HabitBuilderView.xaml`).

- [ ] **Step 4: Aggiungere la stringa resx `ColorPicker.Recent`**

In `src/Recordum.WPF/Resources/Strings.resx`:
```xml
  <data name="ColorPicker.Recent" xml:space="preserve">
    <value>Recent</value>
  </data>
```
In `src/Recordum.WPF/Resources/Strings.it.resx`:
```xml
  <data name="ColorPicker.Recent" xml:space="preserve">
    <value>Recenti</value>
  </data>
```
In `src/Recordum.WPF/Resources/Strings.Designer.cs` aggiungere la proprietà generata (copiare il pattern di una proprietà esistente, es. `HabitBuilder_Series`):
```csharp
        public static string ColorPicker_Recent {
            get {
                return ResourceManager.GetString("ColorPicker.Recent", resourceCulture);
            }
        }
```

- [ ] **Step 5: Build — verificare 0/0**

Run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`. (Se XAML parse error, controllare gli xmlns aggiunti.)

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/Views/ColorSwatchPicker.xaml src/Recordum.WPF/Views/ColorSwatchPicker.xaml.cs src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
git commit -m "feat(wpf): ColorSwatchPicker popup with curated palette + recents + hex"
```

---

## Task 3: Adozione colore (builder + editor) + persistenza recenti

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml`
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs`
- Modify: `src/Recordum.WPF/Views/EditorPane.xaml`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs`

- [ ] **Step 1: Test builder — recenti caricati e registrati (FAIL)**

Aggiungere a `HabitBuilderViewModelTests.cs`. Nota: `NewSut` va esteso per accettare un `IRecentColorStore`. Aggiornare l'helper e aggiungere i test:

```csharp
// aggiornare l'helper NewSut esistente:
private static HabitBuilderViewModel NewSut(
    IComposedHabitService? composed = null,
    IHabitTemplateService? templates = null,
    IReferenceDataQueryService? refData = null,
    IRecentColorStore? recentColors = null)
    => new(composed ?? new FakeComposedHabitService(), templates ?? new FakeHabitTemplateService(),
           refData ?? new FakeReferenceData(), new FakeClock(),
           recentColors ?? new RecentColorStore(new FakeAppSettingService()));
```

```csharp
[WpfFact]
public async Task InitializeAsync_loads_recent_colors()
{
    var store = new RecentColorStore(new FakeAppSettingService());
    await store.AddAsync("#123456");
    var sut = NewSut(recentColors: store);

    await sut.InitializeAsync();

    sut.RecentColors.Should().Contain("#123456");
}

[WpfFact]
public async Task Launch_records_the_picked_color_as_recent()
{
    var settings = new FakeAppSettingService();
    var store = new RecentColorStore(settings);
    var sut = NewSut(recentColors: store);
    sut.Name = "Corsa";
    sut.Color = "#abcdef";
    sut.AddFieldCommand.Execute(FieldNature.Count);

    await sut.LaunchCommand.ExecuteAsync(null);

    (await store.GetAsync()).Should().Contain("#abcdef");
}
```

Aggiungere gli `using` necessari al file di test: `using Recordum.WPF.Services;`.

- [ ] **Step 2: Run — FAIL (ctor arity + `RecentColors` mancante)**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~HabitBuilderViewModelTests" --nologo`
Expected: errore di compilazione.

- [ ] **Step 3: Builder VM — dep + collezione + load + record**

In `HabitBuilderViewModel.cs`:

```csharp
// campo + ctor (aggiungere il parametro in coda, aggiornare l'assegnazione):
private readonly IRecentColorStore _recentColors;

public HabitBuilderViewModel(IComposedHabitService composed, IHabitTemplateService templates,
    IReferenceDataQueryService refData, IClock clock, IRecentColorStore recentColors)
{
    _composed = composed;
    _templates = templates;
    _refData = refData;
    _clock = clock;
    _recentColors = recentColors;
}
```

```csharp
// collezione esposta al picker (accanto ad AvailableUnits/AvailableScales):
public ObservableCollection<string> RecentColors { get; } = new();
```

In `InitializeAsync`, dopo aver popolato i time-unit, caricare i recenti:
```csharp
        RecentColors.Clear();
        foreach (var c in await _recentColors.GetAsync(ct).ConfigureAwait(true)) RecentColors.Add(c);
```

In `LaunchAsync`, dopo la creazione riuscita, registrare il colore:
```csharp
    private async Task LaunchAsync(CancellationToken ct)
    {
        CreatedHabitNodeId = await _composed.CreateComposedHabitAsync(BuildRequest(), ct).ConfigureAwait(true);
        await _recentColors.AddAsync(Color, ct).ConfigureAwait(true);
    }
```

Aggiungere `using Recordum.WPF.Services;` se non presente.

- [ ] **Step 4: Builder XAML — sostituire la TextBox colore col picker**

In `HabitBuilderView.xaml`, nella zona testata, **sostituire** il blocco "Color" (`StackPanel` Grid.Column=0 con `TextBox Text="{Binding Color...}"`) **e** il "Color preview" Border con un singolo `ColorSwatchPicker`. Risultato per quella riga:

```xml
<!-- Color -->
<StackPanel Grid.Column="0" Margin="0,0,12,0">
    <TextBlock Text="{x:Static res:Strings.HabitBuilder_Color}"
               FontSize="{StaticResource FontSize.Small}"
               Foreground="{DynamicResource Brush.TextMuted}"
               Margin="0,0,0,2"/>
    <v:ColorSwatchPicker Color="{Binding Color, Mode=TwoWay}"
                         RecentColors="{Binding RecentColors}"
                         HorizontalAlignment="Left"/>
</StackPanel>
```

Aggiungere agli xmlns del file: `xmlns:v="clr-namespace:Recordum.WPF.Views"` (verificare; se assente aggiungerlo). Il `Grid.Column="1"` del preview ora è libero — rimuovere il Border "Color preview" (il picker ha già la sua anteprima). La `ColumnDefinition` può restare (Width=Auto, vuota) per non disturbare il layout della colonna "Icon" a destra.

- [ ] **Step 5: Editor VM — dep + collezione + load + record su color-change valido**

In `HabitEditorViewModel.cs`:
- Aggiungere il parametro ctor `IRecentColorStore recentColors` (in coda) + campo `_recentColors` + assegnazione + `ArgumentNullException.ThrowIfNull` se lo stile del ctor lo usa.
- Aggiungere `public ObservableCollection<string> RecentColors { get; } = new();`
- In `LoadAsync`, dopo `Color = node.Color;` (o a fine load), popolare:
```csharp
        RecentColors.Clear();
        foreach (var c in await _recentColors.GetAsync(ct).ConfigureAwait(true)) RecentColors.Add(c);
```
- In `OnColorChanged(string value)`, **dopo** il check di validità che passa (quando `HexColorPattern.IsMatch(value)` è true e il colore viene persistito), registrare il recente:
```csharp
        _ = _recentColors.AddAsync(value);
```
(fire-and-forget coerente con l'autosave fire-and-forget già presente lì).

Aggiungere `using Recordum.WPF.Services;` se non presente.

- [ ] **Step 6: Editor XAML — bind RecentColors**

In `EditorPane.xaml`, sul `ColorSwatchPicker` esistente aggiungere l'attributo:
```xml
<v:ColorSwatchPicker Margin="8,0,0,0"
                     Color="{Binding Color, Mode=TwoWay}"
                     RecentColors="{Binding RecentColors}"/>
```

- [ ] **Step 7: Run i test builder — PASS**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~HabitBuilderViewModelTests" --nologo`
Expected: PASS (inclusi i 2 nuovi).

- [ ] **Step 8: Run intera suite WPF — PASS (regressioni ctor editor)**

Run: `dotnet test tests/Recordum.WPF.Tests --nologo`
Expected: PASS. **Probabile rottura**: i factory di test che costruiscono `HabitEditorViewModel` (es. `HabitEditorViewModelTests`, eventuale `MainWindowViewModelFactory`) ora mancano l'argomento `IRecentColorStore`. Cercarli (`grep -rn "new HabitEditorViewModel(" tests`) e passare `new RecentColorStore(new FakeAppSettingService())`. Ricompilare finché verde.

- [ ] **Step 9: Build src — 0/0**

Run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat(wpf): adopt color picker in builder + editor, record recents on commit"
```

---

## Task 4: IconCatalog + import geometrie Lucide + test allineamento

**Files:**
- Create: `src/Recordum.WPF/Services/IconCatalog.cs`
- Modify: `src/Recordum.WPF/Resources/Icons.xaml`
- Test: `tests/Recordum.WPF.Tests/Services/IconCatalogTests.cs`
- Test: `tests/Recordum.WPF.Tests/Resources/IconResourceAlignmentTests.cs`

- [ ] **Step 1: Test IconCatalog.Search (FAIL)**

```csharp
// tests/Recordum.WPF.Tests/Services/IconCatalogTests.cs
using FluentAssertions;
using Recordum.WPF.Services;
using Xunit;

namespace Recordum.WPF.Tests.Services;

public class IconCatalogTests
{
    [Fact]
    public void All_is_non_empty()
        => IconCatalog.All.Should().NotBeEmpty();

    [Fact]
    public void Search_empty_returns_all()
        => IconCatalog.Search("").Should().Equal(IconCatalog.All);

    [Fact]
    public void Search_null_returns_all()
        => IconCatalog.Search(null).Should().Equal(IconCatalog.All);

    [Fact]
    public void Search_is_case_insensitive_substring_over_the_name()
    {
        // "Icon.Dumbbell" should match "dumb"
        IconCatalog.All.Should().Contain("Icon.Dumbbell");
        IconCatalog.Search("dumb").Should().Contain("Icon.Dumbbell");
        IconCatalog.Search("DUMB").Should().Contain("Icon.Dumbbell");
    }

    [Fact]
    public void Search_no_match_is_empty()
        => IconCatalog.Search("zzzznomatch").Should().BeEmpty();
}
```

- [ ] **Step 2: Run — FAIL (IconCatalog non esiste)**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~IconCatalogTests" --nologo`
Expected: FAIL (compilazione).

- [ ] **Step 3: Implementare IconCatalog**

I nomi sono le **resource key** (prefisso `Icon.`); la ricerca ignora il prefisso. La lista deve corrispondere alle key aggiunte in `Icons.xaml` allo Step 4.

```csharp
// src/Recordum.WPF/Services/IconCatalog.cs
namespace Recordum.WPF.Services;

/// <summary>Curated, searchable list of Lucide icon resource keys available to the icon picker.
/// Each entry is a key into Resources/Icons.xaml. Keep in sync with that file (covered by a test).</summary>
public static class IconCatalog
{
    public static IReadOnlyList<string> All { get; } = new[]
    {
        // movement / sport
        "Icon.Dumbbell", "Icon.Bike", "Icon.Footprints", "Icon.Trophy", "Icon.Mountain",
        // health / medical
        "Icon.Heart", "Icon.HeartPulse", "Icon.Pill", "Icon.Stethoscope", "Icon.Activity",
        // mind / sleep
        "Icon.Brain", "Icon.Moon", "Icon.Sun", "Icon.Smile", "Icon.Leaf",
        // study / work
        "Icon.BookOpen", "Icon.GraduationCap", "Icon.PenLine", "Icon.Laptop", "Icon.Target",
        // food / hydration
        "Icon.Utensils", "Icon.Apple", "Icon.Coffee", "Icon.Droplet", "Icon.GlassWater",
        // home / care
        "Icon.Home", "Icon.Bath", "Icon.Brush", "Icon.Shirt", "Icon.Sprout",
        // abstinence / control
        "Icon.Cigarette", "Icon.Wine", "Icon.Ban", "Icon.ShieldCheck", "Icon.Flame",
        // misc / general
        "Icon.Star", "Icon.CheckCircle", "Icon.Calendar", "Icon.Clock", "Icon.Music",
    };

    public static IReadOnlyList<string> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return All;
        var q = query.Trim();
        return All.Where(k => Name(k).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // "Icon.BookOpen" → "BookOpen" (the searchable name).
    private static string Name(string key) => key.StartsWith("Icon.", StringComparison.Ordinal) ? key[5..] : key;
}
```

- [ ] **Step 4: Importare ~40 geometrie Lucide in Icons.xaml**

Per **ogni** key in `IconCatalog.All`, aggiungere una `<Geometry x:Key="...">` in `src/Recordum.WPF/Resources/Icons.xaml`. **Non inventare i path** — sourcarli dalle SVG ufficiali Lucide:

Sorgente per icona `<slug>` (kebab-case del nome, es. `book-open`, `heart-pulse`, `glass-water`, `graduation-cap`, `pen-line`, `shield-check`, `check-circle` → in Lucide è `circle-check`):
`https://raw.githubusercontent.com/lucide-icons/lucide/main/icons/<slug>.svg`
(usare `WebFetch` per scaricare; mappa nome→slug Lucide quando differiscono — verificare su lucide.dev se un fetch dà 404).

**Conversione SVG → Geometry mini-language** (stesso stile delle 7 esistenti, viewBox 24×24, stroke-based):
- Ogni `<path d="…">` → copiare il `d` così com'è.
- `<line x1 y1 x2 y2>` → `M x1 y1 L x2 y2`.
- `<circle cx cy r>` → due archi: `M (cx-r) cy a r r 0 1 1 (2r) 0 a r r 0 1 1 (-2r) 0z` (cfr. `Icon.Search`/`Icon.Settings` esistenti).
- `<rect x y w h rx>` → `M (x+rx) y h (w-2rx) a rx rx 0 0 1 rx rx v (h-2rx) a rx rx 0 0 1 -rx rx h -(w-2rx) a rx rx 0 0 1 -rx -rx v -(h-2rx) a rx rx 0 0 1 rx -rxz` (per rx=0 è un rettangolo semplice `M x y h w v h h -w z`).
- `<polyline points="x1,y1 x2,y2 …">` → `M x1 y1 L x2 y2 L …`.
- `<polygon …>` → come polyline + `z`.
- Concatenare i sotto-tracciati con uno spazio (più sotto-tracciati per `Geometry`, cfr. `Icon.Pin`).

Esempi concreti (formato atteso — verificare i path reali col fetch, qui solo a scopo di template):
```xml
    <Geometry x:Key="Icon.Heart">M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z</Geometry>
    <Geometry x:Key="Icon.Droplet">M12 22a7 7 0 0 0 7-7c0-2-1-3.9-3-5.5s-3.5-4-4-6.5c-.5 2.5-2 4.9-4 6.5C6 11.1 5 13 5 15a7 7 0 0 0 7 7z</Geometry>
    <Geometry x:Key="Icon.Clock">M12 6v6l4 2 M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z</Geometry>
```

> Importante: se per una key il fetch Lucide è 404 (nome cambiato tra versioni), **scegliere un'icona equivalente esistente e aggiornare sia la key in `Icons.xaml` sia in `IconCatalog.All`** così restano allineati (il test allo Step 6 lo garantisce).

- [ ] **Step 5: Test allineamento catalogo↔risorse (scrivere)**

```csharp
// tests/Recordum.WPF.Tests/Resources/IconResourceAlignmentTests.cs
using System.Windows;
using FluentAssertions;
using Recordum.WPF.Services;
using Xunit;

namespace Recordum.WPF.Tests.Resources;

public class IconResourceAlignmentTests
{
    [WpfFact]
    public void Every_catalog_key_resolves_to_a_geometry()
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Recordum.WPF;component/Resources/Icons.xaml")
        };

        foreach (var key in IconCatalog.All)
            dict.Contains(key).Should().BeTrue($"icon resource '{key}' must exist in Icons.xaml");
    }
}
```

- [ ] **Step 6: Run i test icone — PASS**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~IconCatalog|FullyQualifiedName~IconResourceAlignment" --nologo`
Expected: PASS. Se l'alignment fallisce su una key → mancano/typo nella geometria in `Icons.xaml` (correggere il nome o aggiungere la geometria).

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/Services/IconCatalog.cs src/Recordum.WPF/Resources/Icons.xaml tests/Recordum.WPF.Tests/Services/IconCatalogTests.cs tests/Recordum.WPF.Tests/Resources/IconResourceAlignmentTests.cs
git commit -m "feat(wpf): curated Lucide icon catalog + ~40 geometries + alignment test"
```

---

## Task 5: IconPicker potenziato (popup + griglia + ricerca)

**Files:**
- Modify: `src/Recordum.WPF/Views/IconPicker.xaml`
- Modify: `src/Recordum.WPF/Views/IconPicker.xaml.cs`

> Verifica visiva nel Task 6. Logica di ricerca già coperta da `IconCatalogTests`.

- [ ] **Step 1: Code-behind — collezione filtrata + handlers**

Estendere `IconPicker.xaml.cs`: aggiungere una `ObservableCollection<string> Results`, un campo Popup (`x:Name="PickerPopup"`), e gli handler trigger/search/select. Mantenere `Icon`/`SelectedIconGeometry` invariati.

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using Recordum.WPF.Services;
// ...
public ObservableCollection<string> Results { get; } = new();

public IconPicker()
{
    InitializeComponent();
    Reset(IconCatalog.All);
}

private void Reset(IReadOnlyList<string> keys)
{
    Results.Clear();
    foreach (var k in keys) Results.Add(k);
}

private void OnTriggerClick(object sender, MouseButtonEventArgs e)
{
    PickerPopup.IsOpen = !PickerPopup.IsOpen;
    e.Handled = true;
}

private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
{
    if (sender is System.Windows.Controls.TextBox tb) Reset(IconCatalog.Search(tb.Text));
}

private void OnIconClick(object sender, MouseButtonEventArgs e)
{
    if (sender is FrameworkElement fe && fe.DataContext is string key)
    {
        Icon = key;
        PickerPopup.IsOpen = false;
        e.Handled = true;
    }
}
```

- [ ] **Step 2: XAML — trigger box + popup (search + grid)**

Sostituire il contenuto di `IconPicker.xaml` (dopo gli xmlns; aggiungere `xmlns:res="clr-namespace:Recordum.WPF.Resources"`) con:

```xml
<StackPanel Orientation="Horizontal">
    <!-- Trigger: current icon preview (opens popup) -->
    <Border x:Name="Trigger" Width="28" Height="28" Cursor="Hand"
            BorderBrush="{DynamicResource Brush.Border}" BorderThickness="1"
            CornerRadius="3" Background="Transparent" Padding="4"
            MouseLeftButtonUp="OnTriggerClick">
        <Path Stretch="Uniform"
              Stroke="{DynamicResource Brush.Text}" StrokeThickness="1.5"
              StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
              Data="{Binding SelectedIconGeometry, RelativeSource={RelativeSource AncestorType={x:Type local:IconPicker}}}"/>
    </Border>

    <Popup x:Name="PickerPopup" PlacementTarget="{Binding ElementName=Trigger}"
           Placement="Bottom" StaysOpen="False" AllowsTransparency="True">
        <Border Background="{DynamicResource Brush.SurfaceElevated}"
                BorderBrush="{DynamicResource Brush.Border}" BorderThickness="1"
                CornerRadius="6" Padding="10" Width="240">
            <StackPanel>
                <TextBox TextChanged="OnSearchChanged"
                         Padding="6,4" Margin="0,0,0,8"
                         ToolTip="{x:Static res:Strings.IconPicker_SearchPrompt}"/>
                <ScrollViewer MaxHeight="200" VerticalScrollBarVisibility="Auto">
                    <ItemsControl ItemsSource="{Binding Results, RelativeSource={RelativeSource AncestorType={x:Type local:IconPicker}}}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Width="32" Height="32" Margin="3" Cursor="Hand"
                                        BorderBrush="{DynamicResource Brush.Border}" BorderThickness="1"
                                        CornerRadius="3" Background="Transparent" Padding="5"
                                        MouseLeftButtonUp="OnIconClick">
                                    <Path Stretch="Uniform"
                                          Stroke="{DynamicResource Brush.Text}" StrokeThickness="1.5"
                                          StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
                                          Data="{Binding ., Converter={StaticResource IconKeyToGeometry}}"/>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </ScrollViewer>
            </StackPanel>
        </Border>
    </Popup>
</StackPanel>
```

- [ ] **Step 3: Converter `IconKeyToGeometry`**

Serve un converter che risolva una resource key → `Geometry` via `Application.Current.TryFindResource`. Verificare se esiste (`grep -rn "IconKeyToGeometry\|TryFindResource" src/Recordum.WPF/Converters`); se no, crearlo:

```csharp
// src/Recordum.WPF/Converters/IconKeyToGeometryConverter.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

public sealed class IconKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string key && Application.Current?.TryFindResource(key) is Geometry g ? g : null;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

Registrarlo tra le risorse globali accanto agli altri converter (cercare dove `HexToBrush`/`BoolToVisible` sono dichiarati: `grep -rn "HexToBrush" src/Recordum.WPF/App.xaml src/Recordum.WPF/Resources`) e aggiungere:
```xml
<conv:IconKeyToGeometryConverter x:Key="IconKeyToGeometry"/>
```
(con l'xmlns `conv` del namespace converter già usato lì).

- [ ] **Step 4: resx `IconPicker.SearchPrompt`**

Aggiungere `IconPicker.SearchPrompt` a `Strings.resx` ("Search icons"), `Strings.it.resx` ("Cerca icone") e la proprietà in `Strings.Designer.cs` (`IconPicker_SearchPrompt`), come nello Step 4 del Task 2.

- [ ] **Step 5: Build src — 0/0**

Run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/Views/IconPicker.xaml src/Recordum.WPF/Views/IconPicker.xaml.cs src/Recordum.WPF/Converters/IconKeyToGeometryConverter.cs src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
# + il file App.xaml/Resources dove è stato registrato il converter
git commit -m "feat(wpf): IconPicker popup with searchable Lucide grid"
```

---

## Task 6: Adozione icona nel builder + verifica finale

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml`

- [ ] **Step 1: Builder XAML — sostituire la TextBox icona col picker**

In `HabitBuilderView.xaml`, nella zona testata, **sostituire** il blocco "Icon (optional)" (`TextBox Text="{Binding Icon...}"`) con:

```xml
<!-- Icon (optional) -->
<StackPanel Grid.Column="2">
    <TextBlock Text="{x:Static res:Strings.HabitBuilder_Icon}"
               FontSize="{StaticResource FontSize.Small}"
               Foreground="{DynamicResource Brush.TextMuted}"
               Margin="0,0,0,2"/>
    <v:IconPicker Icon="{Binding Icon, Mode=TwoWay}" HorizontalAlignment="Left"/>
</StackPanel>
```

(`xmlns:v` già aggiunto nel Task 3 Step 4.)

- [ ] **Step 2: Build src — 0/0**

Run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`.

- [ ] **Step 3: Intera suite WPF — PASS**

Run: `dotnet test tests/Recordum.WPF.Tests --nologo`
Expected: PASS (tutti). Annotare il conteggio.

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml
git commit -m "feat(wpf): adopt icon picker in habit builder"
```

- [ ] **Step 5: Verifica visiva (utente, skill `run`)**

Checklist:
1. Builder → "Costruisci da zero": il quadratino colore **si clicca** e apre il popup con palette + (dopo qualche creazione) recenti + hex.
2. Lanciando un'abitudine, il colore scelto ricompare in "Recenti" alla creazione successiva.
3. Builder: il box icona apre il popup con griglia Lucide + ricerca; selezionando un'icona, l'anteprima si aggiorna e l'abitudine creata mostra quell'icona in Home.
4. Configura (editor) di un'abitudine esistente: stesso color picker (con recenti) e stesso icon picker.
5. Temi chiaro/scuro: popup leggibili, swatch e icone con contrasto corretto.

- [ ] **Step 6: Aggiornare il tracker**

In `docs/phase-4-issues.md`, marcare `R-builderColorPicker` e `R-builderIconPicker` come `fixed` (verifica visiva utente: da fare/fatta), con riferimento ai commit. Commit:
```bash
git add docs/phase-4-issues.md
git commit -m "docs(phase-4): color/icon pickers implemented (shared builder + Configura)"
```

---

## Self-Review (esito)

- **Spec coverage**: §4 color picker → Task 2+3; §4.3 RecentColorStore → Task 1; §5 icon picker → Task 4 (catalogo+geometrie) + Task 5 (UI); §6 adozione → Task 3 (colore) + Task 6 (icona); §7 testing → Task 1 (store), Task 4 (catalogo+allineamento), Task 3 (VM). Tutte le sezioni coperte.
- **Placeholder scan**: i path geometrie Lucide sono volutamente sourcati via fetch (non inventati) — è un'istruzione operativa, non un placeholder; forniti formato, regole di conversione e 3 esempi-template. Nessun altro TBD.
- **Type consistency**: `IRecentColorStore.GetAsync/AddAsync`, DP `RecentColors`, `IconCatalog.All/Search`, converter `IconKeyToGeometry`/`IconKeyToGeometryConverter` coerenti tra i task. Ctor di `HabitBuilderViewModel`/`HabitEditorViewModel` aggiornati con la stessa firma usata nei test (Task 3 Step 1/8).
- **Rischio noto**: l'import delle 40 geometrie (Task 4 Step 4) è il punto più laborioso e fragile; il test di allineamento (Step 5) fa da rete. Se una key Lucide è 404, l'istruzione è sostituire+riallineare key e catalogo.
