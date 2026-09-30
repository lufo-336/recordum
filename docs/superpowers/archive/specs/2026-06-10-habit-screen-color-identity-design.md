# Habit Screen Color Identity — Design (#2 polish)

> **Status:** approved (2026-06-10). Filone #2 of 3 in the Phase 4 polish cluster (animation → **habit visual identity** → calendar redesign).

## Goal

Make the open habit workspace feel strongly tied to the habit's own color: every interactive element inside `HabitScreen` (button hover/focus borders, text-selection, calendar today/selected) adopts the habit color instead of the neutral theme accent — live, updating when the color changes in Configura. Builds on #5 (top strip + active tab already in the habit color).

## Approach (chosen: local `Brush.Accent` override)

The theme defines `Brush.Accent` (a `SolidColorBrush`) in `Light.xaml` / `Dark.xaml`. Every interactive style references it as `{DynamicResource Brush.Accent}` (button hover/focus border, `RepeatButton`/`ToggleButton`, ComboBox, `TextBox.SelectionBrush`, calendar today border + selected-day background). `DynamicResource` is resolved at the point of use, walking up from the element through its resource scopes.

**Therefore:** overriding `Brush.Accent` in `HabitScreen`'s `UserControl.Resources` with a brush bound to the habit color makes every descendant element re-resolve the accent to the habit color — automatically and consistently, with no per-element markup. Elements outside `HabitScreen` (Home, Charts, the Configura `EditorPane`) keep the theme accent.

### Reaching `Node.Color` from inside resources

Resources have no `DataContext`, so a plain `{Binding Node.Color}` can't resolve there. A `BindingProxy` (a `Freezable` that carries an inherited `DataContext`) bridges this — the standard WPF pattern. The proxy's `Data` binds to `Node.Color`; the overridden brush binds its `Color` to the proxy's `Data` through a hex→`Color` converter.

```xml
<UserControl.Resources>
    <conv:BindingProxy x:Key="HabitColorProxy" Data="{Binding Node.Color}"/>
    <SolidColorBrush x:Key="Brush.Accent"
                     Color="{Binding Data, Source={StaticResource HabitColorProxy},
                             Converter={StaticResource HexToColor}}"/>
    <!-- existing resources … -->
</UserControl.Resources>
```

When `Node.Color` changes (Configura live-edit, via the existing `HabitUpdated` wiring that #5 relies on), the binding updates the brush `Color` and every consumer re-renders.

### What this colors (within `HabitScreen`)

- **Button / ComboBox hover + keyboard-focus borders** → habit color (the literal "hover highlights in the habit color" ask).
- **`TextBox` selection highlight** (quick-add numeric/text inputs) → habit color. No contrast issue (selection sits behind unchanged `Brush.Text`).
- **Calendar today border** → habit color (border only, no contrast issue).
- **Calendar selected-day background** → habit color. **Filled state — needs contrast handling (below).**
- The #5 top strip and active tab already use the habit color via `HexToBrush` on `Node.Color`; unaffected and consistent.

## Contrast safety

The only **filled** accent state inside `HabitScreen` is the calendar **selected day** (`Background = Brush.Accent`, `Foreground = Brush.OnAccent` = fixed white). On light habit colors (e.g. yellow) white text is illegible. The pill-toggle filled state (`Recordum.PillToggle`) lives in the `EditorPane`, out of scope.

**Fix:** in `CalendarNavigator.xaml`, change the selected-day cell's `Foreground` from `{DynamicResource Brush.OnAccent}` to a contrast-aware brush computed from the habit color, using the existing `ContrastForegroundConverter`. The cell already exposes the habit color as `TintColorHex` (`HabitScreenViewModel` sets `Calendar.CompletedColorHex = node.Color`, and `ApplyMarkersAsync` copies it to each `cell.TintColorHex`):

```xml
<!-- Selected: filled accent background, contrast-aware text -->
<DataTrigger Binding="{Binding IsSelected}" Value="True">
    <Setter Property="Background" Value="{DynamicResource Brush.Accent}"/>
    <Setter Property="Foreground"
            Value="{Binding TintColorHex, Converter={StaticResource ContrastForeground}}"/>
</DataTrigger>
```

The selected-day background (`Brush.Accent`, overridden = habit color) and the foreground source (`TintColorHex` = `node.Color`) are the same color, so the contrast is computed against the actual background. Correct.

## Scope

- **In:** `HabitScreen` workspace (all three tabs).
- **Out:** the `EditorPane` / Configura (keeps the theme accent — could extend later), Home, Charts.
- **Calendar:** adopts the accent now (today/selected in the habit color) plus the contrast fix. Its full visual redesign — square cells + day/week/month/year view modes — is **filone #3** and out of scope here.

## Files

- `src/Recordum.WPF/Helpers/BindingProxy.cs` — **new**: `Freezable` carrying a bindable `Data` (DependencyProperty). ~12 lines.
- `src/Recordum.WPF/Converters/HexToColorConverter.cs` — **new**: `IValueConverter`, hex `#RRGGBB` → `System.Windows.Media.Color`; invalid/empty → a default accent color (`#3B82F6`) so the brush is never null.
- `src/Recordum.WPF/App.xaml` — **modify**: register `HexToColorConverter` as `HexToColor` (alongside the other `conv:` entries).
- `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` — **modify**: add the `xmlns` for the proxy namespace if needed, the `BindingProxy`, and the `Brush.Accent` override in `UserControl.Resources`.
- `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml` — **modify**: contrast-aware selected-day foreground.
- `tests/Recordum.WPF.Tests/Converters/HexToColorConverterTests.cs` — **new**.

## Testing

- **TDD unit:** `HexToColorConverter` — valid hex → correct RGB `Color`; malformed/empty/null → the default fallback color. (Mirror the existing `HabitTintBrushConverterTests` style.)
- **`BindingProxy`:** trivial `Freezable` boilerplate — no unit test.
- **Build:** `dotnet build src/Recordum.WPF` must stay 0 warning / 0 error.
- **Visual verification (skill `run`):**
  1. Open a habit; hover over buttons (Export, Configura, range buttons, Save/Mark, the ✕ series delete) → border highlights in the **habit color** (test several habits with different colors, Light and Dark).
  2. Keyboard-focus a button (Tab) → focus border in the habit color.
  3. Calendar: today's border and the selected day's background are in the habit color; the selected day's number stays **legible** (test a light color like yellow and a dark one).
  4. Change the color in Configura → all of the above update live.
  5. Home rows, the Charts workspace, and the Configura editor pane are **unaffected** (still neutral accent).

## Out of scope

- Filling primary action buttons (Save/Mark) in the habit color (the rejected option 3) — revisit only if #2 feels too subtle after verification.
- Any calendar layout/view-mode change (filone #3).
- Extending the accent override into the Configura `EditorPane`.
```