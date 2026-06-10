# Habit Screen Color Identity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every interactive element inside `HabitScreen` (button hover/focus borders, text-selection, calendar today/selected) adopt the habit's own color, live, by overriding the theme `Brush.Accent` locally with the habit color.

**Architecture:** A `BindingProxy` (Freezable) carries `Node.Color` into `HabitScreen`'s `UserControl.Resources`, where a `SolidColorBrush x:Key="Brush.Accent"` binds its `Color` to that hex via a new `HexToColorConverter`. Because all interactive styles reference `Brush.Accent` as `DynamicResource`, descendants of `HabitScreen` re-resolve the accent to the habit color automatically; elements elsewhere keep the theme accent. The only filled accent state in scope (calendar selected day) gets a contrast-aware foreground.

**Tech Stack:** WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-06-10-habit-screen-color-identity-design.md`

**Repo conventions (important):**
- **The WPF app locks its binaries while running** — close it before building: `Get-Process Recordum.WPF -ErrorAction SilentlyContinue | Stop-Process -Force` (PowerShell).
- `dotnet build src/Recordum.WPF` must stay **0 warning / 0 error** (TreatWarningsAsErrors on src).
- Tests: `dotnet test tests/Recordum.WPF.Tests --nologo`. Single: `... --filter "FullyQualifiedName~<Name>"`.
- A pre-existing flaky test (`HomeViewModelTests.CompletionRecorded_event_reloads_the_list_for_visual_feedback`) fails intermittently only in the full suite; confirm green by re-running or running the class in isolation.
- Commit messages in English, footer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

## File Structure

- `src/Recordum.WPF/Converters/HexToColorConverter.cs` — **new**: hex `#RRGGBB` → `Color`, fallback to `#3B82F6`.
- `src/Recordum.WPF/Helpers/BindingProxy.cs` — **new**: `Freezable` carrying a bindable `Data`.
- `src/Recordum.WPF/App.xaml` — **modify**: register `HexToColorConverter` as `HexToColor`.
- `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` — **modify**: `xmlns:helpers`, the `BindingProxy`, and the `Brush.Accent` override.
- `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml` — **modify**: contrast-aware selected-day foreground.
- `tests/Recordum.WPF.Tests/Converters/HexToColorConverterTests.cs` — **new**.

---

## Task 1: HexToColorConverter

**Files:**
- Create: `src/Recordum.WPF/Converters/HexToColorConverter.cs`
- Create: `tests/Recordum.WPF.Tests/Converters/HexToColorConverterTests.cs`
- Modify: `src/Recordum.WPF/App.xaml`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Recordum.WPF.Tests/Converters/HexToColorConverterTests.cs
using System.Globalization;
using System.Windows.Media;
using FluentAssertions;
using Recordum.WPF.Converters;
using Xunit;

namespace Recordum.WPF.Tests.Converters;

public class HexToColorConverterTests
{
    private readonly HexToColorConverter _sut = new();

    [Fact]
    public void Parses_valid_hex_to_color()
    {
        var c = (Color)_sut.Convert("#3B82F6", typeof(Color), null, CultureInfo.InvariantCulture);
        c.R.Should().Be(0x3B);
        c.G.Should().Be(0x82);
        c.B.Should().Be(0xF6);
    }

    [Fact]
    public void Malformed_or_null_falls_back_to_default_accent()
    {
        var fallback = (Color)ColorConverter.ConvertFromString("#3B82F6");
        ((Color)_sut.Convert("nope", typeof(Color), null, CultureInfo.InvariantCulture)).Should().Be(fallback);
        ((Color)_sut.Convert(null, typeof(Color), null, CultureInfo.InvariantCulture)).Should().Be(fallback);
    }
}
```

- [ ] **Step 2: Run the test — verify it FAILS (type does not exist)**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~HexToColorConverterTests" --nologo`
Expected: compile error / FAIL.

- [ ] **Step 3: Implement the converter**

```csharp
// src/Recordum.WPF/Converters/HexToColorConverter.cs
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

/// <summary>Converts a habit hex color (#RRGGBB) to a <see cref="Color"/> for binding a
/// SolidColorBrush.Color. Malformed/empty input falls back to the default accent (#3B82F6)
/// so the resulting brush is never null.</summary>
public sealed class HexToColorConverter : IValueConverter
{
    private static readonly Color Fallback = (Color)ColorConverter.ConvertFromString("#3B82F6");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch (FormatException) { return Fallback; }
        }
        return Fallback;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 4: Run the test — verify it PASSES**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~HexToColorConverterTests" --nologo`
Expected: PASS (2 tests).

- [ ] **Step 5: Register the converter in App.xaml**

In `src/Recordum.WPF/App.xaml`, after the line `<conv:HabitTintBrushConverter x:Key="HabitTint"/>`, add:

```xml
            <conv:HexToColorConverter x:Key="HexToColor"/>
```

- [ ] **Step 6: Build — verify 0/0**

Close the app first, then run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/Converters/HexToColorConverter.cs src/Recordum.WPF/App.xaml tests/Recordum.WPF.Tests/Converters/HexToColorConverterTests.cs
git commit -m "feat(wpf): HexToColorConverter — habit hex to Color for the accent override"
```

---

## Task 2: BindingProxy

**Files:**
- Create: `src/Recordum.WPF/Helpers/BindingProxy.cs`

> No unit test: this is standard `Freezable`/`DependencyProperty` boilerplate with no logic. Verified by the build and by Task 3's visual result.

- [ ] **Step 1: Create the BindingProxy**

```csharp
// src/Recordum.WPF/Helpers/BindingProxy.cs
using System.Windows;

namespace Recordum.WPF.Helpers;

/// <summary>A <see cref="Freezable"/> that carries an inherited DataContext into places that
/// have none (e.g. resource dictionaries), so a binding can reach the host element's DataContext.
/// Standard WPF pattern; used to bind the habit color into HabitScreen's overridden Brush.Accent.</summary>
public sealed class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new PropertyMetadata(null));
}
```

- [ ] **Step 2: Build — verify 0/0**

Close the app first, then run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/Recordum.WPF/Helpers/BindingProxy.cs
git commit -m "feat(wpf): BindingProxy — carry DataContext into resource dictionaries"
```

---

## Task 3: Override Brush.Accent in HabitScreen with the habit color

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`

> No unit test (XAML). Visual verification in Task 5.

- [ ] **Step 1: Add the `helpers` namespace to the UserControl root**

In `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`, the root `<UserControl …>` already declares several `xmlns:` lines (e.g. `xmlns:res="clr-namespace:Recordum.WPF.Resources"`). Add, alongside them:

```xml
             xmlns:helpers="clr-namespace:Recordum.WPF.Helpers"
```

- [ ] **Step 2: Add the proxy + the Brush.Accent override at the top of UserControl.Resources**

The resources block opens with `<UserControl.Resources>` immediately followed by `<DataTemplate DataType="{x:Type vm:TimerControlViewModel}">`. Insert the two resources **right after** the `<UserControl.Resources>` opening tag (before that first DataTemplate). The `BindingProxy` must be declared before the brush that references it (StaticResource needs prior declaration):

```xml
    <UserControl.Resources>

        <!-- Habit color identity: every Brush.Accent consumer inside this screen (button hover/focus
             borders, text selection, calendar today/selected) re-resolves to the habit color. The
             proxy bridges Node.Color into the resource scope (resources have no DataContext). -->
        <helpers:BindingProxy x:Key="HabitColorProxy" Data="{Binding Node.Color}"/>
        <SolidColorBrush x:Key="Brush.Accent"
                         Color="{Binding Data, Source={StaticResource HabitColorProxy},
                                 Converter={StaticResource HexToColor}}"/>
```

(Leave the rest of the resources — the DataTemplates, `QuickAddSelector`, and the `Recordum.HabitTab` style — unchanged.)

- [ ] **Step 3: Build — verify 0/0**

Close the app first, then run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`. (A missing `helpers` xmlns or an out-of-order StaticResource shows up here as a XAML error.)

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml
git commit -m "feat(wpf): habit-colored accent inside HabitScreen via local Brush.Accent override"
```

---

## Task 4: Contrast-aware selected day in the calendar

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml`

> No unit test (XAML). Visual verification in Task 5.

- [ ] **Step 1: Make the selected-day foreground contrast-aware**

In `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml`, find the selected-day DataTrigger in the day-cell Button style:

```xml
                                        <!-- Selected: filled accent background -->
                                        <DataTrigger Binding="{Binding IsSelected}" Value="True">
                                            <Setter Property="Background" Value="{DynamicResource Brush.Accent}"/>
                                            <Setter Property="Foreground" Value="{DynamicResource Brush.OnAccent}"/>
                                        </DataTrigger>
```

Replace the `Foreground` setter so the day number stays legible on light habit colors (the background is now the habit color via the override; `TintColorHex` is that same habit color, set by `HabitScreenViewModel` → `Calendar.CompletedColorHex` → each cell). The result:

```xml
                                        <!-- Selected: filled accent background (= habit color via the
                                             HabitScreen Brush.Accent override), with contrast-aware text. -->
                                        <DataTrigger Binding="{Binding IsSelected}" Value="True">
                                            <Setter Property="Background" Value="{DynamicResource Brush.Accent}"/>
                                            <Setter Property="Foreground" Value="{Binding TintColorHex, Converter={StaticResource ContrastForeground}}"/>
                                        </DataTrigger>
```

- [ ] **Step 2: Build — verify 0/0**

Close the app first, then run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml
git commit -m "fix(wpf): contrast-aware selected-day text in calendar (habit-colored accent)"
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
1. Open a habit; hovering buttons (Export, Configura, range buttons, Save/Mark, the ✕ series delete) highlights the border in the **habit color** (try several habits with different colors; Light and Dark).
2. Keyboard-focus (Tab) gives a focus border in the habit color.
3. Calendar: today's border and the selected day's background are the habit color; the selected day's number stays **legible** (test a light color like yellow and a dark one).
4. Changing the color in Configura updates all of the above live.
5. Home rows, the Charts workspace, and the Configura editor pane are **unaffected** (neutral accent).

- [ ] **Step 3: Update the tracker**

In `docs/phase-4-issues.md`, near the #5 visual-identity entry, add a line recording that the habit-color identity now extends inside the workspace (hover/focus + calendar accents via a local `Brush.Accent` override; contrast-safe selected day), with the commit range and the spec path `2026-06-10-habit-screen-color-identity-design.md`.

- [ ] **Step 4: Commit**

```bash
git add docs/phase-4-issues.md
git commit -m "docs(phase-4): habit-color identity extended inside the workspace"
```

---

## Self-Review (result)

- **Spec coverage:** local `Brush.Accent` override → Task 3; `BindingProxy` → Task 2; `HexToColorConverter` (+ register) → Task 1; contrast-aware selected day → Task 4; testing (HexToColor unit + visual) → Tasks 1 & 5. All spec sections covered. Scope (HabitScreen only, calendar adopted now / redesigned in #3) is honored — no EditorPane or Home changes.
- **Placeholder scan:** no TBD/TODO; every code step shows the full code; commands have expected output.
- **Type consistency:** converter key `HexToColor` registered in Task 1 Step 5 and consumed in Task 3 Step 2; `BindingProxy` (namespace `Recordum.WPF.Helpers`, member `Data`) defined in Task 2 and referenced via `xmlns:helpers` + `Data="{Binding Node.Color}"` in Task 3; `ContrastForeground` is an existing registered converter (already used elsewhere in `CalendarNavigator.xaml`); `TintColorHex` is an existing `CalendarDayCellViewModel` property.
- **Known risk:** the `BindingProxy` DataContext inheritance is the standard WPF pattern; if for any reason `Node.Color` does not flow (proxy `Data` stays null), the override falls back to `#3B82F6` (a sane blue) rather than breaking — visible in Task 5 Step 1 (accents would look default-blue, not habit-colored) and would prompt a binding-path check.
```