# Color Picker v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Overhaul `ColorSwatchPicker` with floating-panel polish plus four features — selection ring, auto shade ramp, savable favorites, and a (VM-only) screen eyedropper.

**Architecture:** All UI lives in the existing `ColorSwatchPicker` UserControl (used by EditorPane, HabitBuilderView, NotesSectionView — they inherit the changes). New pure/logic pieces (`ShadeRamp`, `ColorEqualsConverter`, `FavoriteColorStore`, `ScreenColorPicker`) sit in `src/Recordum.WPF/Services` + `Converters`, mirroring the existing `RecentColorStore`. Host VMs feed a `FavoriteColors` collection and provide add/remove/eyedropper commands, exactly as they already feed `RecentColors`.

**Tech Stack:** C# / .NET 10, WPF, CommunityToolkit.Mvvm, xUnit + FluentAssertions. Spec: `docs/superpowers/specs/2026-06-23-color-picker-v2-design.md`.

## Global Constraints

- **Strings**: user-facing strings in `.resx` (English-first, Italian secondary) — `Recordum.WPF/Resources/Strings.resx` + `Strings.it.resx`. No hardcoded UI text.
- **Build/verify on macOS**: WPF compiles only — `dotnet build src/Recordum.WPF/Recordum.WPF.csproj -p:EnableWindowsTargeting=true`. WPF tests (`Recordum.WPF.Tests`, `net10.0-windows`) **compile but do not run** on macOS → write tests, compile-verify, run on the VM.
- **Colors**: `#RRGGBB` strings, normalized lowercase. Validate with regex `^#[0-9a-fA-F]{6}$`.
- **No DB migration** — only runtime AppSettings (not seeded), like `ui.recent_colors`.
- **IClock / no `DateTime.Now`** — N/A here (no timestamps).
- **Eyedropper is Win32 / Windows-only and unverifiable on macOS** — isolated behind `IScreenColorPicker`, implemented last; if unavailable the button hides.

---

## Phase 1 — Logic foundations (testable; compile-only on macOS, run on VM)

### Task 1: `ShadeRamp.For`

**Files:**
- Create: `src/Recordum.WPF/Services/ShadeRamp.cs`
- Test: `tests/Recordum.WPF.Tests/Services/ShadeRampTests.cs`

**Interfaces:**
- Consumes: `HsvColor.FromHex(string?) -> (double H,double S,double V)?`, `HsvColor.ToHex(double,double,double) -> string` (existing, `src/Recordum.WPF/Services/HsvColor.cs`).
- Produces: `static IReadOnlyList<string> ShadeRamp.For(string hex, int steps = 7)` — `steps` `#rrggbb` colors, lightest→darkest, the base color at the centre; empty list for invalid hex.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Recordum.WPF.Services;
using Xunit;

namespace Recordum.WPF.Tests.Services;

public class ShadeRampTests
{
    [Fact]
    public void For_returns_seven_valid_hex_colors_light_to_dark()
    {
        var ramp = ShadeRamp.For("#3b82f6");

        ramp.Should().HaveCount(7);
        ramp.Should().OnlyContain(c => System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-f]{6}$"));

        // Value (brightness) strictly decreases lightest -> darkest.
        var vs = ramp.Select(c => HsvColor.FromHex(c)!.Value.V).ToList();
        for (int i = 1; i < vs.Count; i++) vs[i].Should().BeLessThan(vs[i - 1]);
    }

    [Fact]
    public void For_centres_on_the_base_hue()
    {
        var ramp = ShadeRamp.For("#3b82f6");
        var baseH = HsvColor.FromHex("#3b82f6")!.Value.H;
        var midH = HsvColor.FromHex(ramp[3])!.Value.H;
        midH.Should().BeApproximately(baseH, 1.0);
    }

    [Fact]
    public void For_returns_empty_on_invalid_hex()
    {
        ShadeRamp.For("nope").Should().BeEmpty();
        ShadeRamp.For(null!).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet build tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj -p:EnableWindowsTargeting=true`
Expected: FAIL — `ShadeRamp` does not exist (CS0103). (Tests can't execute on macOS; compile failure is the red signal.)

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace Recordum.WPF.Services;

/// <summary>Generates a light→dark tonal ramp around a base colour (HSV: vary V, ease S),
/// with the base colour at the centre. Pure; used by the colour picker's "Tonalità" row.</summary>
public static class ShadeRamp
{
    public static IReadOnlyList<string> For(string hex, int steps = 7)
    {
        if (HsvColor.FromHex(hex) is not { } hsv) return [];
        var (h, s, v) = hsv;
        var mid = steps / 2;
        var result = new string[steps];
        for (var i = 0; i < steps; i++)
        {
            double ns, nv;
            if (i == mid) { ns = s; nv = v; }
            else if (i < mid)            // lighter: raise V toward 1, ease S down
            {
                var k = (mid - i) / (double)mid;
                nv = v + (1.0 - v) * k * 0.9;
                ns = s * (1 - 0.6 * k);
            }
            else                         // darker: lower V, nudge S up
            {
                var k = (i - mid) / (double)(steps - 1 - mid);
                nv = v * (1 - 0.65 * k);
                ns = s + (1 - s) * 0.2 * k;
            }
            result[i] = HsvColor.ToHex(h, System.Math.Clamp(ns, 0, 1), System.Math.Clamp(nv, 0, 1));
        }
        return result;
    }
}
```

- [ ] **Step 4: Compile to verify it builds**

Run: `dotnet build tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj -p:EnableWindowsTargeting=true`
Expected: `Build succeeded`. (Mark the test as VM-run in the commit message.)

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Services/ShadeRamp.cs tests/Recordum.WPF.Tests/Services/ShadeRampTests.cs
git commit -m "feat(wpf): ShadeRamp.For — tonal ramp for the colour picker"
```

---

### Task 2: `ColorEqualsConverter`

**Files:**
- Create: `src/Recordum.WPF/Converters/ColorEqualsConverter.cs`
- Test: `tests/Recordum.WPF.Tests/Converters/ColorEqualsConverterTests.cs`

**Interfaces:**
- Produces: `ColorEqualsConverter : IMultiValueConverter` — `Convert([swatchHex, currentHex], …) -> bool` (case-insensitive `#RRGGBB` equality; false if either null/invalid). Used by the selection-ring trigger.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Globalization;
using FluentAssertions;
using Recordum.WPF.Converters;
using Xunit;

namespace Recordum.WPF.Tests.Converters;

public class ColorEqualsConverterTests
{
    private static readonly ColorEqualsConverter Sut = new();
    private static object? Conv(object? a, object? b) =>
        Sut.Convert(new[] { a, b }, typeof(bool), null, CultureInfo.InvariantCulture);

    [Fact] public void Equal_ignoring_case() => Conv("#3B82F6", "#3b82f6").Should().Be(true);
    [Fact] public void Not_equal() => Conv("#3b82f6", "#10b981").Should().Be(false);
    [Fact] public void Null_is_false() => Conv(null, "#3b82f6").Should().Be(false);
}
```

- [ ] **Step 2: Compile to verify it fails**

Run: `dotnet build tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj -p:EnableWindowsTargeting=true`
Expected: FAIL — `ColorEqualsConverter` not found.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>True when two #RRGGBB strings name the same colour (case-insensitive). Drives the
/// selection ring on palette / recent / favourite swatches.</summary>
public sealed class ColorEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [string a, string b]) return false;
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 4: Compile** — `dotnet build tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj -p:EnableWindowsTargeting=true` → `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Converters/ColorEqualsConverter.cs tests/Recordum.WPF.Tests/Converters/ColorEqualsConverterTests.cs
git commit -m "feat(wpf): ColorEqualsConverter for selection-ring matching"
```

---

### Task 3: `IFavoriteColorStore` + `FavoriteColorStore` + DI

**Files:**
- Create: `src/Recordum.WPF/Services/IFavoriteColorStore.cs`, `src/Recordum.WPF/Services/FavoriteColorStore.cs`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` (register next to `IRecentColorStore`)
- Test: `tests/Recordum.WPF.Tests/Services/FavoriteColorStoreTests.cs`

**Interfaces:**
- Consumes: `IAppSettingService` (`GetAsync(key)`, `SetAsync(key,value)`), test `FakeAppSettingService`.
- Produces: `IFavoriteColorStore { ValueTask<IReadOnlyList<string>> GetAsync(ct); ValueTask AddAsync(string hex, ct); ValueTask RemoveAsync(string hex, ct); }` — CSV AppSetting `ui.favorite_colors`, order preserved (append on add), cap 12, no dups, invalid ignored.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Xunit;

namespace Recordum.WPF.Tests.Services;

public class FavoriteColorStoreTests
{
    private static FavoriteColorStore NewSut() => new(new FakeAppSettingService());

    [Fact]
    public async Task Add_appends_and_dedups()
    {
        var sut = NewSut();
        await sut.AddAsync("#ff0000");
        await sut.AddAsync("#00FF00");
        await sut.AddAsync("#ff0000");           // dup → no-op, order kept
        (await sut.GetAsync()).Should().Equal("#ff0000", "#00ff00");
    }

    [Fact]
    public async Task Remove_drops_the_color()
    {
        var sut = NewSut();
        await sut.AddAsync("#ff0000");
        await sut.AddAsync("#00ff00");
        await sut.RemoveAsync("#FF0000");
        (await sut.GetAsync()).Should().Equal("#00ff00");
    }

    [Fact]
    public async Task Caps_at_twelve()
    {
        var sut = NewSut();
        for (int i = 0; i < 15; i++) await sut.AddAsync($"#0000{i:x2}");
        (await sut.GetAsync()).Should().HaveCount(12);
    }

    [Fact]
    public async Task Invalid_hex_ignored()
    {
        var sut = NewSut();
        await sut.AddAsync("nope");
        (await sut.GetAsync()).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Compile to verify it fails** — `dotnet build tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj -p:EnableWindowsTargeting=true` → FAIL (`FavoriteColorStore` not found).

- [ ] **Step 3: Write the interface + implementation**

`IFavoriteColorStore.cs`:
```csharp
namespace Recordum.WPF.Services;

/// <summary>User-pinned favourite habit colours (UI state, persisted in AppSetting). Insertion order.</summary>
public interface IFavoriteColorStore
{
    ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default);
    ValueTask AddAsync(string hex, CancellationToken ct = default);
    ValueTask RemoveAsync(string hex, CancellationToken ct = default);
}
```

`FavoriteColorStore.cs` (mirrors `RecentColorStore`, but append-on-add + RemoveAsync, cap 12):
```csharp
using System.Text.RegularExpressions;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.Services;

/// <summary>Up to 12 pinned #RRGGBB favourites as a CSV AppSetting, insertion order, de-duplicated.</summary>
public sealed partial class FavoriteColorStore : IFavoriteColorStore
{
    internal const string SettingKey = "ui.favorite_colors";
    internal const int Cap = 12;

    private readonly IAppSettingService _settings;
    public FavoriteColorStore(IAppSettingService settings) => _settings = settings;

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexPattern();

    public async ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default)
    {
        var raw = await _settings.GetAsync(SettingKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Where(c => HexPattern().IsMatch(c))
                  .Select(c => c.ToLowerInvariant())
                  .ToArray();
    }

    public async ValueTask AddAsync(string hex, CancellationToken ct = default)
    {
        if (hex is null || !HexPattern().IsMatch(hex)) return;
        var norm = hex.ToLowerInvariant();
        var list = new List<string>(await GetAsync(ct).ConfigureAwait(false));
        if (list.Contains(norm)) return;             // keep position; pinning is order-stable
        list.Add(norm);
        if (list.Count > Cap) list.RemoveRange(Cap, list.Count - Cap);
        await _settings.SetAsync(SettingKey, string.Join(',', list), ct).ConfigureAwait(false);
    }

    public async ValueTask RemoveAsync(string hex, CancellationToken ct = default)
    {
        if (hex is null) return;
        var norm = hex.ToLowerInvariant();
        var list = new List<string>(await GetAsync(ct).ConfigureAwait(false));
        if (list.RemoveAll(c => string.Equals(c, norm, StringComparison.Ordinal)) == 0) return;
        await _settings.SetAsync(SettingKey, string.Join(',', list), ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Register in DI**

In `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`, find the line registering `IRecentColorStore` and add directly below it:
```csharp
services.AddSingleton<IFavoriteColorStore, FavoriteColorStore>();
```
(Match the lifetime used for `IRecentColorStore` — if it's `AddSingleton`/`AddScoped`, mirror it.)

- [ ] **Step 5: Compile** — `dotnet build src/Recordum.WPF/Recordum.WPF.csproj -p:EnableWindowsTargeting=true` then the test project → `Build succeeded`.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/Services/IFavoriteColorStore.cs src/Recordum.WPF/Services/FavoriteColorStore.cs src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs tests/Recordum.WPF.Tests/Services/FavoriteColorStoreTests.cs
git commit -m "feat(wpf): FavoriteColorStore (ui.favorite_colors, add/remove/cap 12)"
```

---

## Phase 2 — Picker restyle + selection ring + shade ramp

### Task 4: Floating-panel popup + rounded HSV + hex "#"

**Files:**
- Modify: `src/Recordum.WPF/Views/ColorSwatchPicker.xaml` (popup chrome + hex row)
- Modify: `src/Recordum.WPF/Views/HsvColorPicker.xaml` (SV/hue corner radius)

**Interfaces:** none new.

- [ ] **Step 1: Restyle the popup chrome.** In `ColorSwatchPicker.xaml`, replace the `<Border x:Name="PopupRoot" …>` opening so the panel matches the menu/dropdown family (radius 8, soft shadow, transparent bleed margin). Wrap its content with an outer transparent border:

```xml
<Popup x:Name="PickerPopup" PlacementTarget="{Binding ElementName=Trigger}"
       Placement="Bottom" StaysOpen="False" AllowsTransparency="True"
       PopupAnimation="Fade" Opened="OnPopupOpened">
    <Border Margin="8" Background="Transparent">
        <Border x:Name="PopupRoot" Focusable="True"
                Background="{DynamicResource Brush.SurfaceElevated}"
                BorderBrush="{DynamicResource Brush.Border}" BorderThickness="1"
                CornerRadius="8" Padding="12"
                KeyDown="OnPopupKeyDown">
            <Border.Effect>
                <DropShadowEffect Color="#000000" Opacity="0.20" BlurRadius="18" ShadowDepth="3" Direction="270"/>
            </Border.Effect>
            <StackPanel Width="232">
                <!-- existing content unchanged for now -->
```
Keep the existing inner content (HSV, palette, recents, hex) as-is in this task; close the extra `</Border>` before `</Popup>`.

- [ ] **Step 2: Rounded SV + hue.** In `HsvColorPicker.xaml`, change `SvArea` `CornerRadius="3"` → `CornerRadius="6"` and `HueArea` `CornerRadius="3"` → `CornerRadius="6"`.

- [ ] **Step 3: Hex with a fixed "#".** In `ColorSwatchPicker.xaml`, replace the hex `TextBox` so the "#" is a fixed prefix and the box holds the 6 digits. Replace the hex `<StackPanel Orientation="Horizontal" Margin="0,10,0,0"> … </StackPanel>` block with:

```xml
<StackPanel Orientation="Horizontal" Margin="0,10,0,0">
    <Border BorderBrush="{DynamicResource Brush.Border}" BorderThickness="0,0,0,1" Padding="2,2">
        <StackPanel Orientation="Horizontal">
            <TextBlock Text="#" Foreground="{DynamicResource Brush.TextMuted}" VerticalAlignment="Center"
                       FontFamily="Consolas"/>
            <TextBox x:Name="HexBox" Width="84" BorderThickness="0" FontFamily="Consolas"
                     VerticalContentAlignment="Center" MaxLength="6"
                     Text="{Binding Color, Converter={StaticResource HexDigitsConverter}, UpdateSourceTrigger=LostFocus}"/>
        </StackPanel>
    </Border>
    <Border Width="24" Height="24" Margin="8,0,0,0" CornerRadius="5"
            BorderBrush="{DynamicResource Brush.Border}" BorderThickness="1"
            Background="{Binding Color, Converter={StaticResource HexToBrush}}"/>
</StackPanel>
```
This needs a small `HexDigitsConverter` (strips/adds the leading `#`). Create `src/Recordum.WPF/Converters/HexDigitsConverter.cs`:
```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>"#3b82f6" ⇄ "3b82f6" for the colour picker's hex field (the "#" is a fixed label).</summary>
public sealed class HexDigitsConverter : IValueConverter
{
    public object Convert(object value, Type t, object? p, CultureInfo c)
        => value is string s && s.StartsWith('#') ? s[1..] : value ?? string.Empty;

    public object ConvertBack(object value, Type t, object? p, CultureInfo c)
    {
        var s = (value as string ?? string.Empty).Trim().TrimStart('#');
        return "#" + s;
    }
}
```
Register both converters (this + `ColorEqualsConverter`) wherever the picker's converters are declared. If they are app-level, add to `App.xaml` resource dictionary; if local, add to `ColorSwatchPicker.xaml` `<UserControl.Resources>`:
```xml
<conv:HexDigitsConverter x:Key="HexDigitsConverter"/>
<conv:ColorEqualsConverter x:Key="ColorEqualsConverter"/>
```
(add `xmlns:conv="clr-namespace:Recordum.WPF.Converters"` if missing.)

- [ ] **Step 4: Compile** — `dotnet build src/Recordum.WPF/Recordum.WPF.csproj -p:EnableWindowsTargeting=true` → `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Views/ColorSwatchPicker.xaml src/Recordum.WPF/Views/HsvColorPicker.xaml src/Recordum.WPF/Converters/HexDigitsConverter.cs
git commit -m "feat(wpf): colour picker popup joins the floating-panel family + hex '#' prefix"
```

---

### Task 5: Selection ring + swatch hover (Feature 1)

**Files:**
- Modify: `src/Recordum.WPF/Views/ColorSwatchPicker.xaml` (swatch DataTemplates: palette + recents)

**Interfaces:** Consumes `ColorEqualsConverter` (Task 2). Uses a `BindingProxy` to bring the picker's `Color` into the ItemsControl item scope.

- [ ] **Step 1: Add a BindingProxy for the current colour.** If the project already has a `BindingProxy` helper (search `class BindingProxy`), reuse it; otherwise it exists at `src/Recordum.WPF/Helpers/BindingProxy.cs` (used by HabitScreen). In `ColorSwatchPicker.xaml` `<UserControl.Resources>` add:
```xml
<helpers:BindingProxy x:Key="CurrentColorProxy"
    Data="{Binding Color, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}}"/>
```
(add `xmlns:helpers="clr-namespace:Recordum.WPF.Helpers"` if missing.)

- [ ] **Step 2: Swatch template with ring + hover.** Replace **each** swatch `DataTemplate` (palette grid + recents) `<Border …>` with one that draws a selection ring when its colour equals the current colour and a muted ring on hover. Use a shared style so palette/recents/favourites stay identical — add to `<UserControl.Resources>`:
```xml
<Style x:Key="Swatch" TargetType="Border">
    <Setter Property="Width" Value="20"/>
    <Setter Property="Height" Value="20"/>
    <Setter Property="Margin" Value="2"/>
    <Setter Property="CornerRadius" Value="4"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Focusable" Value="True"/>
    <Setter Property="Background" Value="{Binding ., Converter={StaticResource HexToBrush}}"/>
    <Setter Property="AutomationProperties.Name" Value="{Binding .}"/>
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Effect">
                <Setter.Value><DropShadowEffect Color="#888888" BlurRadius="0" ShadowDepth="0"/></Setter.Value>
            </Setter>
        </Trigger>
        <DataTrigger Value="True">
            <DataTrigger.Binding>
                <MultiBinding Converter="{StaticResource ColorEqualsConverter}">
                    <Binding Path="."/>
                    <Binding Path="Data" Source="{StaticResource CurrentColorProxy}"/>
                </MultiBinding>
            </DataTrigger.Binding>
            <Setter Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
            <Setter Property="BorderThickness" Value="2"/>
        </DataTrigger>
    </Style.Triggers>
</Style>
```
Then in the palette and recents `DataTemplate`s, replace the `<Border …/>` with:
```xml
<Border Style="{StaticResource Swatch}"
        MouseLeftButtonUp="OnSwatchClick" KeyDown="OnSwatchKeyDown"/>
```
(The palette swatches are currently `Width=20` in a `WrapPanel` — keep that; the style sets the size. Leave the trigger swatch at the top untouched.)

- [ ] **Step 3: Compile** — `dotnet build src/Recordum.WPF/Recordum.WPF.csproj -p:EnableWindowsTargeting=true` → `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Views/ColorSwatchPicker.xaml
git commit -m "feat(wpf): colour picker selection ring + swatch hover (F1)"
```

---

### Task 6: Shade ramp row (Feature 2)

**Files:**
- Modify: `src/Recordum.WPF/Views/ColorSwatchPicker.xaml` (add the row), `src/Recordum.WPF/Views/ColorSwatchPicker.xaml.cs` (compute the ramp on Color change)
- Modify: `Recordum.WPF/Resources/Strings.resx` + `Strings.it.resx` (label `ColorPicker_Shades`)

**Interfaces:** Consumes `ShadeRamp.For` (Task 1). Produces an `ObservableCollection<string> Shades` property on the control + the `OnSwatchClick` reuse for clicking a shade.

- [ ] **Step 1: Add the strings.** Add to `Strings.resx`: name `ColorPicker_Shades`, value `Shades`. To `Strings.it.resx`: value `Tonalità`.

- [ ] **Step 2: Add the `Shades` collection + recompute on Color change.** In `ColorSwatchPicker.xaml.cs`:
- Add field/property:
```csharp
public System.Collections.ObjectModel.ObservableCollection<string> Shades { get; } = new();
```
- In the `Color` DP's `PropertyChangedCallback` (the static handler registered on `ColorProperty`; if none exists, add one in `DependencyProperty.Register(... new FrameworkPropertyMetadata("#808080", BindsTwoWayByDefault, OnColorChanged)`), add:
```csharp
private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
{
    var self = (ColorSwatchPicker)d;
    self.Shades.Clear();
    foreach (var c in Services.ShadeRamp.For((string)e.NewValue)) self.Shades.Add(c);
}
```
(If a callback already exists, append the ramp recompute to it instead of replacing it.)

- [ ] **Step 3: Add the row to the popup.** In `ColorSwatchPicker.xaml`, immediately **after** the `<local:HsvColorPicker …/>` line, insert:
```xml
<TextBlock Text="{x:Static res:Strings.ColorPicker_Shades}" Margin="0,12,0,4"
           FontSize="{StaticResource FontSize.Small}" Foreground="{DynamicResource Brush.TextMuted}"/>
<ItemsControl ItemsSource="{Binding Shades, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate><UniformGrid Rows="1"/></ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Border Height="22" CornerRadius="0" Cursor="Hand" Focusable="True"
                    Background="{Binding ., Converter={StaticResource HexToBrush}}"
                    AutomationProperties.Name="{Binding .}"
                    MouseLeftButtonUp="OnSwatchClick" KeyDown="OnSwatchKeyDown"/>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

- [ ] **Step 4: Compile** — `dotnet build src/Recordum.WPF/Recordum.WPF.csproj -p:EnableWindowsTargeting=true` → `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Views/ColorSwatchPicker.xaml src/Recordum.WPF/Views/ColorSwatchPicker.xaml.cs src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx
git commit -m "feat(wpf): colour picker shade ramp row (F2)"
```

---

## Phase 3 — Favourites (Feature 4)

### Task 7: `FavoriteColors` DP + add/remove commands + favourites UI

**Files:**
- Modify: `src/Recordum.WPF/Views/ColorSwatchPicker.xaml.cs` (DPs), `…/ColorSwatchPicker.xaml` (UI)
- Modify: `Strings.resx` + `Strings.it.resx` (`ColorPicker_Favorites`, `ColorPicker_AddFavorite`)

**Interfaces:** Produces three DPs on `ColorSwatchPicker`: `FavoriteColors` (`IEnumerable<string>`), `AddFavoriteCommand` (`ICommand`), `RemoveFavoriteCommand` (`ICommand`). Mirror the existing `RecentColors` DP exactly (same pattern/notify).

- [ ] **Step 1: Add the strings.** `Strings.resx`: `ColorPicker_Favorites` = `Favorites`, `ColorPicker_AddFavorite` = `+ Add`. `Strings.it.resx`: `Preferiti`, `+ Aggiungi`.

- [ ] **Step 2: Add the DPs** in `ColorSwatchPicker.xaml.cs`, mirroring the existing `RecentColorsProperty` block:
```csharp
public static readonly DependencyProperty FavoriteColorsProperty = DependencyProperty.Register(
    nameof(FavoriteColors), typeof(IEnumerable<string>), typeof(ColorSwatchPicker),
    new PropertyMetadata(System.Array.Empty<string>()));
public IEnumerable<string> FavoriteColors
{
    get => (IEnumerable<string>)GetValue(FavoriteColorsProperty);
    set => SetValue(FavoriteColorsProperty, value);
}

public static readonly DependencyProperty AddFavoriteCommandProperty = DependencyProperty.Register(
    nameof(AddFavoriteCommand), typeof(System.Windows.Input.ICommand), typeof(ColorSwatchPicker), new PropertyMetadata(null));
public System.Windows.Input.ICommand? AddFavoriteCommand
{
    get => (System.Windows.Input.ICommand?)GetValue(AddFavoriteCommandProperty);
    set => SetValue(AddFavoriteCommandProperty, value);
}

public static readonly DependencyProperty RemoveFavoriteCommandProperty = DependencyProperty.Register(
    nameof(RemoveFavoriteCommand), typeof(System.Windows.Input.ICommand), typeof(ColorSwatchPicker), new PropertyMetadata(null));
public System.Windows.Input.ICommand? RemoveFavoriteCommand
{
    get => (System.Windows.Input.ICommand?)GetValue(RemoveFavoriteCommandProperty);
    set => SetValue(RemoveFavoriteCommandProperty, value);
}
```

- [ ] **Step 3: Add the favourites UI** in `ColorSwatchPicker.xaml`, after the recents `ItemsControl`:
```xml
<DockPanel Margin="0,10,0,2">
    <TextBlock Text="{x:Static res:Strings.ColorPicker_Favorites}" DockPanel.Dock="Left"
               FontSize="{StaticResource FontSize.Small}" Foreground="{DynamicResource Brush.TextMuted}"/>
    <Button Content="{x:Static res:Strings.ColorPicker_AddFavorite}" DockPanel.Dock="Right"
            HorizontalAlignment="Right" Background="Transparent" BorderThickness="0" Padding="4,0"
            FontSize="{StaticResource FontSize.Small}" Foreground="{DynamicResource Brush.Accent}"
            Command="{Binding AddFavoriteCommand, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}}"
            CommandParameter="{Binding Color, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}}"/>
</DockPanel>
<ItemsControl ItemsSource="{Binding FavoriteColors, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}}">
    <ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Grid Margin="2">
                <Border Style="{StaticResource Swatch}" Margin="0"
                        MouseLeftButtonUp="OnSwatchClick" KeyDown="OnSwatchKeyDown"/>
                <Button Content="×" Width="13" Height="13" Padding="0" FontSize="9"
                        HorizontalAlignment="Right" VerticalAlignment="Top" Margin="0,-5,-5,0"
                        Background="{DynamicResource Brush.SurfaceElevated}" BorderThickness="1"
                        BorderBrush="{DynamicResource Brush.Border}" Foreground="{DynamicResource Brush.TextMuted}"
                        Command="{Binding RemoveFavoriteCommand, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}}"
                        CommandParameter="{Binding .}"/>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```
(Keeping the ✕ always visible is acceptable for v1; a hover-only refinement can follow.)

- [ ] **Step 4: Compile** — `dotnet build src/Recordum.WPF/Recordum.WPF.csproj -p:EnableWindowsTargeting=true` → `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Views/ColorSwatchPicker.xaml src/Recordum.WPF/Views/ColorSwatchPicker.xaml.cs src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx
git commit -m "feat(wpf): colour picker favourites row + DPs (F4 UI)"
```

---

### Task 8: Wire favourites into the three host VMs

**Files:**
- Modify VMs: `…/HabitEditor/Sections/NotesSectionViewModel.cs`, `…/HabitBuilder/HabitBuilderViewModel.cs`, `…/HabitEditor/HabitEditorViewModel.cs`
- Modify views: `…/Sections/NotesSectionView.xaml`, `HabitBuilder/HabitBuilderView.xaml`, `EditorPane.xaml`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/NotesSectionViewModelTests.cs` (add a favourites test)

**Interfaces:** Consumes `IFavoriteColorStore` (Task 3). Each VM gains: `ObservableCollection<string> FavoriteColors`, `[RelayCommand] AddFavorite(string hex)`, `[RelayCommand] RemoveFavorite(string hex)`, and loads favourites where it loads recents.

- [ ] **Step 1: Failing test (NotesSectionViewModel).** Add to `NotesSectionViewModelTests.cs` (mirror the existing recents test; inject a real `FavoriteColorStore` over `FakeAppSettingService`):
```csharp
[Fact]
public async Task AddFavorite_then_load_exposes_the_color()
{
    var settings = new FakeAppSettingService();
    var favs = new FavoriteColorStore(settings);
    var sut = new NotesSectionViewModel(new FakeHabitConfigService(), new FakeRecentColorStore(), favs);

    await sut.AddFavoriteCommand.ExecuteAsync("#3b82f6");

    sut.FavoriteColors.Should().Contain("#3b82f6");
}
```
(Use the fakes already present in this test file; if `NotesSectionViewModel`'s ctor currently takes `(IHabitConfigService, IRecentColorStore)`, the new param is appended — see Step 3.)

- [ ] **Step 2: Compile to verify it fails** — FAIL: ctor arity / `FavoriteColors` / `AddFavoriteCommand` missing.

- [ ] **Step 3: Implement in `NotesSectionViewModel`** (then repeat verbatim in `HabitBuilderViewModel` and `HabitEditorViewModel`):
- Append `IFavoriteColorStore favoriteColors` to the primary constructor; store `private readonly IFavoriteColorStore _favoriteColors = favoriteColors;`.
- Add:
```csharp
public ObservableCollection<string> FavoriteColors { get; } = new();

[RelayCommand]
private async Task AddFavoriteAsync(string? hex)
{
    if (string.IsNullOrWhiteSpace(hex)) return;
    await _favoriteColors.AddAsync(hex).ConfigureAwait(true);
    await ReloadFavoritesAsync().ConfigureAwait(true);
}

[RelayCommand]
private async Task RemoveFavoriteAsync(string? hex)
{
    if (string.IsNullOrWhiteSpace(hex)) return;
    await _favoriteColors.RemoveAsync(hex).ConfigureAwait(true);
    await ReloadFavoritesAsync().ConfigureAwait(true);
}

private async Task ReloadFavoritesAsync()
{
    FavoriteColors.Clear();
    foreach (var c in await _favoriteColors.GetAsync().ConfigureAwait(true)) FavoriteColors.Add(c);
}
```
- Wherever the VM loads `RecentColors` (e.g. its load/activate method), also call `await ReloadFavoritesAsync();`.

- [ ] **Step 4: Bind in the three views.** On each `<v:ColorSwatchPicker …>` that already sets `RecentColors="{Binding RecentColors}"`, add:
```xml
FavoriteColors="{Binding FavoriteColors}"
AddFavoriteCommand="{Binding AddFavoriteCommand}"
RemoveFavoriteCommand="{Binding RemoveFavoriteCommand}"
```

- [ ] **Step 5: Compile** both projects with `-p:EnableWindowsTargeting=true` → `Build succeeded`.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitEditor/Sections/NotesSectionViewModel.cs src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs src/Recordum.WPF/Views/HabitEditor/Sections/NotesSectionView.xaml src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml src/Recordum.WPF/Views/EditorPane.xaml tests/Recordum.WPF.Tests/ViewModels/HabitEditor/NotesSectionViewModelTests.cs
git commit -m "feat(wpf): wire favourite colours into editor/builder/notes VMs (F4)"
```

---

## Phase 4 — Screen eyedropper (Feature 3) — VM-only, isolated, build last

> ⚠️ Cannot be run or visually verified on macOS. Compile-only here; full verification on the VM. Behind `IScreenColorPicker`; if it throws/returns null the picker keeps working.

### Task 9: `IScreenColorPicker` + Win32 `ScreenColorPicker`

**Files:**
- Create: `src/Recordum.WPF/Services/IScreenColorPicker.cs`, `src/Recordum.WPF/Services/ScreenColorPicker.cs`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`

**Interfaces:** Produces `IScreenColorPicker { Task<string?> PickAsync(); }` — returns a `#rrggbb` colour the user clicked anywhere on screen, or `null` if cancelled (Esc).

- [ ] **Step 1: Interface.**
```csharp
namespace Recordum.WPF.Services;

/// <summary>Lets the user sample a colour from anywhere on screen (eyedropper). Windows-only.</summary>
public interface IScreenColorPicker
{
    /// <returns>A #rrggbb colour, or null if the user cancelled (Esc / right-click).</returns>
    System.Threading.Tasks.Task<string?> PickAsync();
}
```

- [ ] **Step 2: Win32 implementation.** A full-screen transparent topmost `Window` captures the mouse; on left-click it reads the pixel under the cursor via the desktop DC; Esc/right-click cancels. Create `ScreenColorPicker.cs`:
```csharp
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Recordum.WPF.Services;

public sealed partial class ScreenColorPicker : IScreenColorPicker
{
    [LibraryImport("user32.dll")] private static partial IntPtr GetDC(IntPtr hWnd);
    [LibraryImport("user32.dll")] private static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [LibraryImport("gdi32.dll")]  private static partial uint GetPixel(IntPtr hdc, int x, int y);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }

    public Task<string?> PickAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        var overlay = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true, Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true, ShowInTaskbar = false, Cursor = Cursors.Cross,
            Left = SystemParameters.VirtualScreenLeft, Top = SystemParameters.VirtualScreenTop,
            Width = SystemParameters.VirtualScreenWidth, Height = SystemParameters.VirtualScreenHeight,
        };
        void Close(string? result) { overlay.Close(); tcs.TrySetResult(result); }
        overlay.MouseLeftButtonDown += (_, _) =>
        {
            GetCursorPos(out var p);
            var dc = GetDC(IntPtr.Zero);
            try
            {
                var bgr = GetPixel(dc, p.X, p.Y);
                var r = (byte)(bgr & 0xFF); var g = (byte)((bgr >> 8) & 0xFF); var b = (byte)((bgr >> 16) & 0xFF);
                Close($"#{r:x2}{g:x2}{b:x2}");
            }
            finally { ReleaseDC(IntPtr.Zero, dc); }
        };
        overlay.MouseRightButtonDown += (_, _) => Close(null);
        overlay.KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(null); };
        overlay.Show();
        overlay.Activate();
        return tcs.Task;
    }
}
```

- [ ] **Step 3: Register in DI** (in `ServiceRegistrar.cs`, near the other WPF services):
```csharp
services.AddSingleton<IScreenColorPicker, ScreenColorPicker>();
```

- [ ] **Step 4: Compile** — `dotnet build src/Recordum.WPF/Recordum.WPF.csproj -p:EnableWindowsTargeting=true` → `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Services/IScreenColorPicker.cs src/Recordum.WPF/Services/ScreenColorPicker.cs src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs
git commit -m "feat(wpf): ScreenColorPicker (Win32 eyedropper) behind IScreenColorPicker"
```

---

### Task 10: Eyedropper button wired through the host VMs

**Files:**
- Modify: `ColorSwatchPicker.xaml` (button) + `.xaml.cs` (`EyedropperCommand` DP)
- Modify VMs: NotesSection / HabitBuilder / HabitEditor (`[RelayCommand] Eyedrop`), views (bind), `Strings.resx`/`.it.resx` (`ColorPicker_Eyedropper` tooltip)

**Interfaces:** Produces `EyedropperCommand` (`ICommand`) DP on the control. Each host VM gains `IScreenColorPicker` + `[RelayCommand] async Task Eyedrop()` that sets the bound colour.

- [ ] **Step 1: Strings.** `Strings.resx`: `ColorPicker_Eyedropper` = `Pick from screen`. `Strings.it.resx`: `Preleva dallo schermo`.

- [ ] **Step 2: `EyedropperCommand` DP** in `ColorSwatchPicker.xaml.cs` (same shape as `AddFavoriteCommand` in Task 7). Add the button in `ColorSwatchPicker.xaml` inside the hex row (after the preview swatch):
```xml
<Button Width="30" Height="30" Margin="8,0,0,0" Padding="0"
        Background="{DynamicResource Brush.Surface}" BorderBrush="{DynamicResource Brush.Border}" BorderThickness="1"
        ToolTip="{x:Static res:Strings.ColorPicker_Eyedropper}"
        Visibility="{Binding EyedropperCommand, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}, Converter={StaticResource NullToVisibility}}"
        Command="{Binding EyedropperCommand, RelativeSource={RelativeSource AncestorType={x:Type local:ColorSwatchPicker}}}">
    <Path Width="15" Height="15" Stretch="Uniform"
          Stroke="{DynamicResource Brush.TextMuted}" StrokeThickness="1.6"
          StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
          Data="M11 7l6 6 M4 20l4-1 9.5-9.5a2.1 2.1 0 0 0-3-3L5 13l-1 4z"/>
</Button>
```
(`NullToVisibility` already exists in the project — it hides the button when no command is bound, e.g. on the VM if the eyedropper is disabled.)

- [ ] **Step 3: Host VM command** (NotesSection / HabitBuilder / HabitEditor): append `IScreenColorPicker screenPicker` to the ctor, store it, add:
```csharp
[RelayCommand]
private async Task EyedropAsync()
{
    var picked = await _screenPicker.PickAsync().ConfigureAwait(true);
    if (!string.IsNullOrWhiteSpace(picked)) <BoundColorProperty> = picked;
}
```
Replace `<BoundColorProperty>` with the property each VM binds to the picker's `Color` (e.g. `NoteColor` / `Color`). Bind on each view's `<v:ColorSwatchPicker …>`: `EyedropperCommand="{Binding EyedropCommand}"`.

- [ ] **Step 4: Compile** both projects with `-p:EnableWindowsTargeting=true` → `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(wpf): colour picker eyedropper button wired through host VMs (F3)"
```

---

## Final: docs + VM checklist

- [ ] Update `docs/phase-4-issues.md` (R-colorPicker entry), `docs/dev-environment-sync.md` (VM checklist: selection ring, ramp, favourites add/remove/persist, eyedropper sampling), `CLAUDE.md` UX line. Commit.

---

## Self-review notes
- **Spec coverage**: polish (T4) · F1 selection (T5) · F2 ramp (T1+T6) · F4 favourites (T3+T7+T8) · F3 eyedropper (T9+T10). ✓
- **Eyedropper isolation/sequence**: behind `IScreenColorPicker`, built last, hides via `NullToVisibility` when unbound. ✓
- **Types consistent**: `ShadeRamp.For`, `ColorEqualsConverter.Convert(object[])`, `IFavoriteColorStore.{Get,Add,Remove}Async`, control DPs `FavoriteColors`/`AddFavoriteCommand`/`RemoveFavoriteCommand`/`EyedropperCommand` referenced identically across tasks. ✓
- **Open verification points (VM)**: every UI task is compile-only on macOS; logic tests run on the VM. Flagged per task.
