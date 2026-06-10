# Habit Visual Identity (#5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Home habit rows read as tinted blocks in their own color, mark the selected habit with an inner colored border + stronger tint, and echo the habit color in the workspace column (top strip + active tab).

**Architecture:** A new value converter turns a habit hex + an alpha into a semi-transparent brush; the Home row templates wrap their content in a tinted `Border` styled off the row's `ColorHex`/`IsSelected`; a new `IsSelected` flag on the row VM is kept in sync by `HomeViewModel`; the workspace adds a color top strip and a keyed `TabItem` style whose selected underline binds to the habit color.

**Tech Stack:** WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-06-10-habit-visual-identity-design.md`

**Repo conventions (important):**
- Test runner: `dotnet test tests/Recordum.WPF.Tests --nologo`. Single test: `... --filter "FullyQualifiedName~<Name>"`.
- **The WPF app locks its binaries while running** — ensure it is closed before building (`Get-Process Recordum.WPF | Stop-Process -Force`).
- `dotnet build src/Recordum.WPF` must stay **0 warning / 0 error** (TreatWarningsAsErrors on src).
- Commit messages in English, footer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- A pre-existing flaky test (`HomeViewModelTests.CompletionRecorded_event_reloads_the_list_for_visual_feedback`) fails intermittently only in the full suite; confirm green by re-running or running the class in isolation.

---

## File Structure

- `src/Recordum.WPF/Converters/HabitTintBrushConverter.cs` — **new**: hex + alpha → semi-transparent brush.
- `src/Recordum.WPF/App.xaml` — **modify**: register the converter + define the shared `Recordum.HabitBlock` Border style.
- `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs` — **modify**: `IsSelected` observable.
- `src/Recordum.WPF/ViewModels/HomeViewModel.cs` — **modify**: sync `IsSelected` on selection change + on `Rebuild`.
- `src/Recordum.WPF/Views/HabitRowTemplate.xaml` — **modify**: wrap row in the tinted block.
- `src/Recordum.WPF/Views/GroupRowTemplate.xaml` — **modify**: wrap row in the tinted block.
- `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` — **modify**: top strip + keyed `HabitTab` style + apply to the 3 tabs.
- `tests/Recordum.WPF.Tests/Converters/HabitTintBrushConverterTests.cs` — **new**.
- `tests/Recordum.WPF.Tests/ViewModels/HomeViewModelTests.cs` — **modify**: add `IsSelected` sync test.

---

## Task 1: HabitTintBrushConverter

**Files:**
- Create: `src/Recordum.WPF/Converters/HabitTintBrushConverter.cs`
- Create: `tests/Recordum.WPF.Tests/Converters/HabitTintBrushConverterTests.cs`
- Modify: `src/Recordum.WPF/App.xaml` (register)

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Recordum.WPF.Tests/Converters/HabitTintBrushConverterTests.cs
using System.Globalization;
using System.Windows.Media;
using FluentAssertions;
using Recordum.WPF.Converters;
using Xunit;

namespace Recordum.WPF.Tests.Converters;

public class HabitTintBrushConverterTests
{
    private readonly HabitTintBrushConverter _sut = new();

    [Fact]
    public void Produces_brush_of_the_color_at_the_requested_alpha()
    {
        var brush = (SolidColorBrush)_sut.Convert("#3B82F6", typeof(Brush), "26", CultureInfo.InvariantCulture);
        brush.Color.A.Should().Be(26);
        brush.Color.R.Should().Be(0x3B);
        brush.Color.G.Should().Be(0x82);
        brush.Color.B.Should().Be(0xF6);
    }

    [Fact]
    public void Malformed_or_null_input_is_transparent()
    {
        _sut.Convert("nope", typeof(Brush), "26", CultureInfo.InvariantCulture).Should().Be(Brushes.Transparent);
        _sut.Convert(null, typeof(Brush), "26", CultureInfo.InvariantCulture).Should().Be(Brushes.Transparent);
    }

    [Fact]
    public void Missing_alpha_parameter_defaults_to_about_ten_percent()
    {
        var brush = (SolidColorBrush)_sut.Convert("#3B82F6", typeof(Brush), null, CultureInfo.InvariantCulture);
        brush.Color.A.Should().Be(0x1A);
    }
}
```

- [ ] **Step 2: Run the test — verify it FAILS (type does not exist)**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~HabitTintBrushConverterTests" --nologo`
Expected: compile error / FAIL.

- [ ] **Step 3: Implement the converter**

```csharp
// src/Recordum.WPF/Converters/HabitTintBrushConverter.cs
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

/// <summary>Converts a habit hex color (#RRGGBB) plus an alpha (ConverterParameter, decimal byte
/// 0-255) into a semi-transparent <see cref="SolidColorBrush"/> for the tinted habit blocks.
/// Returns <see cref="Brushes.Transparent"/> for malformed/empty input.</summary>
public sealed class HabitTintBrushConverter : IValueConverter
{
    private const byte DefaultAlpha = 0x1A; // ~10%

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(Color.FromArgb(ParseAlpha(parameter), c.R, c.G, c.B));
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    private static byte ParseAlpha(object? parameter) =>
        parameter is string s && byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
            ? a
            : DefaultAlpha;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 4: Run the test — verify it PASSES**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~HabitTintBrushConverterTests" --nologo`
Expected: PASS (3 tests).

- [ ] **Step 5: Register the converter in App.xaml**

In `src/Recordum.WPF/App.xaml`, after the line `<conv:IconKeyToGeometryConverter x:Key="IconKeyToGeometry"/>`, add:

```xml
            <conv:HabitTintBrushConverter x:Key="HabitTint"/>
```

- [ ] **Step 6: Build — verify 0/0**

Run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/Converters/HabitTintBrushConverter.cs src/Recordum.WPF/App.xaml tests/Recordum.WPF.Tests/Converters/HabitTintBrushConverterTests.cs
git commit -m "feat(wpf): HabitTintBrushConverter — habit color at alpha for tinted blocks"
```

---

## Task 2: Row IsSelected state + sync

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HomeViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HomeViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `HomeViewModelTests.cs` (the file already has `_query`, `CreateSut()`, and a `SampleRow(...)` helper):

```csharp
    [Fact]
    public async Task SelectedRow_drives_IsSelected_and_clears_the_previous_row()
    {
        _query.HomeRows.Add(SampleRow(name: "First"));
        _query.HomeRows.Add(SampleRow(name: "Second"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var first = sut.Rows.Single(r => r.Name == "First");
        var second = sut.Rows.Single(r => r.Name == "Second");

        sut.SelectedRow = first;
        first.IsSelected.Should().BeTrue();
        second.IsSelected.Should().BeFalse();

        sut.SelectedRow = second;
        first.IsSelected.Should().BeFalse();
        second.IsSelected.Should().BeTrue();

        sut.SelectedRow = null;
        second.IsSelected.Should().BeFalse();
    }
```

- [ ] **Step 2: Run the test — verify it FAILS (`IsSelected` does not exist)**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~SelectedRow_drives_IsSelected" --nologo`
Expected: compile error / FAIL.

- [ ] **Step 3: Add `IsSelected` to the row VM**

In `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs`, next to the other `[ObservableProperty]` fields (e.g. near `_pendingName`), add:

```csharp
    [ObservableProperty] private bool _isSelected;
```

- [ ] **Step 4: Sync it from `HomeViewModel`**

In `src/Recordum.WPF/ViewModels/HomeViewModel.cs`, add a change handler for the generated `SelectedRow` property. Place this method anywhere in the class body (e.g. just after the `SelectRow` method):

```csharp
    partial void OnSelectedRowChanged(HomeRowViewModel? oldValue, HomeRowViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
    }
```

Then make a fresh `Rebuild()` re-apply selection (rows are recreated, so the new instance must reflect the kept selection). In `Rebuild()`, after `Rows` has been populated and `SelectedRow` re-resolved, the `OnSelectedRowChanged` won't fire if the reference is reassigned to an equal-by-reference value — so set it explicitly. Locate the end of `Rebuild()` and add, as its last statement:

```csharp
        foreach (var r in Rows) r.IsSelected = ReferenceEquals(r, SelectedRow);
```

- [ ] **Step 5: Run the test — verify it PASSES**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~SelectedRow_drives_IsSelected" --nologo`
Expected: PASS.

- [ ] **Step 6: Run the HomeViewModel class — verify no regression**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~HomeViewModelTests" --nologo`
Expected: PASS (all).

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HomeRowViewModel.cs src/Recordum.WPF/ViewModels/HomeViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HomeViewModelTests.cs
git commit -m "feat(wpf): HomeRowViewModel.IsSelected synced from SelectedRow"
```

---

## Task 3: Tinted block in the Home row templates

**Files:**
- Modify: `src/Recordum.WPF/App.xaml` (shared block style)
- Modify: `src/Recordum.WPF/Views/HabitRowTemplate.xaml`
- Modify: `src/Recordum.WPF/Views/GroupRowTemplate.xaml`

> No unit test (XAML). Logic (IsSelected) is covered by Task 2. Visual verification in Task 5.

- [ ] **Step 1: Define the shared `Recordum.HabitBlock` style in App.xaml**

In `src/Recordum.WPF/App.xaml`, after the converter declarations (after the `HabitTint` line added in Task 1, and before `</ResourceDictionary>`'s other content / styles), add:

```xml
            <!-- Tinted habit-row block: resting = ~10% of the habit color, selected = ~20% + inner border. -->
            <Style x:Key="Recordum.HabitBlock" TargetType="Border">
                <Setter Property="CornerRadius" Value="6"/>
                <Setter Property="Margin" Value="0,1"/>
                <Setter Property="ClipToBounds" Value="True"/>
                <Setter Property="BorderThickness" Value="0"/>
                <Setter Property="Background" Value="{Binding ColorHex, Converter={StaticResource HabitTint}, ConverterParameter=26}"/>
                <Style.Triggers>
                    <DataTrigger Binding="{Binding IsSelected}" Value="True">
                        <Setter Property="Background" Value="{Binding ColorHex, Converter={StaticResource HabitTint}, ConverterParameter=51}"/>
                        <Setter Property="BorderBrush" Value="{Binding ColorHex, Converter={StaticResource HexToBrush}}"/>
                        <Setter Property="BorderThickness" Value="1.5"/>
                    </DataTrigger>
                </Style.Triggers>
            </Style>
```

(Place it after the `<conv:.../>` converter entries so the `HabitTint`/`HexToBrush` StaticResources it references are already declared.)

- [ ] **Step 2: Wrap the habit row in the block**

In `src/Recordum.WPF/Views/HabitRowTemplate.xaml`, wrap the existing root `<Grid Height="{StaticResource Layout.RowHeight}"> … </Grid>` in the styled Border. Change the opening so the DataTemplate's child is:

```xml
        <Border Style="{StaticResource Recordum.HabitBlock}">
            <Grid Height="{StaticResource Layout.RowHeight}">
```

and add the matching `</Border>` immediately after the Grid's closing `</Grid>` (the one that currently closes the template's root grid, just before `</DataTemplate>`). Leave everything inside the Grid (accent bar, icon, name, type, heatmap, quick-action, `Grid.ContextMenu`) unchanged.

- [ ] **Step 3: Wrap the group row in the block**

In `src/Recordum.WPF/Views/GroupRowTemplate.xaml`, identically wrap its root `<Grid Height="{StaticResource Layout.RowHeight}"> … </Grid>`:

```xml
        <Border Style="{StaticResource Recordum.HabitBlock}">
            <Grid Height="{StaticResource Layout.RowHeight}">
```

and add the matching `</Border>` after that Grid's closing `</Grid>`. (Groups never set `IsSelected` in normal use beyond the shared mechanism — the resting tint applies; if a group is selected the same treatment is harmless.)

- [ ] **Step 4: Build — verify 0/0**

Run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`. (XAML parse errors here usually mean a missing `</Border>` — check both templates balance.)

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/App.xaml src/Recordum.WPF/Views/HabitRowTemplate.xaml src/Recordum.WPF/Views/GroupRowTemplate.xaml
git commit -m "feat(wpf): tinted habit-color blocks in Home rows + selection border"
```

---

## Task 4: Workspace color accents

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`

> No unit test (XAML). Visual verification in Task 5.

- [ ] **Step 1: Add the keyed `Recordum.HabitTab` style to HabitScreen resources**

In `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`, inside the existing `<UserControl.Resources>` block (before `</UserControl.Resources>`), add a keyed TabItem style whose selected underline + label use the habit color carried in `Tag`:

```xml
        <Style x:Key="Recordum.HabitTab" TargetType="{x:Type TabItem}" BasedOn="{StaticResource {x:Type TabItem}}">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type TabItem}">
                        <Border x:Name="Bd"
                                Background="{TemplateBinding Background}"
                                BorderBrush="Transparent"
                                BorderThickness="0,0,0,2"
                                SnapsToDevicePixels="True">
                            <ContentPresenter ContentSource="Header"
                                              Margin="{TemplateBinding Padding}"
                                              HorizontalAlignment="Center"
                                              VerticalAlignment="Center"
                                              RecognizesAccessKey="True"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsSelected" Value="True">
                                <Setter TargetName="Bd" Property="BorderBrush"
                                        Value="{Binding Tag, RelativeSource={RelativeSource TemplatedParent}, Converter={StaticResource HexToBrush}}"/>
                                <Setter Property="Foreground"
                                        Value="{Binding Tag, RelativeSource={RelativeSource TemplatedParent}, Converter={StaticResource HexToBrush}}"/>
                            </Trigger>
                            <MultiTrigger>
                                <MultiTrigger.Conditions>
                                    <Condition Property="IsSelected" Value="False"/>
                                    <Condition Property="IsMouseOver" Value="True"/>
                                </MultiTrigger.Conditions>
                                <Setter Property="Foreground" Value="{DynamicResource Brush.Text}"/>
                            </MultiTrigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```

- [ ] **Step 2: Add the top strip row + element**

In the root layout `<Grid>` (the one at the start of the control body, currently with three `RowDefinition`s: Auto/Auto/`*`), add a fourth row at the top and shift the existing `Grid.Row` values down by one.

Change the `RowDefinitions` to:

```xml
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
```

Immediately after `</Grid.RowDefinitions>`, add the strip:

```xml
        <Border Grid.Row="0" Height="3"
                Background="{Binding Node.Color, Converter={StaticResource HexToBrush}}"/>
```

Then bump the three existing children:
- The header `<Border Grid.Row="0" Padding="12" …>` → `Grid.Row="1"`.
- The WebView2 banner `<Border Grid.Row="1" Padding="8" …>` → `Grid.Row="2"`.
- The `<TabControl Grid.Row="2" …>` → `Grid.Row="3"`.

- [ ] **Step 3: Apply the keyed style + Tag to the three tabs**

On each of the three `<TabItem …>` elements (Abitudine / Detail / Notes), add the style and the color-carrying Tag. For each, change the opening tag to include:

```xml
            <TabItem Style="{StaticResource Recordum.HabitTab}"
                     Tag="{Binding Node.Color}"
                     Header="{x:Static res:Strings.HabitScreen_Tab_Abitudine}">
```

(Repeat `Style="{StaticResource Recordum.HabitTab}" Tag="{Binding Node.Color}"` on the `HabitScreen_Tab_Detail` and `HabitScreen_Tab_Notes` TabItems — keep each one's existing `Header`.)

- [ ] **Step 4: Build — verify 0/0**

Run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml
git commit -m "feat(wpf): workspace color accents — top strip + active tab in habit color"
```

---

## Task 5: Verify + tracker

**Files:**
- Modify: `docs/phase-4-issues.md`

- [ ] **Step 1: Full WPF suite**

Run: `dotnet test tests/Recordum.WPF.Tests --nologo`
Expected: PASS. If the only failure is `HomeViewModelTests.CompletionRecorded_event_reloads_the_list_for_visual_feedback`, re-run the class in isolation (`--filter "FullyQualifiedName~HomeViewModelTests"`) to confirm it's the known flake.

- [ ] **Step 2: Visual verification (skill `run`)**

Launch the app. Confirm:
1. Home rows are tinted blocks in each habit's color, detached from the background (both Light and Dark).
2. The selected habit shows the inner colored border + stronger tint; selecting another moves it.
3. Group rows are tinted too.
4. The open workspace has a 3px top strip in the habit color and the active tab's underline/label in the habit color.
5. Changing the color in Configura updates rows, strip, and tab live (uses the existing `HabitUpdated` wiring).

- [ ] **Step 3: Update the tracker**

In `docs/phase-4-issues.md`, mark **#5 evidenziazione abitudini** as `fixed` with the commit range and a one-line summary (tinted blocks + selection border + workspace strip/active-tab color; spec `2026-06-10-habit-visual-identity-design.md`).

- [ ] **Step 4: Commit**

```bash
git add docs/phase-4-issues.md
git commit -m "docs(phase-4): #5 habit visual identity implemented"
```

---

## Self-Review (result)

- **Spec coverage:** §4.1 resting block → Task 3 (style + HabitRowTemplate); §4.2 selection → Task 2 (IsSelected) + Task 3 (trigger); §4.3 workspace accents → Task 4; §4.4 theming/converter → Task 1; §4.5 groups → Task 3 Step 3; §6 testing → Task 1 (converter), Task 2 (IsSelected), Task 5 (visual). All covered.
- **Placeholder scan:** no TBD/TODO; every code step shows full code; the row-template wraps quote the exact elements to change.
- **Type consistency:** converter key `HabitTint` and `HexToBrush` used identically across App.xaml style and HabitScreen tabs; `IsSelected` defined in Task 2 and consumed by the `Recordum.HabitBlock` trigger in Task 3; `Recordum.HabitBlock` / `Recordum.HabitTab` style keys consistent between definition and use.
- **Known risk:** the selected-tab foreground uses the habit color; very dark user-chosen colors could be low-contrast on dark theme — a one-line retune (foreground → `Brush.Text`) if visual verification flags it.
