# Larghezze colonna ridimensionabili — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Le 4 colonne del workspace HabitScreen si ridimensionano trascinando il confine verticale destro (Thumb), con doppio-click reset, clamp 320–1000 e persistenza per-colonna su AppSetting `ui.column_widths`.

**Architecture:** Stato larghezze in `HabitScreenViewModel` (4 proprietà double con clamp nel setter, manuali — non `[ObservableProperty]`, che non permette clamp), persistite in CSV `Cronologia=460,…` solo a fine drag. La view sovrappone un `Thumb` di 6px al bordo destro di ogni colonna; il code-behind traduce DragDelta/DragCompleted/DoubleClick in chiamate VM. Nessuna migrazione (chiave on-write).

**Tech Stack:** WPF + CommunityToolkit.Mvvm; xUnit/FluentAssertions (`HabitScreenViewModelFactory` esistente).

**Spec:** `docs/superpowers/specs/2026-06-11-column-resize-design.md`

---

## Mappa file

| Azione | Path | Responsabilità |
|---|---|---|
| Modify | `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` | proprietà larghezza + clamp + persistenza/restore |
| Modify | `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` | stile Thumb + binding Width + Thumb per colonna |
| Modify | `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml.cs` | handler DragDelta/DragCompleted/DoubleClick |
| Test | `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenColumnWidthsTests.cs` (new) | clamp/roundtrip/malformed/reset |

---

### Task 1: stato larghezze in `HabitScreenViewModel` (TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenColumnWidthsTests.cs` (new)

- [x] **Step 1: test (RED)** — nuovo file, stessa factory di `HabitScreenColumnsTests`:

```csharp
using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitScreenColumnWidthsTests
{
    [Fact]
    public void Widths_default_to_per_column_values()
    {
        var (sut, _) = HabitScreenViewModelFactory.CreateWithSettings(out _);

        sut.CronologiaWidth.Should().Be(460);
        sut.DettaglioWidth.Should().Be(520);
        sut.NoteWidth.Should().Be(460);
        sut.ConfiguraWidth.Should().Be(480);
    }

    [Fact]
    public void Width_setter_clamps_to_min_and_max()
    {
        var (sut, _) = HabitScreenViewModelFactory.CreateWithSettings(out _);

        sut.CronologiaWidth = 100;
        sut.CronologiaWidth.Should().Be(HabitScreenViewModel.MinColumnWidth);

        sut.CronologiaWidth = 5000;
        sut.CronologiaWidth.Should().Be(HabitScreenViewModel.MaxColumnWidth);
    }

    [Fact]
    public async Task Persist_writes_csv_and_roundtrips_on_restore()
    {
        var (sut, _) = HabitScreenViewModelFactory.CreateWithSettings(out var settings);
        sut.SetColumnWidth(HabitColumn.Note, 600);
        await sut.PersistColumnWidthsAsync();

        (await settings.GetAsync(HabitScreenViewModel.ColumnWidthsSettingKey))
            .Should().Be("Cronologia=460,Dettaglio=520,Note=600,Configura=480");

        var (sut2, _) = HabitScreenViewModelFactory.CreateOver(settings);
        await sut2.RestoreOpenColumnsAsync();
        sut2.NoteWidth.Should().Be(600);
        sut2.DettaglioWidth.Should().Be(520);
    }

    [Fact]
    public async Task Restore_ignores_malformed_or_out_of_range_tokens()
    {
        var (_, _) = HabitScreenViewModelFactory.CreateWithSettings(out var settings);
        await settings.SetAsync(HabitScreenViewModel.ColumnWidthsSettingKey,
            "Cronologia=abc,Bogus=400,Note=9999,Dettaglio=600,Configura");

        var (sut, _) = HabitScreenViewModelFactory.CreateOver(settings);
        await sut.RestoreOpenColumnsAsync();

        sut.CronologiaWidth.Should().Be(460);   // valore non numerico → default
        sut.NoteWidth.Should().Be(460);          // fuori range → default
        sut.DettaglioWidth.Should().Be(600);     // token valido applicato
        sut.ConfiguraWidth.Should().Be(480);     // token senza '=' → default
    }

    [Fact]
    public void Reset_returns_the_single_column_to_its_default()
    {
        var (sut, _) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        sut.SetColumnWidth(HabitColumn.Dettaglio, 700);
        sut.SetColumnWidth(HabitColumn.Note, 700);

        sut.ResetColumnWidth(HabitColumn.Dettaglio);

        sut.DettaglioWidth.Should().Be(520);
        sut.NoteWidth.Should().Be(700);  // le altre restano
    }
}
```

- [x] **Step 2: RED** — `dotnet test tests/Recordum.WPF.Tests --filter HabitScreenColumnWidths` (errori di compilazione attesi: proprietà/metodi mancanti).

- [x] **Step 3: implementare** in `HabitScreenViewModel`, nella regione "column workspace state (nav-model)" (dopo `CloseLastColumn`, riga ~107):

```csharp
    // ---- column widths (resizable via drag-thumb, spec 2026-06-11-column-resize) ----

    internal const string ColumnWidthsSettingKey = "ui.column_widths";
    internal const double MinColumnWidth = 320;
    internal const double MaxColumnWidth = 1000;

    private static double DefaultWidth(HabitColumn column) => column switch
    {
        HabitColumn.Cronologia => 460,
        HabitColumn.Dettaglio => 520,
        HabitColumn.Note => 460,
        _ => 480,
    };

    private static double ClampWidth(double value) => Math.Clamp(value, MinColumnWidth, MaxColumnWidth);

    // Manual properties (not [ObservableProperty]) so the setter can clamp.
    private double _cronologiaWidth = DefaultWidth(HabitColumn.Cronologia);
    public double CronologiaWidth { get => _cronologiaWidth; set => SetProperty(ref _cronologiaWidth, ClampWidth(value)); }

    private double _dettaglioWidth = DefaultWidth(HabitColumn.Dettaglio);
    public double DettaglioWidth { get => _dettaglioWidth; set => SetProperty(ref _dettaglioWidth, ClampWidth(value)); }

    private double _noteWidth = DefaultWidth(HabitColumn.Note);
    public double NoteWidth { get => _noteWidth; set => SetProperty(ref _noteWidth, ClampWidth(value)); }

    private double _configuraWidth = DefaultWidth(HabitColumn.Configura);
    public double ConfiguraWidth { get => _configuraWidth; set => SetProperty(ref _configuraWidth, ClampWidth(value)); }

    public double GetColumnWidth(HabitColumn column) => column switch
    {
        HabitColumn.Cronologia => CronologiaWidth,
        HabitColumn.Dettaglio => DettaglioWidth,
        HabitColumn.Note => NoteWidth,
        _ => ConfiguraWidth,
    };

    public void SetColumnWidth(HabitColumn column, double width)
    {
        switch (column)
        {
            case HabitColumn.Cronologia: CronologiaWidth = width; break;
            case HabitColumn.Dettaglio: DettaglioWidth = width; break;
            case HabitColumn.Note: NoteWidth = width; break;
            case HabitColumn.Configura: ConfiguraWidth = width; break;
        }
    }

    public void ResetColumnWidth(HabitColumn column) => SetColumnWidth(column, DefaultWidth(column));

    /// <summary>Called by the view at the end of a resize drag (not per-pixel).</summary>
    public Task PersistColumnWidthsAsync()
        => _appSettings.SetAsync(ColumnWidthsSettingKey,
            string.Join(",", Enum.GetValues<HabitColumn>()
                .Select(c => $"{c}={(int)Math.Round(GetColumnWidth(c))}"))).AsTask();

    private async Task RestoreColumnWidthsAsync(CancellationToken ct)
    {
        var raw = await _appSettings.GetAsync(ColumnWidthsSettingKey, ct).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(raw)) return;
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split('=', 2);
            if (parts.Length != 2) continue;
            if (!Enum.TryParse<HabitColumn>(parts[0], out var column)) continue;
            if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var width)) continue;
            if (width is < MinColumnWidth or > MaxColumnWidth) continue;   // fuori range → default (spec §4)
            SetColumnWidth(column, width);
        }
    }
```

e in testa a `RestoreOpenColumnsAsync` (prima della lettura di `OpenColumnsSettingKey`):

```csharp
        await RestoreColumnWidthsAsync(ct).ConfigureAwait(true);
```

NB: il restore delle larghezze deve avvenire anche quando `ui.open_columns` è vuoto — per questo va PRIMA dell'early-return esistente.

- [x] **Step 4: GREEN** — `dotnet test tests/Recordum.WPF.Tests --filter HabitScreenColumnWidths` → 5 PASS; poi filtro `HabitScreenColumns` (i 5 esistenti restano verdi).

- [x] **Step 5: commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenColumnWidthsTests.cs
git commit -m "feat(wpf): per-column width state with clamp + persistence"
```

---

### Task 2: Thumb di resize nella view

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml.cs`

- [x] **Step 1: stile Thumb** in `UserControl.Resources`, dopo `HabitScreen.ColumnClose` (riga ~542):

```xml
        <Style x:Key="HabitScreen.ColumnResizeThumb" TargetType="Thumb">
            <Setter Property="HorizontalAlignment" Value="Right"/>
            <Setter Property="Width" Value="6"/>
            <Setter Property="Cursor" Value="SizeWE"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Thumb">
                        <Border x:Name="bd" Background="Transparent"/>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="bd" Property="Background" Value="{DynamicResource Brush.Accent}"/>
                                <Setter TargetName="bd" Property="Opacity" Value="0.4"/>
                            </Trigger>
                            <Trigger Property="IsDragging" Value="True">
                                <Setter TargetName="bd" Property="Background" Value="{DynamicResource Brush.Accent}"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```

- [x] **Step 2: colonne** — per ognuna delle 4 colonne: `Width` fissa → binding, e il `DockPanel` figlio si avvolge in un `Grid` col Thumb come secondo figlio (sopra il contenuto, sul bordo destro). Cronologia (riga ~626):

```xml
                    <Border Width="{Binding CronologiaWidth}" Style="{StaticResource HabitScreen.Column}"
                            Visibility="{Binding IsCronologiaOpen, Converter={StaticResource BoolToVisible}}">
                        <Grid>
                            <DockPanel>
                                <!-- contenuto esistente INVARIATO -->
                            </DockPanel>
                            <Thumb Style="{StaticResource HabitScreen.ColumnResizeThumb}"
                                   Tag="{x:Static vm:HabitColumn.Cronologia}"
                                   DragDelta="OnColumnResizeDelta"
                                   DragCompleted="OnColumnResizeCompleted"
                                   MouseDoubleClick="OnColumnResizeReset"/>
                        </Grid>
                    </Border>
```

Stesso pattern per Dettaglio (`Width="{Binding DettaglioWidth}"`, `Tag="{x:Static vm:HabitColumn.Dettaglio}"`), Note (`NoteWidth`/`HabitColumn.Note`), Configura (`ConfiguraWidth`/`HabitColumn.Configura`).

- [x] **Step 3: code-behind** `HabitScreen.xaml.cs` — using nuovi (`System.Windows.Controls.Primitives`, `System.Windows.Input`, `Recordum.WPF.ViewModels.HabitScreen`) + handler:

```csharp
    private HabitScreenViewModel? Vm => DataContext as HabitScreenViewModel;

    private static HabitColumn ColumnOf(object sender) => (HabitColumn)((Thumb)sender).Tag;

    private void OnColumnResizeDelta(object sender, DragDeltaEventArgs e)
    {
        if (Vm is not { } vm) return;
        var column = ColumnOf(sender);
        vm.SetColumnWidth(column, vm.GetColumnWidth(column) + e.HorizontalChange);
    }

    private void OnColumnResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        if (Vm is { } vm) _ = vm.PersistColumnWidthsAsync();
    }

    private void OnColumnResizeReset(object sender, MouseButtonEventArgs e)
    {
        if (Vm is not { } vm) return;
        vm.ResetColumnWidth(ColumnOf(sender));
        _ = vm.PersistColumnWidthsAsync();
        e.Handled = true;
    }
```

- [x] **Step 4: build + suite** — `dotnet build src/Recordum.WPF -v q` (0 errori, nessun warning nuovo) + suite WPF completa verde (flaky noto `CompletionRecorded_event_reloads…` ammesso se passa isolato).

- [x] **Step 5: commit**

```bash
git add src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml.cs
git commit -m "feat(wpf): drag-resizable workspace columns with double-click reset"
```

---

### Task 3: verifica visiva + tracker

- [x] skill `run` + checklist (utente: ok 2026-06-11): drag su ogni confine (cursore ↔, hover accent); clamp visibile a 320/1000; doppio-click reset; larghezze persistite tra riavvii; colonne chiuse/riaperte mantengono la larghezza; scroll orizzontale coerente; due temi.
- [x] aggiornare `docs/phase-4-issues.md` (resize colonne fatto, chiude nav-model §8 larghezze) + memoria; commit docs.

---

## Self-review

- Spec §3 (drag/clamp/doppio-click/persist a fine drag/hover) → T1 (clamp, persist) + T2 (Thumb, handler, hover, reset); §4 VM/view/code-behind → T1/T2; §6 testing → T1 test 5 casi + T3 verifica visiva. Coverage ok.
- Persist scrive interi (`(int)Math.Round`) → il roundtrip nel test usa 600 esatto, ok; drag frazionario si arrotonda solo alla persistenza, la proprietà resta double.
- `RestoreColumnWidthsAsync` non innesca persist (nessun guard necessario: persist parte solo da DragCompleted/reset).
- Tipi coerenti: `SetColumnWidth/GetColumnWidth/ResetColumnWidth/PersistColumnWidthsAsync`, chiave `ui.column_widths`, costanti `Min/MaxColumnWidth` internal (usate dai test).
- `Thumb` in XAML non richiede xmlns nuovo (è in `System.Windows.Controls.Primitives`, mappato nello schema WPF di default).
